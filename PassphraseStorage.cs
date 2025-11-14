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
/// Handles storage of encrypted credentials to disk.
/// File format: JSON with multiple credential types
/// {
///   "Version": 2,
///   "Credentials": {
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
    private partial class CredentialsJsonContext : JsonSerializerContext { }

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
            var json = JsonSerializer.Serialize(
                credFile,
                CredentialsJsonContext.Default.CredentialsFile
            );

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
            throw new IOException(
                $"Failed to save encrypted credentials due to permission error: {ex.Message}",
                ex
            );
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save encrypted credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a specific encrypted credential by type (e.g., "pin", "passphrase").
    /// </summary>
    public static byte[]? LoadEncryptedCredential(string credentialType)
    {
        if (!File.Exists(PassphraseFile))
        {
            throw new FileNotFoundException(
                $"Encrypted credentials file not found at: {PassphraseFile}\n"
                    + "Run 'gpg-winhello.exe enroll' first to configure Windows Hello encryption."
            );
        }

        try
        {
            string fileContent = File.ReadAllText(PassphraseFile);
            var credFile = JsonSerializer.Deserialize(
                fileContent,
                CredentialsJsonContext.Default.CredentialsFile
            );

            if (credFile == null || credFile.Version != 2)
            {
                throw new NotSupportedException(
                    $"Unsupported credentials file version: {credFile?.Version ?? -1}\n"
                        + "Run 'gpg-winhello.exe enroll' to re-enroll with current version."
                );
            }

            if (!credFile.Credentials.TryGetValue(credentialType, out var base64Data))
            {
                // Credential type not enrolled - return null so caller can handle fallback
                return null;
            }

            return Convert.FromBase64String(base64Data);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException(
                $"Permission denied reading encrypted credentials file: {ex.Message}",
                ex
            );
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to load encrypted credentials: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new CryptographicException(
                $"Corrupted credentials file (invalid JSON): {ex.Message}",
                ex
            );
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
