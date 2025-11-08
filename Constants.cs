namespace GpgWinHello;

/// <summary>
/// Constants used throughout the application.
/// </summary>
public static class Constants
{
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

    /// <summary>
    /// Storage format constants.
    /// </summary>
    public static class Storage
    {
        /// <summary>
        /// Current version of the encrypted passphrase file format.
        /// Version 1: [1 byte version][12 bytes nonce][16 bytes tag][encrypted data]
        /// </summary>
        public const byte CurrentVersion = 1;
    }
}
