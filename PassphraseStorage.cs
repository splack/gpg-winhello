using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GpgWinHello;

/// <summary>
/// Handles storage of encrypted credentials to disk with version control.
/// File format (v2): JSON with multiple credential types
/// {
///   "version": 2,
///   "credentials": {
///     "pin": "base64-encoded-encrypted-pin",
///     "passphrase": "base64-encoded-passphrase"
///   }
/// }
/// Each encrypted value: [12 bytes nonce][16 bytes tag][encrypted data]
/// </summary>
public static partial class PassphraseStorage
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

    private class CredentialsFile
    {
        public int Version { get; set; } = 2;
        public Dictionary<string, string> Credentials { get; set; } = new();
    }

    /// <summary>
    /// JSON source generator context for Native AOT compatibility.
    /// </summary>
    [JsonSerializable(typeof(CredentialsFile))]
    private partial class CredentialsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Saves encrypted credentials to disk with user-only permissions.
    /// Supports multiple credential types (PIN, passphrase).
    /// </summary>
    public static void SaveEncryptedCredentials(Dictionary<string, byte[]> credentials)
    {
        if (credentials == null || credentials.Count == 0)
        {
            throw new ArgumentException("Credentials cannot be null or empty", nameof(credentials));
        }

        try
        {
            // Ensure storage directory exists
            Directory.CreateDirectory(StoragePath);

            // Convert byte arrays to base64 for JSON storage
            var credFile = new CredentialsFile();
            foreach (var kvp in credentials)
            {
                credFile.Credentials[kvp.Key] = Convert.ToBase64String(kvp.Value);
            }

            // Serialize to JSON using source generator for Native AOT
            var json = JsonSerializer.Serialize(credFile, CredentialsJsonContext.Default.CredentialsFile);

            // Write to file atomically
            string tempFile = PassphraseFile + ".tmp";
            File.WriteAllText(tempFile, json);

            // Set file permissions to current user only (Windows-specific)
            var fileInfo = new FileInfo(tempFile);
            var fileSecurity = fileInfo.GetAccessControl();
            fileSecurity.SetAccessRuleProtection(true, false);
            var currentUser = WindowsIdentity.GetCurrent();
            var accessRule = new FileSystemAccessRule(
                currentUser.User!,
                FileSystemRights.FullControl,
                AccessControlType.Allow
            );
            fileSecurity.AddAccessRule(accessRule);
            fileInfo.SetAccessControl(fileSecurity);

            // Atomic move to final location
            File.Move(tempFile, PassphraseFile, overwrite: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Failed to save encrypted credentials due to permission error: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save encrypted credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Legacy method for backwards compatibility with v1 format (single credential).
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
    /// Loads a specific encrypted credential by type (e.g., "pin", "passphrase").
    /// Supports both v2 (multi-credential JSON) and v1 (legacy single credential) formats.
    /// </summary>
    public static byte[]? LoadEncryptedCredential(string credentialType)
    {
        if (!File.Exists(PassphraseFile))
        {
            throw new FileNotFoundException(
                $"Encrypted credentials file not found at: {PassphraseFile}\n" +
                "Run 'gpg-winhello.exe enroll' first to configure Windows Hello encryption."
            );
        }

        try
        {
            string fileContent = File.ReadAllText(PassphraseFile);

            // Try to parse as JSON (v2 format)
            if (fileContent.TrimStart().StartsWith("{"))
            {
                var credFile = JsonSerializer.Deserialize(fileContent, CredentialsJsonContext.Default.CredentialsFile);

                if (credFile == null || credFile.Version != 2)
                {
                    throw new NotSupportedException(
                        $"Unsupported credentials file version: {credFile?.Version ?? -1}\n" +
                        "Run 'gpg-winhello.exe enroll' to re-enroll with current version."
                    );
                }

                if (!credFile.Credentials.TryGetValue(credentialType, out var base64Data))
                {
                    // Credential type not enrolled - return null so caller can handle fallback
                    return null;
                }

                return Convert.FromBase64String(base64Data);
            }
            else
            {
                // Legacy v1 format (binary with version header)
                // Treat as "passphrase" type for backwards compatibility
                if (credentialType != "passphrase")
                {
                    return null; // v1 only supports passphrase
                }

                return LoadLegacyV1Format();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied reading encrypted credentials file: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to load encrypted credentials: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new CryptographicException($"Corrupted credentials file (invalid JSON): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Legacy loader for v1 format.
    /// </summary>
    private static byte[] LoadLegacyV1Format()
    {
        byte[] fileData = File.ReadAllBytes(PassphraseFile);

        // Validate minimum file size
        int minSize = 1 + Constants.Crypto.NonceSize + Constants.Crypto.TagSize + 1;
        if (fileData.Length < minSize)
        {
            throw new CryptographicException(
                $"Encrypted credentials file is corrupted or too short (expected at least {minSize} bytes, got {fileData.Length})"
            );
        }

        // Extract and validate version
        byte version = fileData[0];
        if (version != 1)
        {
            throw new NotSupportedException(
                $"Unsupported legacy file version: {version}\n" +
                "Run 'gpg-winhello.exe enroll' to re-enroll with current version."
            );
        }

        // Extract encrypted data (without version header)
        byte[] encryptedData = new byte[fileData.Length - 1];
        System.Buffer.BlockCopy(fileData, 1, encryptedData, 0, encryptedData.Length);

        return encryptedData;
    }

    /// <summary>
    /// Legacy method for backwards compatibility.
    /// Loads encrypted passphrase from disk and validates version.
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
                    $"Encrypted passphrase file version {version} is incompatible with current version {Constants.Storage.CurrentVersion}.\n" +
                    "This may happen after upgrading gpg-winhello to a new version.\n\n" +
                    "BACKUP WARNING: Running setup will overwrite the existing encrypted file.\n" +
                    "If you need the old passphrase, decrypt it with the previous version first.\n\n" +
                    "To fix: Run 'gpg-winhello.exe setup' to re-encrypt with the current version."
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
