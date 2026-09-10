using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GpgWinHello;

/// <summary>
/// Windows dialog helpers that never block the Assuan protocol indefinitely.
///
/// A pinentry is spawned by gpg-agent as a background child with stdio pipes and no
/// visible window. An unbounded modal dialog raised from here is frequently invisible
/// or unfocusable, and blocks gpg-agent forever - wedging every subsequent GPG and SSH
/// operation until the agent is reloaded. Every dialog therefore gets a timeout and is
/// forced to the foreground.
/// </summary>
public static class DialogHelper
{
    public const uint MB_OK = 0x00000000;
    public const uint MB_OKCANCEL = 0x00000001;
    public const uint MB_ICONINFORMATION = 0x00000040;
    public const uint MB_ICONWARNING = 0x00000030;

    // Force the dialog somewhere the user can actually see and dismiss it.
    private const uint MB_SYSTEMMODAL = 0x00001000;
    private const uint MB_SETFOREGROUND = 0x00010000;
    private const uint MB_TOPMOST = 0x00040000;
    private const uint ForceFlags = MB_SYSTEMMODAL | MB_SETFOREGROUND | MB_TOPMOST;

    public const int IDOK = 1;
    public const int IDCANCEL = 2;

    /// <summary>Returned when the dialog auto-dismissed without a user answer.</summary>
    public const int IDTIMEOUT = 32000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    // Undocumented but present on every supported Windows release since XP.
    [DllImport("user32.dll", EntryPoint = "MessageBoxTimeoutW", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxTimeout(
        IntPtr hWnd,
        string text,
        string caption,
        uint type,
        short wLanguageId,
        int dwMilliseconds
    );

    /// <summary>
    /// Shows a modal dialog that auto-dismisses after <paramref name="timeoutSeconds"/>.
    /// Returns IDOK, IDCANCEL, or IDTIMEOUT. Never blocks indefinitely.
    /// </summary>
    public static int ShowWithTimeout(string message, string caption, uint type, int timeoutSeconds)
    {
        int timeoutMs = ClampToMilliseconds(timeoutSeconds);

        try
        {
            return MessageBoxTimeout(
                IntPtr.Zero,
                message,
                caption,
                type | ForceFlags,
                0,
                timeoutMs
            );
        }
        catch (EntryPointNotFoundException)
        {
            // MessageBoxTimeoutW is undocumented; fall back to a thread we can abandon.
            return ShowOnAbandonableThread(message, caption, type, timeoutMs);
        }
        catch (DllNotFoundException)
        {
            // No UI available at all - treat as no answer rather than hanging.
            return IDTIMEOUT;
        }
    }

    /// <summary>
    /// Shows an advisory dialog without blocking the caller at all.
    /// Used on paths where gpg-agent has already been given its reply.
    /// </summary>
    public static void ShowNonBlocking(string message, string caption, int timeoutSeconds)
    {
        StartDialogThread(() =>
            ShowWithTimeout(message, caption, MB_OK | MB_ICONWARNING, timeoutSeconds)
        );
    }

    /// <summary>
    /// Runs MessageBox on a background thread and abandons it if it outlives the timeout.
    /// The thread is a background thread, so an orphaned dialog can never keep the
    /// process alive after the pinentry session ends.
    /// </summary>
    private static int ShowOnAbandonableThread(
        string message,
        string caption,
        uint type,
        int timeoutMs
    )
    {
        int result = IDTIMEOUT;
        using var done = new ManualResetEventSlim(false);

        StartDialogThread(() =>
        {
            try
            {
                result = MessageBox(IntPtr.Zero, message, caption, type | ForceFlags);
            }
            catch
            {
                result = IDTIMEOUT;
            }
            finally
            {
                done.Set();
            }
        });

        // Wait/Set establishes the happens-before needed to read `result` safely.
        return done.Wait(timeoutMs) ? result : IDTIMEOUT;
    }

    private static void StartDialogThread(Action body)
    {
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch
            {
                // Dialogs are best-effort; never propagate onto a background thread.
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static int ClampToMilliseconds(int timeoutSeconds)
    {
        if (timeoutSeconds < Constants.Timeouts.MinimumSeconds)
        {
            timeoutSeconds = Constants.Timeouts.MinimumSeconds;
        }
        else if (timeoutSeconds > Constants.Timeouts.MaximumSeconds)
        {
            timeoutSeconds = Constants.Timeouts.MaximumSeconds;
        }

        return timeoutSeconds * 1000;
    }
}
