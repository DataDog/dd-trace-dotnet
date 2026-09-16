using System;
using System.IO;
#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.InteropServices;
#endif
using Confluent.Kafka;

namespace Samples.Kafka.LibraryMismatch;

internal static class NativeLibraryLoader
{
    public static void LoadAndVerify(string nativeVersion)
    {
        var expectedVersion = nativeVersion switch
        {
            "1.6.1" => 0x010601ff,
            "2.2.0" => 0x020200ff,
            "2.8.0" => 0x020800ff,
            _ => throw new ArgumentException("Unexpected native version", nameof(nativeVersion)),
        };

        // 2.8.0 uses the native library shipped with the managed Confluent.Kafka package.
        if (nativeVersion != "2.8.0")
        {
            LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, $"librdkafka-{nativeVersion}"));
        }

        var actualVersion = Library.Version;
        if (actualVersion != expectedVersion)
        {
            throw new InvalidOperationException($"Expected native version {expectedVersion:x8}, loaded {actualVersion:x8}");
        }

        Console.WriteLine($"Native version: {Library.VersionString}");
    }

    private static void LoadFromDirectory(string directory)
    {
        var libraryName = "librdkafka.dll";
#if NETCOREAPP3_1_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            libraryName = File.Exists("/etc/alpine-release") ? "alpine-librdkafka.so" : "centos6-librdkafka.so";
        }
#endif
        var libraryPath = Path.Combine(directory, libraryName);
#if NETCOREAPP3_1_OR_GREATER
        // Library.Load(path) alone does not override .NET's resolution of Confluent's P/Invokes.
        // Keep the handle alive for the process, including native background threads and cleanup.
        var loadFlags = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories
                            : (DllImportSearchPath?)null;
        var nativeHandle = NativeLibrary.Load(libraryPath, typeof(Library).Assembly, loadFlags);
        NativeLibrary.SetDllImportResolver(typeof(Library).Assembly, (name, _, _) =>
            name is "librdkafka" or "alpine-librdkafka" ? nativeHandle : IntPtr.Zero);
        // The resolver also handles Confluent's Alpine binding, without requiring its explicit-path libdl loader.
        Library.Load();
#else
        Library.Load(libraryPath);
#endif
    }
}
