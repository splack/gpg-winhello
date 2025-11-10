using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GpgWinHello;

/// <summary>
/// Implements the Assuan pinentry protocol for GPG agent.
/// Communicates via stdin/stdout using the pinentry protocol.
/// </summary>
public static class Pinentry
{
    private static string? _lastDescription = null;

    // MessageBox constants
    private const int MB_OK = 0x00000000;
    private const int MB_ICONINFORMATION = 0x00000040;
    private const int MB_OKCANCEL = 0x00000001;
    private const int IDOK = 1;
    private const int IDCANCEL = 2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static void ShowErrorDialog(string message)
    {
        MessageBox(IntPtr.Zero, message, "GPG Windows Hello - Error", MB_OK | MB_ICONINFORMATION);
    }

    private static void LogPromptRequest(string detectedType, string? description)
    {
        try
        {
            var config = AppConfigManager.Load();

            // Skip logging if disabled
            if (!config.Logging.Enabled)
            {
                return;
            }

            var logPath = config.Logging.Path;
            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var decodedDesc = string.IsNullOrEmpty(description)
                ? "(none)"
                : Uri.UnescapeDataString(description);

            var logEntry = $"[{timestamp}] Type={detectedType} | Description={decodedDesc}\n";

            File.AppendAllText(logPath, logEntry);
            Console.Error.WriteLine($"INFO: Logged prompt request to {logPath} <Pinentry>");
        }
        catch (Exception ex)
        {
            // Logging is best-effort, don't fail on log errors but report them
            Console.Error.WriteLine($"WARN: Failed to write prompt log: {ex.Message} <Pinentry>");
        }
    }

    public static async Task<int> RunAsync()
    {
        try
        {
            // Pinentry protocol: communicate via stdin/stdout
            // Send OK on startup
            Console.Error.WriteLine("INFO: gpg-winhello pinentry starting <Pinentry>");

            // Load config at startup to ensure config file is created
            // This also validates config early before any commands
            var _ = AppConfigManager.Load();

            Console.WriteLine("OK Pleased to meet you");
            Console.Out.Flush(); // Ensure gpg-agent receives this immediately

            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                var command = line.Trim();

                if (command.StartsWith("GETPIN"))
                {
                    Console.Error.WriteLine($"INFO: Received GETPIN command (lastDescription={_lastDescription ?? "null"}) <Pinentry>");
                    await HandleGetPinAsync(_lastDescription).ConfigureAwait(false);
                }
                else if (command.StartsWith("SETDESC "))
                {
                    // Capture the description GPG is sending about what it wants
                    _lastDescription = command.Substring("SETDESC ".Length);
                    Console.Error.WriteLine($"INFO: Received SETDESC: {_lastDescription} <Pinentry>");
                    Console.WriteLine("OK");
                    Console.Out.Flush();
                }
                else if (command == "CONFIRM")
                {
                    // Show message box and wait for user response
                    Console.Error.WriteLine($"INFO: Received CONFIRM command (description={_lastDescription ?? "none"}) <Pinentry>");
                    HandleConfirm(_lastDescription);
                }
                else if (command.StartsWith("SETPROMPT") ||
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
                    Console.Out.Flush();
                }
                else if (command == "BYE")
                {
                    Console.WriteLine("OK closing connection");
                    Console.Out.Flush();
                    return 0;
                }
                else if (command == "RESET")
                {
                    // Reset state
                    Console.WriteLine("OK");
                    Console.Out.Flush();
                }
                else
                {
                    // Unknown command - still acknowledge to keep protocol happy
                    Console.WriteLine("OK");
                    Console.Out.Flush();
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

    /// <summary>
    /// Detects what type of credential is being requested based on GPG's description.
    /// </summary>
    private static void HandleConfirm(string? description)
    {
        try
        {
            // Decode the URL-encoded description
            var message = string.IsNullOrEmpty(description)
                ? "Please confirm"
                : Uri.UnescapeDataString(description);

            Console.Error.WriteLine($"INFO: Showing confirmation dialog: {message} <Pinentry>");

            // Show message box with OK/Cancel
            int result = MessageBox(IntPtr.Zero, message, "GPG Windows Hello", MB_OKCANCEL | MB_ICONINFORMATION);

            if (result == IDOK)
            {
                Console.WriteLine("OK");
                Console.Out.Flush();
                Console.Error.WriteLine("INFO: User clicked OK on confirmation dialog <Pinentry>");
            }
            else
            {
                // User cancelled or closed the dialog
                Console.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Cancelled");
                Console.Out.Flush();
                Console.Error.WriteLine("INFO: User cancelled confirmation dialog <Pinentry>");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERR: Failed to show confirmation dialog: {ex.Message} <Pinentry>");
            Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Dialog error");
            Console.Out.Flush();
        }
    }

    private static string? DetectCredentialType(string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            Console.Error.WriteLine("ERROR: No description provided by GPG - cannot determine credential type <Pinentry>");
            return null;
        }

        var desc = Uri.UnescapeDataString(description).ToLowerInvariant();

        // Exact smartcard/YubiKey PIN pattern from GPG
        // Pattern: "Please unlock the card"
        if (desc.StartsWith("please unlock the card"))
        {
            return "pin";
        }

        // Exact passphrase patterns from GPG
        // Pattern: "Please enter the passphrase" or mentions "secret key"
        if (desc.StartsWith("please enter the passphrase") ||
            desc.Contains("secret key"))
        {
            return "passphrase";
        }

        // No match - return null to trigger error dialog
        Console.Error.WriteLine($"ERROR: Unknown credential request pattern. Description: {desc} <Pinentry>");
        return null;
    }

    private static async Task HandleGetPinAsync(string? description)
    {
        byte[]? encryptionKey = null;
        char[]? passphrase = null;
        string? encoded = null;

        try
        {
            // Detect what GPG is asking for
            var credentialType = DetectCredentialType(description);

            // Check if detection failed
            if (credentialType == null)
            {
                var decodedDesc = string.IsNullOrEmpty(description)
                    ? "(no description provided)"
                    : Uri.UnescapeDataString(description);

                var errorMsg = $"Unknown credential request from GPG.\n\n" +
                              $"Description received:\n{decodedDesc}\n\n" +
                              $"This pattern is not recognized. Please report this at:\n" +
                              $"https://github.com/splack/gpg-winhello/issues\n\n" +
                              $"Log file: %APPDATA%\\gpg-winhello\\prompt.log";

                ShowErrorDialog(errorMsg);

                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Unknown credential type <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Unknown credential request pattern");
                Console.Out.Flush();
                return;
            }

            // Log to file for debugging
            LogPromptRequest(credentialType, description);

            Console.Error.WriteLine($"INFO: GPG requesting '{credentialType}' (description: {description ?? "none"}) <Pinentry>");

            // Check if configured
            if (!PassphraseStorage.IsConfigured())
            {
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Not configured <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Not configured");
                Console.Out.Flush();
                return;
            }

            // Load encrypted credential for the detected type
            byte[]? encryptedData;
            try
            {
                encryptedData = PassphraseStorage.LoadEncryptedCredential(credentialType);

                if (encryptedData == null)
                {
                    // Credential type not enrolled - try fallback to passphrase
                    Console.Error.WriteLine($"WARN: Credential type '{credentialType}' not enrolled, trying 'passphrase' fallback <Pinentry>");
                    encryptedData = PassphraseStorage.LoadEncryptedCredential("passphrase");

                    if (encryptedData == null)
                    {
                        Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} No credentials enrolled <Pinentry>");
                        Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Credential not enrolled - run 'gpg-winhello.exe enroll'");
                        Console.Out.Flush();
                        return;
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Configuration not found");
                Console.Out.Flush();
                return;
            }
            catch (NotSupportedException ex)
            {
                // Version mismatch
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Version mismatch - run setup again");
                Console.Out.Flush();
                return;
            }
            catch (CryptographicException ex)
            {
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Corrupted passphrase file");
                Console.Out.Flush();
                return;
            }

            // Show info dialog if configured
            var config = AppConfigManager.Load();
            if (config.InfoDialog.Enabled)
            {
                var decodedDesc = string.IsNullOrEmpty(description)
                    ? "Unlock credential"
                    : Uri.UnescapeDataString(description);

                var infoMessage = $"{decodedDesc}\n\nClick OK to authenticate with Windows Hello.";

                Console.Error.WriteLine($"INFO: Showing info dialog: {infoMessage} <Pinentry>");

                int result = MessageBox(IntPtr.Zero, infoMessage, "GPG Windows Hello", MB_OKCANCEL | MB_ICONINFORMATION);

                if (result == IDCANCEL)
                {
                    Console.Error.WriteLine("INFO: User cancelled at info dialog <Pinentry>");
                    Console.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Cancelled");
                    Console.Out.Flush();
                    return;
                }

                Console.Error.WriteLine("INFO: User clicked OK on info dialog, proceeding to Windows Hello <Pinentry>");
            }

            // Get decryption key from Windows Hello (triggers biometric auth)
            try
            {
                encryptionKey = await WindowsHelloHelper.GetEncryptionKeyAsync().ConfigureAwait(false);
                if (encryptionKey == null)
                {
                    Console.Error.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Authentication cancelled <Pinentry>");
                    Console.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Cancelled");
                    Console.Out.Flush();
                    return;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Windows Hello failed
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.Cancelled} Authentication failed");
                Console.Out.Flush();
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
                    Console.Out.Flush();
                    return;
                }
            }
            catch (CryptographicException ex)
            {
                // Decryption or authentication failed - use generic message
                Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} {ex.Message} <Pinentry>");
                Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Authentication failed");
                Console.Out.Flush();
                return;
            }

            // Return passphrase to gpg-agent
            // Format: "D " followed by the passphrase (URL-encoded)
            // Convert char[] to string for URL encoding (temporary)
            encoded = Uri.EscapeDataString(new string(passphrase));
            Console.WriteLine($"D {encoded}");
            Console.WriteLine("OK");
            Console.Out.Flush();
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} I/O error: {ex.Message} <Pinentry>");
            Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} File access error");
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Unexpected error: {ex.Message} <Pinentry>");
            Console.WriteLine($"ERR {Constants.AssuanErrors.GeneralError} Internal error");
            Console.Out.Flush();
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
