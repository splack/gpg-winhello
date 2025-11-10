using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace GpgWinHello;

/// <summary>
/// Helper class for Windows Hello biometric authentication and cryptographic operations.
/// </summary>
public static class WindowsHelloHelper
{
    private const string KeyName = "gpg-winhello-key";

    /// <summary>
    /// Deterministic challenge used for key derivation.
    /// This ensures the same Windows Hello signature produces the same encryption key.
    /// Note: If the Windows Hello credential is compromised, all encrypted passphrases
    /// can be decrypted. This is an acceptable tradeoff for the convenience of biometric unlock.
    /// </summary>
    private const string Challenge = "gpg-winhello-deterministic-challenge-v1";

    /// <summary>
    /// Checks if Windows Hello is supported on this device.
    /// </summary>
    public static async Task<bool> IsSupportedAsync()
    {
        return await KeyCredentialManager.IsSupportedAsync().AsTask().ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a Windows Hello key credential already exists for this application.
    /// This does not trigger biometric authentication, only checks for existence.
    /// </summary>
    public static async Task<bool> KeyExistsAsync()
    {
        var openResult = await KeyCredentialManager.OpenAsync(KeyName).AsTask().ConfigureAwait(false);
        return openResult.Status == KeyCredentialStatus.Success;
    }

    /// <summary>
    /// Creates a new Windows Hello key credential for this application.
    /// This will prompt the user for biometric authentication.
    /// </summary>
    public static async Task<bool> CreateKeyAsync()
    {
        var result = await KeyCredentialManager.RequestCreateAsync(
            KeyName,
            KeyCredentialCreationOption.ReplaceExisting
        ).AsTask().ConfigureAwait(false);

        return result.Status == KeyCredentialStatus.Success;
    }

    /// <summary>
    /// Gets the encryption key by signing a challenge with Windows Hello.
    /// This will prompt the user for biometric authentication.
    /// IMPORTANT: Caller must call Array.Clear(key, 0, key.Length) when done with the key.
    /// </summary>
    public static async Task<byte[]?> GetEncryptionKeyAsync()
    {
        byte[]? signatureBytes = null;
        byte[]? keyBytes = null;

        try
        {
            var openResult = await KeyCredentialManager.OpenAsync(KeyName).AsTask().ConfigureAwait(false);

            if (openResult.Status != KeyCredentialStatus.Success)
            {
                throw new InvalidOperationException($"Failed to open Windows Hello key: {openResult.Status}");
            }

            var challengeBuffer = CryptographicBuffer.ConvertStringToBinary(
                Challenge,
                BinaryStringEncoding.Utf8
            );

            var signResult = await openResult.Credential.RequestSignAsync(challengeBuffer).AsTask().ConfigureAwait(false);

            if (signResult.Status != KeyCredentialStatus.Success)
            {
                throw new InvalidOperationException($"Failed to sign challenge: {signResult.Status}");
            }

            // Convert signature to byte array
            signatureBytes = signResult.Result.ToArray();

            // Hash the signature to get a consistent 32-byte key
            using var sha256 = SHA256.Create();
            keyBytes = sha256.ComputeHash(signatureBytes);

            // Validate key size
            if (keyBytes.Length != Constants.Crypto.KeySize)
            {
                throw new CryptographicException($"Expected {Constants.Crypto.KeySize} byte key, got {keyBytes.Length}");
            }

            return keyBytes;
        }
        catch (CryptographicException)
        {
            // Clear and rethrow cryptographic errors
            if (keyBytes != null) Array.Clear(keyBytes, 0, keyBytes.Length);
            throw;
        }
        catch (InvalidOperationException)
        {
            // Clear and rethrow Windows Hello errors
            if (keyBytes != null) Array.Clear(keyBytes, 0, keyBytes.Length);
            throw;
        }
        catch (Exception ex)
        {
            // Clear and wrap unexpected errors
            if (keyBytes != null) Array.Clear(keyBytes, 0, keyBytes.Length);
            throw new InvalidOperationException($"Unexpected error getting encryption key: {ex.Message}", ex);
        }
        finally
        {
            // Always clear intermediate signature data
            if (signatureBytes != null)
            {
                Array.Clear(signatureBytes, 0, signatureBytes.Length);
            }
        }
    }

    /// <summary>
    /// Encrypts a passphrase using AES-256-GCM authenticated encryption.
    /// Format: [nonce (12 bytes)][tag (16 bytes)][encrypted data]
    /// IMPORTANT: Caller should clear passphrase char[] after calling this.
    /// </summary>
    public static byte[] EncryptPassphrase(char[] passphrase, byte[] key)
    {
        if (passphrase == null || passphrase.Length == 0)
        {
            throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));
        }

        if (key == null || key.Length != Constants.Crypto.KeySize)
        {
            throw new ArgumentException($"Key must be {Constants.Crypto.KeySize} bytes", nameof(key));
        }

        byte[]? passphraseBytes = null;
        byte[]? nonce = null;
        byte[]? tag = null;
        byte[]? ciphertext = null;

        try
        {
            // Convert passphrase to UTF-8 bytes
            passphraseBytes = Encoding.UTF8.GetBytes(passphrase);

            // Generate random nonce
            nonce = new byte[Constants.Crypto.NonceSize];
            RandomNumberGenerator.Fill(nonce);

            // Allocate arrays for tag and ciphertext
            tag = new byte[Constants.Crypto.TagSize];
            ciphertext = new byte[passphraseBytes.Length];

            // Encrypt using AES-GCM
            using var aesGcm = new AesGcm(key, Constants.Crypto.TagSize);
            aesGcm.Encrypt(nonce, passphraseBytes, ciphertext, tag);

            // Combine: nonce + tag + ciphertext
            var result = new byte[Constants.Crypto.NonceSize + Constants.Crypto.TagSize + ciphertext.Length];
            System.Buffer.BlockCopy(nonce, 0, result, 0, Constants.Crypto.NonceSize);
            System.Buffer.BlockCopy(tag, 0, result, Constants.Crypto.NonceSize, Constants.Crypto.TagSize);
            System.Buffer.BlockCopy(ciphertext, 0, result, Constants.Crypto.NonceSize + Constants.Crypto.TagSize, ciphertext.Length);

            return result;
        }
        finally
        {
            // Always clear sensitive data
            if (passphraseBytes != null) Array.Clear(passphraseBytes, 0, passphraseBytes.Length);
            if (nonce != null) Array.Clear(nonce, 0, nonce.Length);
            if (tag != null) Array.Clear(tag, 0, tag.Length);
            if (ciphertext != null) Array.Clear(ciphertext, 0, ciphertext.Length);
        }
    }

    /// <summary>
    /// Decrypts a passphrase using AES-256-GCM authenticated encryption.
    /// Returns the passphrase as a char array.
    /// IMPORTANT: Caller MUST call Array.Clear(result, 0, result.Length) when done.
    /// </summary>
    public static char[]? DecryptPassphrase(byte[] encryptedData, byte[] key)
    {
        if (encryptedData == null)
        {
            throw new ArgumentNullException(nameof(encryptedData));
        }

        if (key == null || key.Length != Constants.Crypto.KeySize)
        {
            throw new ArgumentException($"Key must be {Constants.Crypto.KeySize} bytes", nameof(key));
        }

        // Validate minimum data length: nonce + tag + at least 1 byte ciphertext
        int minLength = Constants.Crypto.NonceSize + Constants.Crypto.TagSize + 1;
        if (encryptedData.Length < minLength)
        {
            throw new CryptographicException($"Encrypted data too short (minimum {minLength} bytes, got {encryptedData.Length})");
        }

        byte[]? nonce = null;
        byte[]? tag = null;
        byte[]? ciphertext = null;
        byte[]? decryptedBytes = null;

        try
        {
            // Extract components: nonce + tag + ciphertext
            nonce = new byte[Constants.Crypto.NonceSize];
            tag = new byte[Constants.Crypto.TagSize];
            int ciphertextLength = encryptedData.Length - Constants.Crypto.NonceSize - Constants.Crypto.TagSize;
            ciphertext = new byte[ciphertextLength];

            System.Buffer.BlockCopy(encryptedData, 0, nonce, 0, Constants.Crypto.NonceSize);
            System.Buffer.BlockCopy(encryptedData, Constants.Crypto.NonceSize, tag, 0, Constants.Crypto.TagSize);
            System.Buffer.BlockCopy(encryptedData, Constants.Crypto.NonceSize + Constants.Crypto.TagSize, ciphertext, 0, ciphertextLength);

            // Decrypt using AES-GCM
            decryptedBytes = new byte[ciphertextLength];
            using var aesGcm = new AesGcm(key, Constants.Crypto.TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, decryptedBytes);

            // Validate UTF-8 encoding
            if (!IsValidUtf8(decryptedBytes))
            {
                throw new CryptographicException("Decrypted data is not valid UTF-8");
            }

            // Convert to char array
            string passphraseString = Encoding.UTF8.GetString(decryptedBytes);
            char[] result = passphraseString.ToCharArray();

            // Clear the temporary string (best effort - strings are immutable)
            // The char array is what we return for proper clearing
            return result;
        }
        catch (CryptographicException ex)
        {
            // Authentication failed or decryption error - use generic message to avoid leaking info
            throw new CryptographicException("Authentication failed - passphrase could not be decrypted", ex);
        }
        finally
        {
            // Always clear sensitive data
            if (nonce != null) Array.Clear(nonce, 0, nonce.Length);
            if (tag != null) Array.Clear(tag, 0, tag.Length);
            if (ciphertext != null) Array.Clear(ciphertext, 0, ciphertext.Length);
            if (decryptedBytes != null) Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
        }
    }

    /// <summary>
    /// Validates that a byte array contains valid UTF-8 encoded text.
    /// </summary>
    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            var decoder = Encoding.UTF8.GetDecoder();
            decoder.Fallback = new DecoderExceptionFallback();

            char[] chars = new char[decoder.GetCharCount(bytes, 0, bytes.Length)];
            decoder.GetChars(bytes, 0, bytes.Length, chars, 0);

            Array.Clear(chars, 0, chars.Length);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
