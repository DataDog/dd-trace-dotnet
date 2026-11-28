// <copyright file="DebugLogReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util.Streams;
#if NETFRAMEWORK
using Datadog.Trace.Vendors.ICSharpCode.SharpZipLib;
using Datadog.Trace.Vendors.ICSharpCode.SharpZipLib.Core;
#endif

namespace Datadog.Trace.Logging.TracerFlare;

internal static class DebugLogReader
{
    public const long MaximumCompressedSizeBytes = 29_000_000; // 31457280 would be 30MiB, we go to 29MB to be safe
    private const string SentinelFilePrefix = "dotnet-tracer-sentinel-";
    private const byte Succeeded = 1;
    private const byte Failed = 2;
    private const byte Pending = 3;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebugLogReader));

    public static FileStream? TryToCreateSentinelFile(string logDirectory, string flareRequestId)
    {
        var claimPath = Path.GetFullPath(GetSentinelPath(logDirectory, flareRequestId));
        if (File.Exists(claimPath))
        {
            return null;
        }

        // Use the sentinel prefix so log retention removes interrupted attempts and archives skip them.
        var temporaryPath = Path.Combine(Path.GetDirectoryName(claimPath)!, $"{SentinelFilePrefix}stage-{Guid.NewGuid():N}.log");
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
        catch (Exception ex)
        {
            // check if the log directory exists, to see if this is an "expected" consequence
            if (Directory.Exists(logDirectory))
            {
                // strange error
                Log.Warning(ex, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}", flareRequestId, logDirectory);
            }
            else
            {
                Log.Warning(ex, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}: Directory does not exist", flareRequestId, logDirectory);
            }

            return null;
        }
        finally
        {
            pendingClaim?.Dispose();
            if (staged)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Log.Debug(exception, "Could not remove unpublished sentinel for {FlareRequestID}", flareRequestId);
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
                                     : File.Open(claimPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

    public static Task WriteDebugLogArchiveToStream(Stream writeTo, string logDirectory)
        => WriteDebugLogArchiveToStream(writeTo, logDirectory, StreamHasCapacity);

    public static async Task WriteDebugLogArchiveToStream(Stream writeTo, string logDirectory, Func<FileInfo, long, bool> streamHasCapacityFunc)
    {
        try
        {
            using var monitoringStream = new WriteCountingStream(writeTo);

#if NETFRAMEWORK
            using var archive = new Vendors.ICSharpCode.SharpZipLib.Zip.ZipOutputStream(monitoringStream);
            archive.IsStreamOwner = false; // Do not close the underlying stream when closing the archive
            archive.SetLevel(9); // Set to optimal compression
#else
            using var archive = new ZipArchive(monitoringStream, ZipArchiveMode.Create, true);
#endif

            // Only sending .log files to avoid the risk of sending files we don't want.
            // Also not recurrsing, in-case they have a weird log setup
            // Grabbing all the filenames once, as we could be creating more in the background, and don't
            // want to run into any modified enumerable issues etc
            var allFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly);
            if (allFiles.Length == 0)
            {
                // no files to upload
                return;
            }

            // we want to upload the most recent files, and leave the older files if we're oversized,
            // so grab the FileInfo for each file, and sort by most recent
            var filesToUpload = new List<FileInfo>();
            foreach (var filePath in allFiles)
            {
                try
                {
                    var filename = Path.GetFileName(filePath);
                    if (filename.StartsWith(SentinelFilePrefix, StringComparison.Ordinal))
                    {
                        // we don't need to send the sentinel files
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);
                    filesToUpload.Add(fileInfo);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error reading file {Filename} when preparing tracer flare zip", filePath);
                }
            }

            // order by most recently modified (note swapped order in compareto)
            filesToUpload.Sort((info1, info2) => info2.LastWriteTimeUtc.CompareTo(info1.LastWriteTimeUtc));

            foreach (var fileDetails in filesToUpload)
            {
                var remainingSize = MaximumCompressedSizeBytes - monitoringStream.Position;
                if (!streamHasCapacityFunc(fileDetails, remainingSize))
                {
                    Log.Warning(
                        "File {FileName} with uncompressed length {Length} may cause compressed size to exceed {MaxFileSize}. Ignoring remaining files",
                        fileDetails.FullName,
                        fileDetails.Length,
                        MaximumCompressedSizeBytes);
                    break;
                }

                try
                {
#if NETFRAMEWORK
                    await AddZipEntryFrameworkAsync(fileDetails, archive).ConfigureAwait(false);
#else
                    await AddZipEntryCoreAsync(fileDetails, archive).ConfigureAwait(false);
#endif
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error recording file {Filename} in tracer flare zip", fileDetails.FullName);
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error creating sanitized debug log stream for tracer flare");
        }
    }

#if NETFRAMEWORK
    internal static async Task AddZipEntryFrameworkAsync(FileInfo fileDetails, Vendors.ICSharpCode.SharpZipLib.Zip.ZipOutputStream archive)
    {
        // Create a fixed-size buffer to bound memory operations
        byte[] buffer = new byte[4096];

        var entry = new Vendors.ICSharpCode.SharpZipLib.Zip.ZipEntry(Path.GetFileName(fileDetails.FullName));
        archive.PutNextEntry(entry);

        // Have to allow FileShare.ReadWrite as the logger will already have it open for writing
        using var file = File.Open(fileDetails.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Using a fixed size buffer here makes no noticeable difference for output
        // but keeps a lid on memory usage.
        int sourceBytes;
        do
        {
            sourceBytes = await file.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            await archive.WriteAsync(buffer, 0, sourceBytes).ConfigureAwait(false);
        }
        while (sourceBytes > 0);
    }
#else
    internal static async Task AddZipEntryCoreAsync(FileInfo fileDetails, ZipArchive archive)
    {
        // There's a SmallestSize for .NET 5+ but it doesn't give much benefit
        // and we would rather not add the extra memory pressure for little gain
        var entry = archive.CreateEntry(fileDetails.Name, CompressionLevel.Optimal);
        using var entryStream = entry.Open();

        // Have to allow FileShare.ReadWrite as the logger will already have it open for writing
        using var file = File.Open(fileDetails.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        await file.CopyToAsync(entryStream).ConfigureAwait(false);
        await entryStream.FlushAsync().ConfigureAwait(false);
    }
#endif

    internal static bool StreamHasCapacity(FileInfo fileDetails, long remainingCapacity)
    {
        // we estimate that the file compresses _roughly_ 10x, so if we're going to exceed that
        // we bail out. That means that we may miss some older smaller files, but at least we
        // have a clear cut off.
        var estimatedFileSize = fileDetails.Length / 10;
        return remainingCapacity >= estimatedFileSize;
    }

    private static string GetSentinelPath(string logDirectory, string flareRequestId)
        => Path.Combine(logDirectory, $"{SentinelFilePrefix}{flareRequestId}.log");

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

    public sealed class WriteCountingStream(Stream innerStream) : LeaveOpenDelegatingStream(innerStream)
    {
        private long _position;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override bool CanSeek => false;

        public override void Write(byte[] buffer, int offset, int count)
        {
            _position += count;
            base.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _position += count;
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            _position += count;
            return base.BeginWrite(buffer, offset, count, callback!, state);
        }

        public override void WriteByte(byte value)
        {
            _position++;
            base.WriteByte(value);
        }
    }
}
