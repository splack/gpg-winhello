using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GpgWinHello;

/// <summary>
/// Handles automatic configuration of gpg-agent.conf.
/// </summary>
public static class Config
{
    private static readonly string GpgConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "gnupg"
    );

    private static readonly string GpgAgentConfig = Path.Combine(GpgConfigDir, "gpg-agent.conf");

    public static async Task<int> RunAsync()
    {
        try
        {
            Console.WriteLine("GPG Agent Configuration");
            Console.WriteLine("=======================\n");

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.Error.WriteLine("ERROR: Could not determine current executable path.");
                return 1;
            }

            // Ensure gnupg directory exists
            if (!Directory.Exists(GpgConfigDir))
            {
                Console.WriteLine($"Creating GPG configuration directory: {GpgConfigDir}");
                Directory.CreateDirectory(GpgConfigDir);
            }

            // Read existing config or create new one
            string[] existingLines = Array.Empty<string>();
            bool configExists = File.Exists(GpgAgentConfig);

            if (configExists)
            {
                existingLines = File.ReadAllLines(GpgAgentConfig);
                Console.WriteLine($"Found existing config: {GpgAgentConfig}");
            }
            else
            {
                Console.WriteLine($"No existing config found. Will create: {GpgAgentConfig}");
            }

            // Check if pinentry-program already set
            var pinentryLine = $"pinentry-program {exePath}";
            var existingPinentryLines = existingLines
                .Select((line, index) => new { line, index })
                .Where(x =>
                    x.line.TrimStart()
                        .StartsWith("pinentry-program", StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            if (existingPinentryLines.Any())
            {
                Console.WriteLine("\nExisting pinentry-program configuration found:");
                foreach (var item in existingPinentryLines)
                {
                    Console.WriteLine($"  {item.line}");
                }

                if (existingPinentryLines.Any(x => x.line.Trim() == pinentryLine))
                {
                    Console.WriteLine("\n✓ Already configured correctly!");
                    return 0;
                }

                Console.WriteLine("\nWould you like to replace it with:");
                Console.WriteLine($"  {pinentryLine}");
                Console.Write("\nReplace? (y/N): ");

                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    Console.WriteLine("Configuration cancelled.");
                    return 0;
                }

                // Remove old pinentry-program lines
                var newLines = existingLines
                    .Where((line, index) => !existingPinentryLines.Any(x => x.index == index))
                    .ToList();

                // Add new pinentry-program line
                newLines.Add(pinentryLine);

                File.WriteAllLines(GpgAgentConfig, newLines);
                Console.WriteLine("\n✓ Configuration updated!");
            }
            else
            {
                // No existing pinentry-program, just add it
                var newLines = existingLines.ToList();
                if (newLines.Any())
                {
                    newLines.Add(""); // Add blank line for readability
                }
                newLines.Add("# GPG Windows Hello pinentry");
                newLines.Add(pinentryLine);

                File.WriteAllLines(GpgAgentConfig, newLines);
                Console.WriteLine($"\n✓ Added to {GpgAgentConfig}:");
                Console.WriteLine($"  {pinentryLine}");
            }

            // Offer to restart gpg-agent
            Console.WriteLine("\nFor changes to take effect, gpg-agent must be restarted.");
            Console.Write("Restart gpg-agent now? (Y/n): ");

            var restart = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (restart != "n" && restart != "no")
            {
                Console.WriteLine("\nRestarting gpg-agent...");
                Console.WriteLine("Run these commands:");
                Console.WriteLine("  gpg-connect-agent killagent /bye");
                Console.WriteLine("  gpg-connect-agent /bye");
                Console.WriteLine("\n(Automatic restart requires gpg-connect-agent in PATH)");
            }

            Console.WriteLine("\n✓ Configuration complete!");
            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(
                $"ERROR: Permission denied accessing GPG configuration. {ex.Message}"
            );
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to configure gpg-agent.conf. {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Unexpected error during configuration: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Gets the gpg-agent.conf file path.
    /// </summary>
    public static string GetConfigPath()
    {
        return GpgAgentConfig;
    }

    /// <summary>
    /// Checks if gpg-agent.conf is configured with gpg-winhello.
    /// </summary>
    public static bool IsConfigured()
    {
        if (!File.Exists(GpgAgentConfig))
        {
            return false;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return false;
        }

        var lines = File.ReadAllLines(GpgAgentConfig);
        var pinentryLine = $"pinentry-program {exePath}";

        return lines.Any(line =>
            line.Trim().Equals(pinentryLine, StringComparison.OrdinalIgnoreCase)
        );
    }
}
