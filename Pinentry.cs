using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GpgWinHello;

/// <summary>
/// Implements the Assuan pinentry protocol for GPG agent.
/// Communicates via stdin/stdout using the pinentry protocol.
/// </summary>
public static class Pinentry
{
    public static async Task<int> RunAsync()
    {
        try
        {
            // Pinentry protocol: communicate via stdin/stdout
            // Send OK on startup
            Console.WriteLine("OK Pleased to meet you");

            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                var command = line.Trim();

                if (command.StartsWith("GETPIN"))
                {
                    await HandleGetPinAsync().ConfigureAwait(false);
                }
                else if (command.StartsWith("SETDESC") ||
                         command.StartsWith("SETPROMPT") ||
                         command.StartsWith("SETERROR") ||
                         command.StartsWith("SETOK") ||
                         command.StartsWith("SETCANCEL") ||
                         command.StartsWith("SETNOTOK") ||
                         command.StartsWith("SETTITLE") ||
                         command.StartsWith("SETQUALITYBAR") ||
                         command.StartsWith("SETREPEATERROR") ||
                         command.StartsWith("SETREPEAT") ||
                         command.StartsWith("OPTION"))
                {
                    // Acknowledge commands we don't need to act on
                    Console.WriteLine("OK");
                }
                else if (command == "BYE")
                {
                    Console.WriteLine("OK closing connection");
                    return 0;
                }
                else if (command == "RESET")
                {
                    // Reset state
                    Console.WriteLine("OK");
                }
                else
                {
                    // Unknown command - still acknowledge to keep protocol happy
                    Console.WriteLine("OK");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error in pinentry: {ex.Message}");
            return 1;
        }
    }

    private static async Task HandleGetPinAsync()
    {
        byte[]? encryptionKey = null;
        char[]? passphrase = null;
        string? encoded = null;

        try
        {
            // Check if configured
            if (!PassphraseStorage.IsConfigured())
            {
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Not configured <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Not configured");
                return;
            }

            // Load encrypted passphrase
            byte[] encryptedData;
            try
            {
                encryptedData = PassphraseStorage.LoadEncryptedPassphrase()
                    ?? throw new InvalidOperationException("Failed to load encrypted passphrase");
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Configuration not found");
                return;
            }
            catch (NotSupportedException ex)
            {
                // Version mismatch
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Version mismatch - run setup again");
                return;
            }
            catch (CryptographicException ex)
            {
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Corrupted passphrase file");
                return;
            }

            // Get decryption key from Windows Hello (triggers biometric auth)
            try
            {
                encryptionKey = await WindowsHelloHelper.GetEncryptionKeyAsync().ConfigureAwait(false);
                if (encryptionKey == null)
                {
                    Console.Error.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Authentication cancelled <Pinentry>");
                    Console.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Cancelled");
                    return;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Windows Hello failed
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Authentication failed");
                return;
            }

            // Decrypt passphrase
            try
            {
                passphrase = WindowsHelloHelper.DecryptPassphrase(encryptedData, encryptionKey);
                if (passphrase == null || passphrase.Length == 0)
                {
                    Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Decryption returned empty result <Pinentry>");
                    Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Authentication failed");
                    return;
                }
            }
            catch (CryptographicException ex)
            {
                // Decryption or authentication failed - use generic message
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Authentication failed");
                return;
            }

            // Return passphrase to gpg-agent
            // Format: "D " followed by the passphrase (URL-encoded)
            // Convert char[] to string for URL encoding (temporary)
            encoded = Uri.EscapeDataString(new string(passphrase));
            Console.WriteLine($"D {encoded}");
            Console.WriteLine("OK");
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} I/O error: {ex.Message} <Pinentry>");
            Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} File access error");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Unexpected error: {ex.Message} <Pinentry>");
            Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Internal error");
        }
        finally
        {
            // Always clear sensitive data from memory
            if (encryptionKey != null)
            {
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
            }
            if (passphrase != null)
            {
                Array.Clear(passphrase, 0, passphrase.Length);
            }
            // Note: encoded string contains passphrase but is immutable
            // Best effort: set to null to mark for GC
            encoded = null;
        }
    }
}
