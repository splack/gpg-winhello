using System;
using System.Collections.Generic;
using System.Linq;
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
            Console.WriteLine("GPG Windows Hello Enrollment");
            Console.WriteLine("============================\n");

            // Warn if running from a temporary location
            var exePath = Environment.ProcessPath ?? string.Empty;
            if (
                exePath.Contains("Downloads", StringComparison.OrdinalIgnoreCase)
                || exePath.Contains("Desktop", StringComparison.OrdinalIgnoreCase)
                || exePath.Contains("Temp", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("NOTE: Running enrollment from a temporary location.");
                Console.WriteLine($"Current location: {exePath}");
                Console.WriteLine("\nFor convenience, consider:");
                Console.WriteLine("1. Moving gpg-winhello.exe to a permanent location");
                Console.WriteLine("2. Then running enrollment from there");
                Console.WriteLine(
                    "\nThis way, the displayed path will match your final gpg-agent.conf configuration."
                );
                Console.WriteLine(
                    "(You can also just move the .exe later and run 'gpg-winhello.exe config' again)"
                );
                Console.Write("\nContinue with enrollment? (Y/n): ");

                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response == "n" || response == "no")
                {
                    Console.WriteLine("Setup cancelled.");
                    return 1;
                }
                Console.WriteLine();
            }

            // Check Windows version (Windows 10 1903+ required for KeyCredentialManager)
            var osVersion = Environment.OSVersion;
            if (osVersion.Platform == PlatformID.Win32NT)
            {
                // Windows 10 version 1903 is build 18362
                if (
                    osVersion.Version.Major < 10
                    || (osVersion.Version.Major == 10 && osVersion.Version.Build < 18362)
                )
                {
                    Console.Error.WriteLine(
                        $"ERROR: Windows version {osVersion.Version} is too old.\n"
                    );
                    Console.Error.WriteLine("Windows Hello KeyCredentialManager requires:");
                    Console.Error.WriteLine(
                        "  Windows 10 version 1903 (May 2019 Update, build 18362) or later"
                    );
                    Console.Error.WriteLine(
                        $"  Your version: {osVersion.Version} (build {osVersion.Version.Build})"
                    );
                    Console.Error.WriteLine(
                        "\nPlease update Windows via Settings > Update & Security > Windows Update"
                    );
                    return 1;
                }
            }

            // Check Windows Hello support
            if (!await WindowsHelloHelper.IsSupportedAsync().ConfigureAwait(false))
            {
                Console.Error.WriteLine("ERROR: Windows Hello is not supported on this device.\n");
                Console.Error.WriteLine("Troubleshooting:");
                Console.Error.WriteLine(
                    "1. Check: Settings > Accounts > Sign-in options > Windows Hello"
                );
                Console.Error.WriteLine("2. Verify fingerprint reader is configured and working");
                Console.Error.WriteLine("3. Ensure TPM 2.0 is enabled in BIOS/UEFI settings");
                Console.Error.WriteLine(
                    "4. Try: Settings > Update & Security > Windows Update (ensure fully updated)"
                );
                return 1;
            }

            Console.WriteLine("Windows Hello is supported.");

            // Ask about credential types
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("CREDENTIAL ENROLLMENT");
            Console.WriteLine(new string('=', 70));
            Console.WriteLine();
            Console.Write("Do you use a YubiKey or smartcard for GPG? (y/N): ");
            var hasYubiKey = Console.ReadLine()?.Trim().ToLowerInvariant() == "y";

            var credentialsToEnroll = new List<string>();
            if (hasYubiKey)
            {
                Console.WriteLine();
                Console.WriteLine("YubiKey setup detected. You can enroll:");
                Console.WriteLine("  1. PIN only (for YubiKey unlock)");
                Console.WriteLine(
                    "  2. PIN + Passphrase (recommended if you have both YubiKey and regular keys)"
                );
                Console.Write("\nEnroll PIN and passphrase? (Y/n): ");
                var enrollBoth = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (enrollBoth != "n" && enrollBoth != "no")
                {
                    credentialsToEnroll.Add("pin");
                    credentialsToEnroll.Add("passphrase");
                }
                else
                {
                    credentialsToEnroll.Add("pin");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Regular GPG key setup. Enrolling passphrase.");
                credentialsToEnroll.Add("passphrase");
            }

            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine();

            // Check if Windows Hello key already exists
            bool keyExists = await WindowsHelloHelper.KeyExistsAsync().ConfigureAwait(false);

            if (!keyExists)
            {
                // First enrollment - need to create key
                Console.WriteLine("Creating Windows Hello key...");
                Console.WriteLine("You will be prompted for biometric authentication.");

                if (!await WindowsHelloHelper.CreateKeyAsync().ConfigureAwait(false))
                {
                    Console.Error.WriteLine("ERROR: Failed to create Windows Hello key.");
                    Console.Error.WriteLine("Authentication may have been cancelled or failed.");
                    return 1;
                }

                Console.WriteLine("Windows Hello key created successfully.");
            }
            else
            {
                Console.WriteLine("Windows Hello key already exists, using existing key.");
            }

            // Get encryption key (triggers biometric auth)
            Console.WriteLine("\nGetting encryption key from Windows Hello...");
            Console.WriteLine("You will be prompted for biometric authentication.");

            encryptionKey = await WindowsHelloHelper.GetEncryptionKeyAsync().ConfigureAwait(false);
            if (encryptionKey == null)
            {
                Console.Error.WriteLine("ERROR: Failed to get encryption key from Windows Hello.");
                return 1;
            }

            // Collect and encrypt each credential type
            var encryptedCredentials = new Dictionary<string, byte[]>();

            foreach (var credType in credentialsToEnroll)
            {
                var credName = credType == "pin" ? "PIN" : "Passphrase";

                Console.WriteLine($"\nEnter your GPG {credName}:");
                Console.Write("> ");
                passphrase = ReadPassword();

                if (passphrase == null || passphrase.Length == 0)
                {
                    Console.Error.WriteLine($"ERROR: {credName} cannot be empty.");
                    return 1;
                }

                // Confirm credential
                Console.WriteLine($"\nConfirm your GPG {credName}:");
                Console.Write("> ");
                passphraseConfirm = ReadPassword();

                if (!AreEqual(passphrase, passphraseConfirm))
                {
                    Console.Error.WriteLine($"ERROR: {credName}s do not match.");
                    return 1;
                }

                // Clear confirmation (no longer needed)
                if (passphraseConfirm != null)
                {
                    Array.Clear(passphraseConfirm, 0, passphraseConfirm.Length);
                    passphraseConfirm = null;
                }

                // Encrypt credential
                Console.WriteLine($"\nEncrypting {credName} with Windows Hello...");
                var encryptedData = WindowsHelloHelper.EncryptPassphrase(passphrase, encryptionKey);
                encryptedCredentials[credType] = encryptedData;

                // Clear passphrase immediately
                Array.Clear(passphrase, 0, passphrase.Length);
                passphrase = null;
            }

            // Save all credentials
            PassphraseStorage.SaveEncryptedCredentials(encryptedCredentials);

            Console.WriteLine($"\n✓ Enrollment successful!");
            Console.WriteLine(
                $"  Enrolled credentials: {string.Join(", ", credentialsToEnroll.Select(c => c.ToUpper()))}"
            );
            Console.WriteLine($"  Storage location: {PassphraseStorage.GetStoragePath()}");
            Console.WriteLine($"  Executable location: {exePath}");
            Console.WriteLine($"  Configuration: {AppConfigManager.GetConfigPath()}");
            Console.WriteLine("\nNext steps:");
            Console.WriteLine("1. Configure GPG agent:");
            Console.WriteLine($"     gpg-winhello.exe config");
            Console.WriteLine("   Or manually edit %APPDATA%\\gnupg\\gpg-agent.conf and add:");
            Console.WriteLine($"     pinentry-program {exePath}");
            Console.WriteLine();
            Console.WriteLine("2. Test with: gopass show <entry> or ssh git@github.com");
            Console.WriteLine(
                "   You should be prompted for Windows Hello fingerprint authentication."
            );
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  • Edit {AppConfigManager.GetConfigPath()} to customize behavior");
            Console.WriteLine("  • See CONFIG.md for all available options");
            Console.WriteLine();
            Console.WriteLine("Troubleshooting:");
            Console.WriteLine(
                "  • Check the log file (see config.json for path, default: %APPDATA%\\gpg-winhello\\prompt.log)"
            );
            Console.WriteLine("  • Re-run enrollment to add missing credential types");

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
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;
        if (a.Length != b.Length)
            return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
