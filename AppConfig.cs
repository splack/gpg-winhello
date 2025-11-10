using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GpgWinHello;

/// <summary>
/// Application configuration settings for gpg-winhello.
/// Stored at %APPDATA%\gpg-winhello\config.json
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Configuration schema version. Current: 1
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Info dialog configuration.
    /// </summary>
    public InfoDialogConfig InfoDialog { get; set; } = new();

    /// <summary>
    /// Logging configuration.
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();

    public class InfoDialogConfig
    {
        /// <summary>
        /// Whether to show an informational dialog before Windows Hello authentication.
        /// The dialog shows what credential is being requested (from GPG description).
        /// Default: true (recommended so user knows what they're unlocking).
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    public class LoggingConfig
    {
        /// <summary>
        /// Whether to enable logging of credential requests.
        /// Default: false (enable for troubleshooting credential detection).
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Path to log file. Supports environment variables.
        /// Default: %APPDATA%\gpg-winhello\prompt.log
        /// If null or empty, uses default path.
        /// </summary>
        public string? Path { get; set; }
    }
}

/// <summary>
/// Manages application configuration file loading and saving.
/// </summary>
public static partial class AppConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "gpg-winhello",
        "config.json"
    );

    /// <summary>
    /// JSON source generator context for Native AOT compatibility.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppConfig))]
    private partial class AppConfigJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Loads configuration from disk. If file doesn't exist, creates it with defaults.
    /// </summary>
    public static AppConfig Load()
    {
        AppConfig config;
        bool shouldSave = false;

        if (!File.Exists(ConfigPath))
        {
            config = new AppConfig();
            shouldSave = true;
        }
        else
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                config = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WARN: Failed to load config from {ConfigPath}: {ex.Message}. Using defaults.");
                config = new AppConfig();
                shouldSave = true;
            }
        }

        // Set default log path if not specified
        if (string.IsNullOrEmpty(config.Logging.Path))
        {
            config.Logging.Path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "gpg-winhello",
                "prompt.log"
            );
        }

        // Auto-create config file with defaults on first use
        if (shouldSave)
        {
            try
            {
                Save(config);
            }
            catch (Exception ex)
            {
                // Best effort - don't fail if we can't save config
                Console.Error.WriteLine($"WARN: Failed to create default config file: {ex.Message} <AppConfig>");
            }
        }

        return config;
    }

    /// <summary>
    /// Saves configuration to disk with pretty formatting for manual editing.
    /// </summary>
    public static void Save(AppConfig config)
    {
        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Serialize with indentation for readability using source generator
            string json = JsonSerializer.Serialize(config, AppConfigJsonContext.Default.AppConfig);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save config to {ConfigPath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the configuration file path for informational purposes.
    /// </summary>
    public static string GetConfigPath()
    {
        return ConfigPath;
    }
}
