using System;
using System.Threading.Tasks;

namespace GpgWinHello;

public class Pinentry
{
    public static async Task<int> RunAsync()
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
                await HandleGetPinAsync();
            }
            else if (command.StartsWith("SETDESC"))
            {
                // Description of what we're asking for - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETPROMPT"))
            {
                // Prompt text - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETERROR"))
            {
                // Error message - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETOK"))
            {
                // OK button text - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETCANCEL"))
            {
                // Cancel button text - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETNOTOK"))
            {
                // Not OK button text - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETTITLE"))
            {
                // Window title - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETQUALITYBAR"))
            {
                // Quality bar - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETREPEATERROR"))
            {
                // Repeat error - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("SETREPEAT"))
            {
                // Repeat prompt - acknowledge but ignore
                Console.WriteLine("OK");
            }
            else if (command.StartsWith("OPTION"))
            {
                // Options - acknowledge but ignore
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

    private static async Task HandleGetPinAsync()
    {
        try
        {
            // Check if configured
            if (!PassphraseStorage.IsConfigured())
            {
                Console.Error.WriteLine("ERR 83886179 Not configured <Pinentry>");
                Console.WriteLine("ERR 83886179 Not configured");
                return;
            }

            // Load encrypted passphrase
            var encryptedData = PassphraseStorage.LoadEncryptedPassphrase();
            if (encryptedData == null)
            {
                Console.Error.WriteLine("ERR 83886179 Cannot load passphrase <Pinentry>");
                Console.WriteLine("ERR 83886179 Cannot load passphrase");
                return;
            }

            // Get decryption key from Windows Hello (triggers biometric auth)
            var encryptionKey = await WindowsHelloHelper.GetEncryptionKeyAsync();
            if (encryptionKey == null)
            {
                Console.Error.WriteLine("ERR 83886194 Cancelled <Pinentry>");
                Console.WriteLine("ERR 83886194 Cancelled");
                return;
            }

            // Decrypt passphrase
            var passphrase = WindowsHelloHelper.DecryptPassphrase(encryptedData, encryptionKey);
            if (passphrase == null)
            {
                Console.Error.WriteLine("ERR 83886179 Decryption failed <Pinentry>");
                Console.WriteLine("ERR 83886179 Decryption failed");
                return;
            }

            // Return passphrase to gpg-agent
            // Format: "D " followed by the passphrase (URL-encoded)
            var encoded = Uri.EscapeDataString(passphrase);
            Console.WriteLine($"D {encoded}");
            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERR 83886179 {ex.Message} <Pinentry>");
            Console.WriteLine($"ERR 83886179 {ex.Message}");
        }
    }
}
