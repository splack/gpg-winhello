using System;
using System.IO;
using System.Threading.Tasks;

namespace GpgWinHello;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Setup mode: gpg-winhello.exe setup
            // Pinentry mode: gpg-winhello.exe (default)

            if (args.Length > 0 && args[0].ToLowerInvariant() == "setup")
            {
                return await Setup.RunAsync().ConfigureAwait(false);
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
}
