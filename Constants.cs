using System.Reflection;

namespace GpgWinHello;

/// <summary>
/// Constants used throughout the application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Application version. Injected at build time via MSBuild Version property.
    /// </summary>
    public static readonly string Version =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Canonical source repository. Shown by --version and --help so the source is
    /// discoverable from the installed binary alone.
    /// </summary>
    public const string RepositoryUrl = "https://git.lanvise.com/andre/gpg-winhello";

    /// <summary>
    /// Assuan protocol error codes for pinentry.
    /// </summary>
    public static class AssuanErrors
    {
        /// <summary>
        /// GPG_ERR_GENERAL - General error.
        /// </summary>
        public const int GeneralError = 83886179;

        /// <summary>
        /// GPG_ERR_CANCELED - User cancelled operation.
        /// </summary>
        public const int Cancelled = 83886194;
    }

    /// <summary>
    /// Timeouts bounding every operation that could otherwise block gpg-agent forever.
    /// A pinentry that never replies wedges the agent and every GPG/SSH operation
    /// behind it until the agent is reloaded.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>
        /// Seconds before a confirmation dialog auto-cancels.
        /// </summary>
        public const int DialogSeconds = 60;

        /// <summary>
        /// Seconds before a Windows Hello prompt is abandoned.
        /// </summary>
        public const int HelloSeconds = 120;

        /// <summary>
        /// Seconds an advisory (non-blocking) error dialog stays on screen.
        /// </summary>
        public const int ErrorDialogSeconds = 30;

        /// <summary>
        /// Lower bound applied to any configured timeout.
        /// </summary>
        public const int MinimumSeconds = 5;

        /// <summary>
        /// Upper bound applied to any configured timeout.
        /// </summary>
        public const int MaximumSeconds = 600;
    }

    /// <summary>
    /// Cryptographic constants.
    /// </summary>
    public static class Crypto
    {
        /// <summary>
        /// AES-GCM nonce size in bytes (12 bytes is standard for GCM).
        /// </summary>
        public const int NonceSize = 12;

        /// <summary>
        /// AES-GCM authentication tag size in bytes (16 bytes provides 128-bit security).
        /// </summary>
        public const int TagSize = 16;

        /// <summary>
        /// AES-256 key size in bytes.
        /// </summary>
        public const int KeySize = 32;
    }
}
