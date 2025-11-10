using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GpgWinHello;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Install mode: gpg-winhello.exe install
            // Enroll mode: gpg-winhello.exe enroll
            // Config mode: gpg-winhello.exe config
            // Pinentry mode: gpg-winhello.exe (default)

            if (args.Length > 0)
            {
                var command = args[0].ToLowerInvariant();

                return command switch
                {
                    "install" => await Install.RunAsync().ConfigureAwait(false),
                    "enroll" => await Setup.RunAsync().ConfigureAwait(false),
                    "config" => await Config.RunAsync().ConfigureAwait(false),
                    "version" or "--version" or "-v" => ShowVersion(),
                    "help" or "--help" or "-h" or "/?" => ShowHelp(),
                    _ => await Pinentry.RunAsync().ConfigureAwait(false)
                };
            }
            else
            {
                return await Pinentry.RunAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static int ShowVersion()
    {
        Console.WriteLine($"gpg-winhello version {Constants.Version}");
        Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        return 0;
    }

    static int ShowHelp()
    {
        Console.WriteLine("GPG Windows Hello - Use Windows Hello fingerprint to unlock GPG");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  gpg-winhello.exe install    Install to user-level directory");
        Console.WriteLine("  gpg-winhello.exe enroll     Enroll GPG passphrase with Windows Hello");
        Console.WriteLine("  gpg-winhello.exe config     Configure gpg-agent.conf automatically");
        Console.WriteLine("  gpg-winhello.exe version    Show version information");
        Console.WriteLine("  gpg-winhello.exe            Run as pinentry (used by GPG agent)");
        Console.WriteLine("  gpg-winhello.exe help       Show this help message");
        Console.WriteLine();
        Console.WriteLine("Typical workflow:");
        Console.WriteLine("  1. gpg-winhello.exe install   # Copy to permanent location");
        Console.WriteLine("  2. gpg-winhello.exe enroll    # Enroll passphrase with Windows Hello");
        Console.WriteLine("  3. gpg-winhello.exe config    # Configure GPG agent (or edit manually)");
        Console.WriteLine();
        Console.WriteLine("For more information, see: https://github.com/splack/gpg-winhello");
        return 0;
    }
}
