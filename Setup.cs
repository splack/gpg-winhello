using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GpgWinHello;

/// <summary>
/// Handles initial setup of Windows Hello encryption for GPG passphrase.
/// </summary>
public static class Setup
{
    public static async Task<int> RunAsync()
    {
        char[]? passphrase = null;
        char[]? passphraseConfirm = null;
        byte[]? encryptionKey = null;

        try
        {
            Console.WriteLine("GPG Windows Hello Setup");
            Console.WriteLine("=======================\n");

            // Check Windows Hello support
            if (!await WindowsHelloHelper.IsSupportedAsync().ConfigureAwait(false))
            {
                Console.Error.WriteLine("ERROR: Windows Hello is not supported on this device.");
                Console.Error.WriteLine("Please ensure Windows Hello is set up in Settings > Accounts > Sign-in options.");
                return 1;
            }

            Console.WriteLine("Windows Hello is supported.");

            // Create Windows Hello key
            Console.WriteLine("\nCreating Windows Hello key...");
            Console.WriteLine("You will be prompted for biometric authentication.");

            if (!await WindowsHelloHelper.CreateKeyAsync().ConfigureAwait(false))
            {
                Console.Error.WriteLine("ERROR: Failed to create Windows Hello key.");
                Console.Error.WriteLine("Authentication may have been cancelled or failed.");
                return 1;
            }

            Console.WriteLine("Windows Hello key created successfully.");

            // Get passphrase from user
            Console.WriteLine("\nEnter your GPG passphrase:");
            Console.Write("> ");
            passphrase = ReadPassword();

            if (passphrase == null || passphrase.Length == 0)
            {
                Console.Error.WriteLine("ERROR: Passphrase cannot be empty.");
                return 1;
            }

            // Confirm passphrase
            Console.WriteLine("\nConfirm your GPG passphrase:");
            Console.Write("> ");
            passphraseConfirm = ReadPassword();

            if (!AreEqual(passphrase, passphraseConfirm))
            {
                Console.Error.WriteLine("ERROR: Passphrases do not match.");
                return 1;
            }

            // Clear confirmation passphrase (no longer needed)
            if (passphraseConfirm != null)
            {
                Array.Clear(passphraseConfirm, 0, passphraseConfirm.Length);
                passphraseConfirm = null;
            }

            // Get encryption key (triggers biometric auth)
            Console.WriteLine("\nEncrypting passphrase with Windows Hello...");
            Console.WriteLine("You will be prompted for biometric authentication.");

            encryptionKey = await WindowsHelloHelper.GetEncryptionKeyAsync().ConfigureAwait(false);
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
            Console.WriteLine("1. Copy gpg-winhello.exe to a permanent location");
            Console.WriteLine("2. Edit %APPDATA%\\gnupg\\gpg-agent.conf");
            Console.WriteLine("3. Add line: pinentry-program C:\\path\\to\\gpg-winhello.exe");
            Console.WriteLine("4. Restart gpg-agent: gpg-connect-agent killagent /bye");

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Unexpected error during setup: {ex.Message}");
            return 1;
        }
        finally
        {
            // Always clear sensitive data from memory
            if (passphrase != null)
            {
                Array.Clear(passphrase, 0, passphrase.Length);
            }
            if (passphraseConfirm != null)
            {
                Array.Clear(passphraseConfirm, 0, passphraseConfirm.Length);
            }
            if (encryptionKey != null)
            {
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
            }
        }
    }

    /// <summary>
    /// Reads a password from console with masking.
    /// Returns a char array that MUST be cleared by the caller using Array.Clear().
    /// </summary>
    private static char[] ReadPassword()
    {
        var passwordList = new List<char>();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Backspace && passwordList.Count > 0)
            {
                // Use range operator to remove last character
                passwordList.RemoveAt(passwordList.Count - 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                passwordList.Add(key.KeyChar);
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return passwordList.ToArray();
    }

    /// <summary>
    /// Compares two char arrays for equality in constant time to prevent timing attacks.
    /// </summary>
    private static bool AreEqual(char[]? a, char[]? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
