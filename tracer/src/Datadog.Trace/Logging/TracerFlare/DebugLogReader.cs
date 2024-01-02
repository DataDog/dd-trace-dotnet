// <copyright file="DebugLogReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util.Streams;

namespace Datadog.Trace.Logging.TracerFlare;

internal static class DebugLogReader
{
    public const long MaximumCompressedSizeBytes = 29_000_000; // 31457280 would be 30MiB, we go to 29MB to be safe
    private const string SentinelFilePrefix = "dotnet-tracer-sentinel-";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebugLogReader));

    public static bool TryToCreateSentinelFile(string logDirectory, string flareRequestId)
    {
        // Assuming the log directory is accessible and writeable
        // If it isn't this will return false anyway, but we do an extra check, just to be sure
        try
        {
            // Using the dotnet-tracer-sentinel-*.log pattern means it gets automatically cleaned up by the log cleaning task
            var filePath = Path.Combine(logDirectory, $"{SentinelFilePrefix}{flareRequestId}.log");
            using var file = File.Open(filePath, FileMode.CreateNew, FileAccess.Write);

            // if we got this far, the file was created
            return true;
        }
        catch (IOException)
        {
            // expected, the file already exists
            Log.Information("Sentinel file for config {FlareRequestID} found in {LogDirectory}", flareRequestId, logDirectory);
            return false;
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

            return false;
        }
    }

    public static Task WriteDebugLogArchiveToStream(Stream writeTo, string logDirectory)
        => WriteDebugLogArchiveToStream(writeTo, logDirectory, StreamHasCapacity);

    public static async Task WriteDebugLogArchiveToStream(Stream writeTo, string logDirectory, Func<FileInfo, long, bool> streamHasCapacityFunc)
    {
        try
        {
            using var monitoringStream = new WriteCountingStream(writeTo);
            using var archive = new ZipArchive(monitoringStream, ZipArchiveMode.Create, true);

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
                    // There's a SmallestSize for .NET 5+ but it doesn't give much benefit
                    // and we would rather not add the extra memory pressure for little gain
                    var entry = archive.CreateEntry(fileDetails.Name, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    // Have to allow FileShare.ReadWrite as the logger will already have it open for writing
                    using var file = File.Open(fileDetails.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    await file.CopyToAsync(entryStream).ConfigureAwait(false);
                    await entryStream.FlushAsync().ConfigureAwait(false);
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

    internal static bool StreamHasCapacity(FileInfo fileDetails, long remainingCapacity)
    {
        // we estimate that the file compresses _roughly_ 10x, so if we're going to exceed that
        // we bail out. That means that we may miss some older smaller files, but at least we
        // have a clear cut off.
        var estimatedFileSize = fileDetails.Length / 10;
        return remainingCapacity >= estimatedFileSize;
    }

    public class WriteCountingStream(Stream innerStream) : LeaveOpenDelegatingStream(innerStream)
    {
        private long _position = 0;

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
