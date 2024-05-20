namespace AICore;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ProcessHelper
{
    public static void SendSigINT(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsProcessHelper.SendCtrlC(process);
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        )
        {
            UnixProcessHelper.SendSigINT(process);
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }
}

public class UnixProcessHelper
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);

    private const int SIGINT = 2;

    public static void SendSigINT(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new InvalidOperationException("This method is for Unix-like systems only.");
        }

        kill(process.Id, SIGINT);
    }
}

public class WindowsProcessHelper
{
    private const int CTRL_C_EVENT = 0;
    private const int CTRL_BREAK_EVENT = 1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add);

    private delegate bool ConsoleCtrlDelegate(uint ctrlType);

    public static void SendCtrlC(Process process)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new InvalidOperationException("This method is for Windows systems only.");
        }

        AttachConsole((uint)process.Id);
        SetConsoleCtrlHandler(null, true); // Disable Ctrl+C handling in the current process

        GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);

        FreeConsole();
        SetConsoleCtrlHandler(null, false); // Re-enable Ctrl+C handling in the current process
    }
}
