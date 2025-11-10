# Configuration Guide

gpg-winhello is configured via a JSON file located at:

```
%APPDATA%\gpg-winhello\config.json
```

## Default Configuration

If no configuration file exists, the following defaults are used:

```json
{
  "Version": 1,
  "InfoDialog": {
    "Enabled": true
  },
  "Logging": {
    "Enabled": false,
    "Path": "%APPDATA%\\gpg-winhello\\prompt.log"
  }
}
```

## Configuration Schema

### Version

**Type:** `integer`  
**Default:** `1`  
**Description:** Configuration schema version. Used for future migrations.

### InfoDialog

Controls the informational dialog shown before Windows Hello authentication.

#### InfoDialog.Enabled

**Type:** `boolean`  
**Default:** `true`  
**Description:** Whether to show an informational dialog before Windows Hello authentication. The dialog displays what credential GPG is requesting (e.g., "Please unlock the card") and allows the user to cancel before the biometric prompt.

**Recommendation:** Keep enabled. This helps you know WHAT you're unlocking before the fingerprint prompt and provides a cancel option.

**Example:**
```json
{
  "InfoDialog": {
    "Enabled": true
  }
}
```

When enabled, you'll see a Windows MessageBox like:
```
Please unlock the card

Click OK to authenticate with Windows Hello.

[OK] [Cancel]
```

### Logging

Controls logging of credential requests for troubleshooting.

#### Logging.Enabled

**Type:** `boolean`
**Default:** `false`
**Description:** Whether to log credential requests to the log file. Logs include timestamp, detected credential type, and the GPG description.

**Recommendation:** Enable only when troubleshooting credential detection issues.

**Example log entry:**
```
[2025-01-10 15:23:45] Type=pin | Description=Please unlock the card
[2025-01-10 15:24:12] Type=passphrase | Description=Please enter the passphrase for the ssh key...
```

#### Logging.Path

**Type:** `string`  
**Default:** `%APPDATA%\gpg-winhello\prompt.log`  
**Description:** Path to the log file. Supports environment variables.

**Note:** The path must be an absolute path. Environment variables like `%APPDATA%` are expanded automatically.

**Examples:**
```json
{
  "Logging": {
    "Path": "%APPDATA%\\gpg-winhello\\prompt.log"
  }
}
```

```json
{
  "Logging": {
    "Path": "C:\\Logs\\gpg-winhello.log"
  }
}
```

## Complete Example

A fully customized configuration:

```json
{
  "Version": 1,
  "InfoDialog": {
    "Enabled": false
  },
  "Logging": {
    "Enabled": true,
    "Path": "C:\\Logs\\gpg-requests.log"
  }
}
```

This configuration:
- Skips the info dialog (goes straight to Windows Hello)
- Enables logging to a custom path

## Editing the Configuration

1. Open `%APPDATA%\gpg-winhello\config.json` in a text editor
2. Make your changes
3. Save the file
4. Changes take effect immediately (no restart needed)

## Troubleshooting

### Configuration not loading

If your configuration doesn't seem to apply:

1. Check JSON syntax is valid (use a JSON validator)
2. Ensure file is saved as UTF-8
3. Check logs in stderr for parsing errors: `gpg --verbose --card-status 2>&1 | findstr "config"`

### Resetting to defaults

Delete the config file and it will be recreated with defaults on next use:

```cmd
del %APPDATA%\gpg-winhello\config.json
```

### Log file growing large

If the log file becomes too large, you can:

1. Disable logging: `"Enabled": false`
2. Move the log file to a different location
3. Manually delete or truncate the log file

The log file is append-only and has no automatic rotation. Consider periodic cleanup if logging is enabled long-term.

## Security Considerations

### Info Dialog

The info dialog shows the GPG description which may contain:
- Key identifiers
- SSH key names
- Card serial numbers

This information is visible to anyone with physical access to your screen. If this is a concern, disable the info dialog.

### Log File

The log file contains timestamps and descriptions of credential requests, which could reveal:
- When you use GPG/SSH keys
- Which keys you access
- Key identifiers

The log file is protected with user-only permissions, but consider:
- Disabling logging if not needed for troubleshooting
- Using a secure location for the log path
- Periodically clearing the log file

### Configuration File

The configuration file itself contains no secrets and can be safely shared.
