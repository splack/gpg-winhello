using System;
using System.IO;
using System.Threading.Tasks;

namespace GpgWinHello;

/// <summary>
/// Handles automated installation to user-level directory.
/// Copies executable to %LOCALAPPDATA%\Programs\gpg-winhello\ and optionally runs setup.
/// </summary>
public static class Install
{
    private static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "gpg-winhello"
    );

    private static readonly string InstallPath = Path.Combine(InstallDir, "gpg-winhello.exe");

    public static async Task<int> RunAsync()
    {
        try
        {
            Console.WriteLine("GPG Windows Hello Installer");
            Console.WriteLine("===========================\n");

            var currentPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentPath))
            {
                Console.Error.WriteLine("ERROR: Could not determine current executable path.");
                return 1;
            }

            // Check if already running from install location
            if (Path.GetFullPath(currentPath).Equals(Path.GetFullPath(InstallPath), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Already installed at: {InstallPath}");
                Console.WriteLine("\nWould you like to enroll your passphrase now? (y/N): ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (response == "y" || response == "yes")
                {
                    Console.WriteLine();
                    return await Setup.RunAsync().ConfigureAwait(false);
                }

                return 0;
            }

            // Create install directory
            Console.WriteLine($"Installing to: {InstallDir}");
            Directory.CreateDirectory(InstallDir);

            // Copy executable
            Console.WriteLine("Copying executable...");
            File.Copy(currentPath, InstallPath, overwrite: true);
            Console.WriteLine("✓ Executable copied successfully.\n");

            // Display success and next steps
            Console.WriteLine("Installation complete!");
            Console.WriteLine($"Installed to: {InstallPath}\n");

            Console.WriteLine("Next steps:");
            Console.WriteLine($"1. Enroll your passphrase:");
            Console.WriteLine($"     cd \"{InstallDir}\"");
            Console.WriteLine($"     .\\gpg-winhello.exe enroll");
            Console.WriteLine();
            Console.WriteLine($"2. Configure GPG agent:");
            Console.WriteLine($"     .\\gpg-winhello.exe config");
            Console.WriteLine();

            // Ask if user wants to run enrollment now
            Console.Write("Would you like to enroll your passphrase now? (Y/n): ");
            var runEnroll = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (runEnroll != "n" && runEnroll != "no")
            {
                Console.WriteLine($"\nSwitching to installed executable at: {InstallPath}");
                Console.WriteLine("Please run the following commands to complete enrollment:");
                Console.WriteLine($"    cd \"{InstallDir}\"");
                Console.WriteLine($"    .\\gpg-winhello.exe enroll");
                Console.WriteLine($"    .\\gpg-winhello.exe config");
                Console.WriteLine("\n(Commands must run from the installed location)");
            }

            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"ERROR: Permission denied. {ex.Message}");
            Console.Error.WriteLine("Try running from a location you have write access to.");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"ERROR: Installation failed. {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Unexpected error during installation: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Gets the installation directory path.
    /// </summary>
    public static string GetInstallDir()
    {
        return InstallDir;
    }

    /// <summary>
    /// Gets the full installation path for the executable.
    /// </summary>
    public static string GetInstallPath()
    {
        return InstallPath;
    }

    /// <summary>
    /// Checks if gpg-winhello is installed at the user-level location.
    /// </summary>
    public static bool IsInstalled()
    {
        return File.Exists(InstallPath);
    }
}
