using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace GpgWinHello;

public class WindowsHelloHelper
{
    private const string KeyName = "gpg-winhello-key";
    private const string Challenge = "gpg-winhello-deterministic-challenge-v1";

    public static async Task<bool> IsSupportedAsync()
    {
        return await KeyCredentialManager.IsSupportedAsync();
    }

    public static async Task<bool> CreateKeyAsync()
    {
        var result = await KeyCredentialManager.RequestCreateAsync(
            KeyName,
            KeyCredentialCreationOption.ReplaceExisting
        );

        return result.Status == KeyCredentialStatus.Success;
    }

    public static async Task<byte[]?> GetEncryptionKeyAsync()
    {
        try
        {
            var openResult = await KeyCredentialManager.OpenAsync(KeyName);

            if (openResult.Status != KeyCredentialStatus.Success)
            {
                Console.Error.WriteLine($"Failed to open key: {openResult.Status}");
                return null;
            }

            var challengeBuffer = CryptographicBuffer.ConvertStringToBinary(
                Challenge,
                BinaryStringEncoding.Utf8
            );

            var signResult = await openResult.Credential.RequestSignAsync(challengeBuffer);

            if (signResult.Status != KeyCredentialStatus.Success)
            {
                Console.Error.WriteLine($"Failed to sign: {signResult.Status}");
                return null;
            }

            // Hash the signature to get a consistent 32-byte key
            using var sha256 = SHA256.Create();
            var signatureBytes = signResult.Result.ToArray();
            return sha256.ComputeHash(signatureBytes);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error getting encryption key: {ex.Message}");
            return null;
        }
    }

    public static byte[] EncryptPassphrase(string passphrase, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        var encryptedData = encryptor.TransformFinalBlock(passphraseBytes, 0, passphraseBytes.Length);

        // Return IV + encrypted data
        return aes.IV.Concat(encryptedData).ToArray();
    }

    public static string? DecryptPassphrase(byte[] encryptedData, byte[] key)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = key;

            // Extract IV (first 16 bytes)
            var iv = encryptedData.Take(16).ToArray();
            var ciphertext = encryptedData.Skip(16).ToArray();

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error decrypting: {ex.Message}");
            return null;
        }
    }
}
