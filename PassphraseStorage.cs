using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GpgWinHello;

public class PassphraseStorage
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "gpg-winhello"
    );

    private static readonly string PassphraseFile = Path.Combine(StoragePath, "passphrase.enc");

    public static void SaveEncryptedPassphrase(byte[] encryptedData)
    {
        Directory.CreateDirectory(StoragePath);
        File.WriteAllBytes(PassphraseFile, encryptedData);

        // Set file permissions to current user only (Windows)
        var fileInfo = new FileInfo(PassphraseFile);
        var fileSecurity = fileInfo.GetAccessControl();

        // Remove inherited permissions
        fileSecurity.SetAccessRuleProtection(true, false);

        // Add explicit permission for current user
        var currentUser = WindowsIdentity.GetCurrent();
        var accessRule = new FileSystemAccessRule(
            currentUser.User!,
            FileSystemRights.FullControl,
            AccessControlType.Allow
        );
        fileSecurity.AddAccessRule(accessRule);

        fileInfo.SetAccessControl(fileSecurity);
    }

    public static byte[]? LoadEncryptedPassphrase()
    {
        if (!File.Exists(PassphraseFile))
        {
            Console.Error.WriteLine($"Encrypted passphrase file not found: {PassphraseFile}");
            Console.Error.WriteLine("Run gpg-winhello-setup.exe first to set up your passphrase.");
            return null;
        }

        return File.ReadAllBytes(PassphraseFile);
    }

    public static bool IsConfigured()
    {
        return File.Exists(PassphraseFile);
    }

    public static string GetStoragePath()
    {
        return StoragePath;
    }
}
