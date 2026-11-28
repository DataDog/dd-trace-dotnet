// <copyright file="FlareClaimStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Datadog.Trace.Logging.TracerFlare;

internal static class FlareClaimStore
{
    private const string SentinelFilePrefix = "dotnet-tracer-sentinel-";
    private const byte Succeeded = 1;
    private const byte Failed = 2;
    private const byte Pending = 3;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FlareClaimStore));

    public static FileStream? TryToCreateSentinelFile(string logDirectory, string flareRequestId)
    {
        var claimPath = Path.GetFullPath(GetSentinelPath(logDirectory, flareRequestId));
        if (File.Exists(claimPath))
        {
            return null;
        }

        // Use the sentinel prefix so log retention removes interrupted attempts and archives skip them.
        var temporaryPath = Path.Combine(Path.GetDirectoryName(claimPath)!, $"{SentinelFilePrefix}stage-{Path.GetRandomFileName()}.log");
        var staged = false;
        FileStream? pendingClaim = null;
        try
        {
            if (Directory.Exists(claimPath))
            {
                return TryCreateDirectoryClaim(claimPath);
            }

            // Publish an initialized file without replacing a claim from an older tracer.
            pendingClaim = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
            staged = true;
            pendingClaim.WriteByte(Pending);
            pendingClaim.Flush();
            if (TryRenameClaim(temporaryPath, claimPath))
            {
                staged = false;
            }
            else if (File.Exists(claimPath))
            {
                return null;
            }
            else if (!TryLinkClaim(temporaryPath, claimPath))
            {
                return TryCreateDirectoryClaim(claimPath);
            }

            var ownedClaim = pendingClaim;
            pendingClaim = null;
            return ownedClaim;
        }
        catch (IOException) when (File.Exists(claimPath))
        {
            // expected, the file already exists
            Log.Information("Sentinel file for config {FlareRequestID} found in {LogDirectory}", flareRequestId, logDirectory);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (Directory.Exists(logDirectory))
            {
                Log.Warning(error, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}", flareRequestId, logDirectory);
            }
            else
            {
                Log.Warning(error, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}: Directory does not exist", flareRequestId, logDirectory);
            }

            return null;
        }
        catch (Exception error)
        {
            Log.Error(error, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}", flareRequestId, logDirectory);
            return null;
        }
        finally
        {
            try
            {
                pendingClaim?.Dispose();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Log.Warning(error, "Error closing unpublished sentinel for config {FlareRequestID} in {LogDirectory}", flareRequestId, logDirectory);
            }
            finally
            {
                if (staged)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        Log.Debug(error, "Could not remove unpublished sentinel for {FlareRequestID}", flareRequestId);
                    }
                }
            }
        }
    }

    public static void RecordSentinelResult(FileStream sentinel, bool succeeded)
    {
        sentinel.Position = 0;
        sentinel.WriteByte(succeeded ? Succeeded : Failed);
        sentinel.Flush();
    }

    public static async Task<bool?> WaitForSentinelResult(string logDirectory, string flareRequestId, TimeSpan timeout)
    {
        var claimPath = Path.GetFullPath(GetSentinelPath(logDirectory, flareRequestId));
        var directoryClaim = Directory.Exists(claimPath);
        using var directory = directoryClaim ? LinuxFlareClaimDirectory.TryOpen(claimPath) : null;
        if (directoryClaim && directory is null)
        {
            return null;
        }

        Stopwatch? waiting = null;
        while (true)
        {
            try
            {
                using var file = directoryClaim
                                     ? directory!.TryOpenReceipt()
                                     : TryOpenFileReceipt(claimPath);
                if (file is null)
                {
                    return null;
                }

                var status = file.ReadByte();
                switch (status)
                {
                    case Succeeded:
                        return true;
                    case Failed:
                        return false;
                    case -1 when !directoryClaim:
                        return true;
                    case -1:
                    case Pending:
                        break;
                    default:
                        return null;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (exception is not IOException || exception is FileNotFoundException || (!directoryClaim && !File.Exists(claimPath)))
                {
                    return null;
                }
            }

            waiting ??= Stopwatch.StartNew();
            if (waiting.Elapsed >= timeout)
            {
                return null;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }
    }

    internal static void CleanExpiredClaimDirectories(string logDirectory, DateTime cutoff)
    {
        if (!LinuxFlareClaimDirectory.IsSupported)
        {
            return;
        }

        var cutoffSeconds = new DateTimeOffset(cutoff).ToUnixTimeSeconds();
        foreach (var claim in Directory.EnumerateDirectories(logDirectory, $"{SentinelFilePrefix}*.log"))
        {
            using var directory = LinuxFlareClaimDirectory.TryOpen(Path.GetFullPath(claim));
            directory?.CleanIfExpired(cutoffSeconds);
        }
    }

    internal static bool IsClaimFileName(string fileName) => fileName.StartsWith(SentinelFilePrefix, StringComparison.Ordinal);

    private static string GetSentinelPath(string logDirectory, string flareRequestId)
        => Path.Combine(logDirectory, $"{SentinelFilePrefix}{flareRequestId}.log");

    private static FileStream? TryOpenFileReceipt(string claimPath)
        => LinuxFlareClaimDirectory.IsSupported
               ? LinuxFlareClaimDirectory.TryOpenFileReceipt(claimPath)
               : File.Open(claimPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    private static FileStream? TryCreateDirectoryClaim(string claimPath)
    {
        using var directory = LinuxFlareClaimDirectory.TryOpen(claimPath, create: true);
        return directory?.TryOpenReceipt(create: true);
    }

    private static bool TryRenameClaim(string temporaryPath, string claimPath)
    {
        var framework = FrameworkDescription.Instance;
        if (framework.IsWindows())
        {
            File.Move(temporaryPath, claimPath);
            return true;
        }

        try
        {
            if (framework.OSPlatform == OSPlatformName.MacOS)
            {
                const uint RenameExclusive = 4;
                return RenameMac(temporaryPath, claimPath, RenameExclusive) == 0;
            }

            if (framework.OSPlatform == OSPlatformName.Linux)
            {
                // The libc wrapper is unavailable on some supported Linux runtimes.
                var renameAt2 = framework.ProcessArchitecture switch
                {
                    ProcessArchitecture.X64 => 316,
                    ProcessArchitecture.Arm64 => 276,
                    _ => 0,
                };
                const int CurrentDirectory = -100;
                const uint RenameNoReplace = 1;
                return renameAt2 != 0 && RenameLinux(renameAt2, CurrentDirectory, temporaryPath, CurrentDirectory, claimPath, RenameNoReplace) == 0;
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Debug(exception, "Could not load the operating system's exclusive rename function for tracer flare");
        }

        return false;
    }

    private static bool TryLinkClaim(string temporaryPath, string claimPath)
    {
        try
        {
            return Link(temporaryPath, claimPath) == 0;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Debug(exception, "Could not load the operating system's link function for tracer flare");
            return false;
        }
    }

    [DllImport("libc", EntryPoint = "link", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int Link(string existingPath, string newPath);

    [DllImport("libc", EntryPoint = "renamex_np", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int RenameMac(string existingPath, string newPath, uint flags);

    [DllImport("libc", EntryPoint = "syscall", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern nint RenameLinux(nint syscall, nint existingDirectory, string existingPath, nint newDirectory, string newPath, nuint flags);
}
