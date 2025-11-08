using System;
using System.Threading.Tasks;

namespace GpgWinHello;

public class Setup
{
    public static async Task<int> RunAsync()
    {
        Console.WriteLine("GPG Windows Hello Setup");
        Console.WriteLine("=======================\n");

        // Check Windows Hello support
        if (!await WindowsHelloHelper.IsSupportedAsync())
        {
            Console.Error.WriteLine("ERROR: Windows Hello is not supported on this device.");
            Console.Error.WriteLine("Please ensure Windows Hello is set up in Settings > Accounts > Sign-in options.");
            return 1;
        }

        Console.WriteLine("Windows Hello is supported.");

        // Create Windows Hello key
        Console.WriteLine("\nCreating Windows Hello key...");
        Console.WriteLine("You will be prompted for biometric authentication.");

        if (!await WindowsHelloHelper.CreateKeyAsync())
        {
            Console.Error.WriteLine("ERROR: Failed to create Windows Hello key.");
            Console.Error.WriteLine("Authentication may have been cancelled or failed.");
            return 1;
        }

        Console.WriteLine("Windows Hello key created successfully.");

        // Get passphrase from user
        Console.WriteLine("\nEnter your GPG passphrase:");
        Console.Write("> ");
        var passphrase = ReadPassword();

        if (string.IsNullOrWhiteSpace(passphrase))
        {
            Console.Error.WriteLine("ERROR: Passphrase cannot be empty.");
            return 1;
        }

        // Confirm passphrase
        Console.WriteLine("\nConfirm your GPG passphrase:");
        Console.Write("> ");
        var passphraseConfirm = ReadPassword();

        if (passphrase != passphraseConfirm)
        {
            Console.Error.WriteLine("ERROR: Passphrases do not match.");
            return 1;
        }

        // Get encryption key (triggers biometric auth)
        Console.WriteLine("\nEncrypting passphrase with Windows Hello...");
        Console.WriteLine("You will be prompted for biometric authentication.");

        var encryptionKey = await WindowsHelloHelper.GetEncryptionKeyAsync();
        if (encryptionKey == null)
        {
            Console.Error.WriteLine("ERROR: Failed to get encryption key from Windows Hello.");
            return 1;
        }

        // Encrypt and save
        var encryptedData = WindowsHelloHelper.EncryptPassphrase(passphrase, encryptionKey);
        PassphraseStorage.SaveEncryptedPassphrase(encryptedData);

        Console.WriteLine($"\nSuccess! Encrypted passphrase saved to:");
        Console.WriteLine($"  {PassphraseStorage.GetStoragePath()}");
        Console.WriteLine("\nNext steps:");
        Console.WriteLine("1. Copy pinentry-winhello.exe to a permanent location");
        Console.WriteLine("2. Edit %APPDATA%\\gnupg\\gpg-agent.conf");
        Console.WriteLine("3. Add line: pinentry-program C:\\path\\to\\pinentry-winhello.exe");
        Console.WriteLine("4. Restart gpg-agent: gpg-connect-agent killagent /bye");

        return 0;
    }

    private static string ReadPassword()
    {
        var password = "";
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password.Substring(0, password.Length - 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password;
    }
}
