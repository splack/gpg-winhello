# GPG Windows Hello

Unlock your GPG SSH agent using Windows Hello fingerprint authentication.

## Features

- **True biometric unlock**: Uses Windows Hello API for fingerprint
  authentication
- **Secure storage**: Passphrase encrypted with TPM-backed Windows Hello keys
- **GPG compatible**: Implements the pinentry protocol for seamless integration

## How It Works

1. **Setup**: Encrypts your GPG passphrase using Windows Hello (RSA-2048 + TPM)
2. **Usage**: Each SSH/GPG operation triggers fingerprint scan to decrypt
   passphrase
3. **Security**: Private key never leaves TPM; passphrase encrypted at rest

## Requirements

- Windows 11 (or Windows 10 with Windows Hello)
- GPG4Win or GnuPG for Windows
- Fingerprint reader configured in Windows Hello

## Installation

See [Building](#building) for how to build the executable.

### 1. Initial Setup

**Run setup mode**:

```powershell
.\gpg-winhello.exe setup
```

This will:

1. Create a Windows Hello key (prompts for fingerprint)
2. Ask for your GPG passphrase (twice for confirmation)
3. Encrypt and save the passphrase to `%APPDATA%\gpg-winhello\passphrase.enc`

### 2. Configure GPG

**Copy executable to a permanent location**:

```powershell
# Example: Copy to GPG installation directory
Copy-Item gpg-winhello.exe "C:\Program Files (x86)\GnuPG\bin\"
```

**Edit `gpg-agent.conf`**:

Location: `%APPDATA%\gnupg\gpg-agent.conf`

Add this line:

```
pinentry-program C:\Program Files (x86)\GnuPG\bin\gpg-winhello.exe
```

**Restart gpg-agent**:

```powershell
gpg-connect-agent killagent /bye
gpg-connect-agent /bye
```

### 3. Test

Try using SSH with your GPG authentication key:

```powershell
ssh git@github.com
```

You should be prompted for fingerprint authentication via Windows Hello.

## Security Considerations

### What This Protects Against

- **Passphrase theft**: Passphrase encrypted at rest, never stored in plaintext
- **Unauthorized access**: Requires biometric authentication to decrypt
- **Session hijacking**: Each operation requires fresh biometric auth

### What This Does NOT Protect Against

- **Malware with your user privileges**: Can call the same APIs after you've
  unlocked
- **Physical access while unlocked**: Windows session compromise = key
  compromise
- **Keyloggers**: Not applicable (no keyboard entry for biometric unlock)

## Troubleshooting

### "Windows Hello is not supported"

- Check: Settings > Accounts > Sign-in options > Windows Hello
- Ensure fingerprint reader is configured
- Verify device supports TPM 2.0

### "Failed to create Windows Hello key"

- May need to authenticate first time with PIN + fingerprint
- Check Windows Hello is fully set up
- Try logging out and back in

### "Not configured" error

- Run `gpg-winhello.exe setup` first
- Check `%APPDATA%\gpg-winhello\passphrase.enc` exists

### "Decryption failed"

- Your Windows Hello key may have changed
- Re-run `gpg-winhello.exe setup` to re-encrypt
- Check Windows Hello settings haven't been reset

### GPG agent not using pinentry

- Verify `gpg-agent.conf` has correct path
- Restart gpg-agent: `gpg-connect-agent killagent /bye`
- Check gpg-winhello.exe has execute permissions

## Advanced Configuration

### Cache TTL

While Windows Hello provides unlock, you may still want caching:

`%APPDATA%\gnupg\gpg-agent.conf`:

```
default-cache-ttl 3600
max-cache-ttl 7200
```

This caches for 1 hour (avoids repeated fingerprint scans).

### Logging

Pinentry errors are written to stderr. To debug:

```powershell
# In gpg-agent.conf
log-file C:\temp\gpg-agent.log
debug-level advanced
```

### Multiple Passphrases

Currently supports one GPG key passphrase. For multiple keys:

- Use same passphrase for all keys (less secure)
- Or run setup multiple times with different storage paths (modify source)

## Technical Details

### Encryption

- **Algorithm**: AES-256-CBC
- **Key derivation**: SHA-256 hash of Windows Hello RSA signature
- **Challenge**: Fixed string "gpg-winhello-deterministic-challenge-v1"
- **Signature**: RSA-2048-PKCS1-SHA256 (Windows Hello default)

### Storage

- **Location**: `%APPDATA%\gpg-winhello\passphrase.enc`
- **Format**: 16-byte IV + AES-encrypted passphrase
- **Permissions**: User-only access (Windows ACLs)

### Pinentry Protocol

Implements Assuan protocol:

- Commands: GETPIN, SETDESC, SETPROMPT, BYE, etc.
- Response: `D <url-encoded-passphrase>` + `OK`
- Error codes: Standard pinentry errors (e.g., 83886194 = cancelled)

## Development

### Project Structure

```
gpg-winhello/
├── *.cs, *.csproj          # C# source files
├── flake.nix               # Flakelight-based Nix flake
└── nix/
    ├── dotnet-version.nix  # Shared .NET SDK version extraction
    └── packages/
        ├── _default.nix    # Main package definition
        └── deps.json       # NuGet dependencies lockfile
```

The project uses [flakelight](https://github.com/nix-community/flakelight) for a
simplified Nix flake structure with automatic package discovery and built-in
formatting checks.

### Building

**With Nix (cross-compile from Linux)**:

```bash
nix build
# Output: result/lib/gpg-winhello/gpg-winhello.exe
```

**With dotnet (Windows)**:

```powershell
# Debug build
dotnet build

# Release build (self-contained)
dotnet publish -c Release -r win-x64 --self-contained
```

### Development Shell

Enter a development environment with all tools:

```bash
nix develop
# Provides: dotnet SDK 9.0, csharp-ls, direnv, nixfmt
```

### Testing

```powershell
# Test setup mode
.\gpg-winhello.exe setup

# Test pinentry mode (manual protocol)
.\gpg-winhello.exe
# Type: GETPIN
# Should trigger Windows Hello and return encrypted passphrase
```

### Updating Dependencies (Nix)

When upgrading .NET version or adding NuGet packages, update
`nix/packages/deps.json`:

```bash
nix run .#update-deps
```

This uses buildDotnetModule's built-in dependency fetcher to generate the
lockfile.

## License

MIT License - feel free to modify and distribute.

## Contributing

Issues and PRs welcome! Areas for improvement:

- Support for multiple GPG keys
- GUI for setup instead of CLI
- Fallback to cached passphrase on Hello failure
- Integration tests with actual GPG agent

## Credits

Built using:

- Windows.Security.Credentials.KeyCredentialManager API
- .NET 9.0
- pinentry protocol specification
- Nix buildDotnetModule for cross-compilation
- [flakelight](https://github.com/nix-community/flakelight) for simplified Nix
  flakes

## See Also

- [pinentry documentation](https://www.gnupg.org/related_software/pinentry/index.html)
- [Windows Hello documentation](https://learn.microsoft.com/en-us/windows/security/identity-protection/hello-for-business/)
- [GPG Agent documentation](https://www.gnupg.org/documentation/manuals/gnupg/Invoking-GPG_002dAGENT.html)
