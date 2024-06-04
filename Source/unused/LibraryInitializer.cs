using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AICore.Loader;

public class LibraryInitializer
{
    static LibraryInitializer()
    {
#if DEBUG
        Debug.Log("LibraryInitializer: Static constructor called");
#endif
        LoadUnmanagedLibraries();
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
#if DEBUG
        Debug.Log("LibraryInitializer: Assembly loaded: " + args.LoadedAssembly.GetName().Name);
#endif
        // Ensure unmanaged libraries are loaded
    }

    private static void LoadUnmanagedLibraries()
    {
#if DEBUG
        Debug.Log("LoadUnmanagedLibraries: Start");
#endif

        string platformName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "Linux"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "OSX"
                    : throw new PlatformNotSupportedException("Unsupported OS platform.");

        string archName =
            RuntimeInformation.OSArchitecture == Architecture.X64
                ? "x64"
                : RuntimeInformation.OSArchitecture == Architecture.X86
                    ? "x86"
                    : RuntimeInformation.OSArchitecture == Architecture.Arm64
                        ? "arm64"
                        : throw new PlatformNotSupportedException("Unsupported architecture.");

        string libraryName = platformName switch
        {
            "Windows" when archName == "x64" => "grpc_csharp_ext.x64.dll",
            "Windows" when archName == "x86" => "grpc_csharp_ext.x86.dll",
            "Linux" when archName == "x64" => "libgrpc_csharp_ext.x64.so",
            "OSX" when archName == "arm64" => "libgrpc_csharp_ext.arm64.dylib",
            "OSX" when archName == "x64" => "libgrpc_csharp_ext.x64.dylib",
            _ => throw new NotSupportedException("Unsupported OS and Architecture combination.")
        };

        string libraryPath = Path.Combine(
            Path.GetFullPath(
                Path.Combine(Assembly.GetExecutingAssembly().Location, @"..\..\..\Libraries\")
            ),
            libraryName
        );

#if DEBUG
        Debug.Log("LoadUnmanagedLibraries: Library path determined: " + libraryPath);
#endif

        if (!File.Exists(libraryPath))
        {
#if DEBUG
            Debug.LogError("LoadUnmanagedLibraries: Library file not found: " + libraryPath);
#endif
            throw new FileNotFoundException($"Library file not found: {libraryPath}");
        }

        LoadLibrary(libraryPath);

#if DEBUG
        Debug.Log("LoadUnmanagedLibraries: Library loaded: " + libraryPath);
#endif
    }

    private static void LoadLibrary(string libraryPath)
    {
#if DEBUG
        Debug.Log("LoadLibrary: Loading library: " + libraryPath);
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (LoadLibraryWindows(libraryPath) == IntPtr.Zero)
            {
#if DEBUG
                Debug.LogError("LoadLibrary: Failed to load library on Windows: " + libraryPath);
#endif
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        )
        {
            const int RTLD_NOW = 2;
            if (dlopen(libraryPath, RTLD_NOW) == IntPtr.Zero)
            {
#if DEBUG
                Debug.LogError("LoadLibrary: Failed to load library on Linux/OSX: " + libraryPath);
#endif
                throw new Exception($"Unable to load library: {libraryPath}");
            }
        }

#if DEBUG
        Debug.Log("LoadLibrary: Library successfully loaded: " + libraryPath);
#endif
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibraryWindows(string lpFileName);

    [DllImport("libdl")]
    private static extern IntPtr dlopen(string fileName, int flags);
}
