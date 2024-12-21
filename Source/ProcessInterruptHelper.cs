using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AICore;

public static class ProcessTerminationHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    private const uint STILL_ACTIVE = 259;
    private const uint PROCESS_TERMINATED = 1;

    // Unix specific
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);

    private const int SIGTERM = 15;  // Standard termination signal
    private const int SIGKILL = 9;   // Forceful termination signal
    private const int TERM_TIMEOUT_MS = 250;  // 0.25 seconds for SIGTERM
    private const int KILL_TIMEOUT_MS = 100;  // 0.1 second for SIGKILL

    public static void ForceKillProcess(Process process)
    {
        try
        {
            if (process.HasExited) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                uint exitCode;
                if (GetExitCodeProcess(process.Handle, out exitCode) && exitCode == STILL_ACTIVE)
                {
                    if (!TerminateProcess(process.Handle, PROCESS_TERMINATED))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                     || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
#if DEBUG
                LogTool.Debug($"Beginning termination of process {process.Id}");
#endif

                // First try SIGTERM just to the specific process
                int result = kill(process.Id, SIGTERM);
#if DEBUG
                if (result != 0)
                {
                    LogTool.Debug($"SIGTERM failed for process {process.Id}: {Marshal.GetLastWin32Error()}");
                }
#endif

                if (!process.WaitForExit(TERM_TIMEOUT_MS))
                {
                    LogTool.Warning("Process still alive after SIGTERM, using SIGKILL");
                    result = kill(process.Id, SIGKILL);
#if DEBUG
                    if (result != 0)
                    {
                        LogTool.Debug($"SIGKILL failed for process {process.Id}: {Marshal.GetLastWin32Error()}");
                    }
#endif

                    if (!process.WaitForExit(KILL_TIMEOUT_MS))
                    {
                        LogTool.Warning($"Process {process.Id} survived SIGKILL!");
                    }
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform for process termination");
            }
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error in process termination: {ex.Message}");
        }
    }
}

public static class ProcessInterruptHelper
{
    public static bool SendSigINT(Process process)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsProcessHelper.SendCtrlC(process);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                     || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return UnixProcessHelper.SendSigINT(process);
            else
                throw new PlatformNotSupportedException("Unsupported platform");
        }
        catch (Exception ex)
        {
            LogTool.Error($"Failed to send interrupt signal: {ex}");
            return false;
        }
    }
}

public static class UnixProcessHelper
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);
    private const int SIGINT = 2;

    public static bool SendSigINT(Process process)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("This method is for Unix-like systems only");

        // Send to process group to ensure child processes are signaled
        int result = kill(-process.Id, SIGINT);
        if (result != 0)
        {
            // If process group send fails, try direct process
            result = kill(process.Id, SIGINT);
        }
        return result == 0;
    }
}

public static class WindowsProcessHelper
{
    private const int CTRL_C_EVENT = 0;

    // private const int CTRL_BREAK_EVENT = 1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handlerRoutine, bool add);

    private delegate bool ConsoleCtrlDelegate(uint ctrlType);

    public static bool SendCtrlC(Process process)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("This method is for Windows systems only");

        bool attached = false;
        try
        {
            // Attempt to attach to the process's console
            if (!AttachConsole((uint)process.Id))
            {
                return false;
            }
            attached = true;

            // Disable Ctrl+C handling for our process
            if (!SetConsoleCtrlHandler(null, true))
            {
                return false;
            }

            // Send Ctrl+C
            if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
            {
                return false;
            }

            return true;
        } finally
        {
            if (attached)
            {
                FreeConsole();
                SetConsoleCtrlHandler(null, false);
            }
        }
    }
}
