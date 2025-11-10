# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.1] - 2025-11-11

### Added

- Informational MessageBox dialog shown before Windows Hello authentication prompt
- JSON configuration system with auto-creation on first use
- Configuration file at `%APPDATA%\gpg-winhello\config.json` for customization
- `InfoDialog.Enabled` config option to show/hide pre-authentication info dialog (default: enabled)
- `Logging.Enabled` config option to enable/disable credential request logging (default: disabled)
- `Logging.Path` config option to customize log file location
- Version command (`--version`, `-v`) showing application and runtime version
- Help command (`--help`, `-h`, `/?`) with usage information
- `install` command for automated installation to user directory
- `config` command for automated GPG agent configuration
- `CONFIG.md` documentation with configuration examples and troubleshooting
- Configuration schema versioning for future migrations

### Changed

- Renamed `setup` command to `enroll` for clarity
- Config loading moved to application startup instead of lazy loading
- Version number now injected from Nix build (`-p:Version=${version}`)
- Version constant now reads from assembly metadata instead of hardcoded value
- Logging disabled by default to avoid clutter (enable for troubleshooting)
- Info dialog shows GPG description (e.g., "Please unlock the card") with OK/Cancel buttons
- Improved README with configuration section and updated technical details

### Fixed

- Config file not being auto-created on first run - now creates at startup
- Missing CONFIRM command implementation for pinentry protocol
- Outdated README showing AES-CBC instead of correct AES-GCM encryption
- Outdated storage format documentation (now correctly shows version/nonce/tag/data)

### Removed

- Removed UserConsentVerifier authentication (was causing double authentication)
- Removed config prompts from enrollment command (config now separate concern)

## [0.2.0] - 2025-01-08

### Security

- **CRITICAL**: Replaced AES-CBC with AES-256-GCM authenticated encryption to prevent padding oracle attacks and ciphertext tampering
- **CRITICAL**: Implemented explicit memory clearing for passphrases and encryption keys to prevent memory dump attacks
- **CRITICAL**: Added comprehensive input validation for encrypted data to prevent crashes and vulnerabilities
- Added version header to encrypted passphrase file format for future-proof format changes
- Implemented constant-time passphrase comparison to prevent timing attacks
- Added UTF-8 validation for decrypted passphrases
- Generic error messages to avoid information leakage to attackers

### Changed

- Passphrases now stored in `char[]` instead of immutable `string` objects for secure memory clearing
- Encrypted file format changed to: `[1 byte version][12 bytes nonce][16 bytes tag][encrypted data]`
- Improved error handling with specific exception types (`CryptographicException`, `InvalidOperationException`, `IOException`)
- Replaced LINQ array operations with `System.Buffer.BlockCopy()` for better performance
- Atomic file writes using temporary file then move for `passphrase.enc`
- Added `ConfigureAwait(false)` to all async calls for better async/await performance

### Added

- `Constants.cs` with centralized error codes, crypto constants, and storage version
- Comprehensive XML documentation comments for all public methods
- Detailed security model documentation in code comments
- Version validation when loading encrypted passphrase files
- Better error messages with troubleshooting guidance

### Fixed

- Buffer ambiguity between `Windows.Storage.Streams.Buffer` and `System.Buffer`
- Windows Runtime async operations now use `.AsTask().ConfigureAwait(false)` pattern
- Missing validation for minimum encrypted data length

## [0.1.2] - 2024-01-XX

### Changed

- Bumped version to 0.1.2
- Fixed immutable release handling in CI

### Added

- Magic Nix Cache for faster CI builds

## [0.1.1] - 2024-01-XX

### Added

- GitHub Actions CI with automated builds
- Contents write permission for releases
- Automated release creation on version tags

## [0.1.0] - 2024-01-XX

### Added

- Initial release: GPG Windows Hello biometric authentication
- Windows Hello fingerprint authentication for GPG passphrases
- AES-256 encryption with TPM-backed Windows Hello keys
- Pinentry protocol implementation for GPG agent integration
- Cross-compilation support from Linux to Windows via Nix
- Flakelight-based Nix flake for simplified development
- Self-contained, single-file Windows executable
- Setup mode for initial passphrase configuration
- Secure storage in `%APPDATA%\gpg-winhello\passphrase.enc`
- User-only ACL permissions on encrypted passphrase file

[Unreleased]: https://github.com/splack/gpg-winhello/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/splack/gpg-winhello/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/splack/gpg-winhello/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/splack/gpg-winhello/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/splack/gpg-winhello/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/splack/gpg-winhello/releases/tag/v0.1.0
