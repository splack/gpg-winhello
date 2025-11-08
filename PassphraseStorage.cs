using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace GpgWinHello;

/// <summary>
/// Handles storage of encrypted passphrases to disk with version control.
/// File format: [1 byte version][encrypted data]
/// Version 1: [1 byte version][12 bytes nonce][16 bytes tag][encrypted data]
/// </summary>
public static class PassphraseStorage
{
    /// <summary>
    /// Storage directory for encrypted passphrases.
    /// Windows: %APPDATA%\gpg-winhello\
    /// </summary>
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "gpg-winhello"
    );

    private static readonly string PassphraseFile = Path.Combine(StoragePath, "passphrase.enc");

    /// <summary>
    /// Saves an encrypted passphrase to disk with version header and user-only permissions.
    /// This is Windows-specific and sets ACLs to restrict access to the current user.
    /// </summary>
    public static void SaveEncryptedPassphrase(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0)
        {
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));
        }

        try
        {
            // Ensure storage directory exists
            Directory.CreateDirectory(StoragePath);

            // Prepend version header to encrypted data
            byte[] fileData = new byte[1 + encryptedData.Length];
            fileData[0] = Constants.Storage.CurrentVersion;
            System.Buffer.BlockCopy(encryptedData, 0, fileData, 1, encryptedData.Length);

            // Write to file atomically by writing to temp file then moving
            string tempFile = PassphraseFile + ".tmp";
            File.WriteAllBytes(tempFile, fileData);

            // Set file permissions to current user only (Windows-specific)
            var fileInfo = new FileInfo(tempFile);
            var fileSecurity = fileInfo.GetAccessControl();

            // Remove inherited permissions
            fileSecurity.SetAccessRuleProtection(true, false);

            // Add explicit permission for current user only
            var currentUser = WindowsIdentity.GetCurrent();
            var accessRule = new FileSystemAccessRule(
                currentUser.User!,
                FileSystemRights.FullControl,
                AccessControlType.Allow
            );
            fileSecurity.AddAccessRule(accessRule);
            fileInfo.SetAccessControl(fileSecurity);

            // Atomic move to final location (overwrites if exists)
            File.Move(tempFile, PassphraseFile, overwrite: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Failed to save encrypted passphrase due to permission error: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save encrypted passphrase: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads an encrypted passphrase from disk and validates version.
    /// Returns the encrypted data without the version header.
    /// </summary>
    public static byte[]? LoadEncryptedPassphrase()
    {
        if (!File.Exists(PassphraseFile))
        {
            throw new FileNotFoundException(
                $"Encrypted passphrase file not found at: {PassphraseFile}\n" +
                "Run 'gpg-winhello.exe setup' first to configure Windows Hello encryption."
            );
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(PassphraseFile);

            // Validate minimum file size (version + nonce + tag + at least 1 byte data)
            int minSize = 1 + Constants.Crypto.NonceSize + Constants.Crypto.TagSize + 1;
            if (fileData.Length < minSize)
            {
                throw new CryptographicException(
                    $"Encrypted passphrase file is corrupted or too short (expected at least {minSize} bytes, got {fileData.Length})"
                );
            }

            // Extract and validate version
            byte version = fileData[0];
            if (version != Constants.Storage.CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported encrypted passphrase file version: {version} (expected {Constants.Storage.CurrentVersion})\n" +
                    "You may need to run 'gpg-winhello.exe setup' again to re-encrypt with the current version."
                );
            }

            // Extract encrypted data (without version header)
            byte[] encryptedData = new byte[fileData.Length - 1];
            System.Buffer.BlockCopy(fileData, 1, encryptedData, 0, encryptedData.Length);

            return encryptedData;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied reading encrypted passphrase file: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to load encrypted passphrase: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if the passphrase storage is configured (encrypted passphrase file exists).
    /// </summary>
    public static bool IsConfigured()
    {
        return File.Exists(PassphraseFile);
    }

    /// <summary>
    /// Gets the storage directory path for informational purposes.
    /// </summary>
    public static string GetStoragePath()
    {
        return StoragePath;
    }
}
