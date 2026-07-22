// <copyright file="GlobalCoverageArtifactWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageArtifactWriter
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

    private readonly GlobalCoverageArtifactLimits _limits;
    private readonly GlobalCoverageInputReader _validator;

    internal GlobalCoverageArtifactWriter(GlobalCoverageArtifactLimits? limits = null)
    {
        _limits = limits ?? GlobalCoverageArtifactLimits.Default;
        _validator = new GlobalCoverageInputReader(_limits);
    }

    internal void WriteAtomicNoReplace(string destinationPath, GlobalCoverageInfo model)
    {
        using var staged = Stage(destinationPath, model, replaceExisting: false);
        staged.Commit();
    }

    internal void WriteAtomicReplace(string destinationPath, GlobalCoverageInfo model)
    {
        using var staged = Stage(destinationPath, model, replaceExisting: true);
        staged.Commit();
    }

    internal GlobalCoverageStagedArtifact StageNoReplace(string destinationPath, GlobalCoverageInfo model)
        => Stage(destinationPath, model, replaceExisting: false);

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A failed write must preserve the primary exception. The non-json temporary is never a valid input.
        }
    }

    private GlobalCoverageStagedArtifact Stage(string destinationPath, GlobalCoverageInfo model, bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A global coverage destination path is required.", nameof(destinationPath));
        }

        _validator.ValidateModel(model);

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullDestinationPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The global coverage destination has no parent directory.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.SequentialScan))
            using (var boundedStream = new BoundedWriteStream(fileStream, _limits.MaximumSerializedBytes))
            using (var streamWriter = new StreamWriter(boundedStream, Utf8WithoutBom, 16 * 1024, true))
            using (var jsonWriter = new JsonTextWriter(streamWriter) { ArrayPool = JsonArrayPool.Shared })
            {
                JsonSerializer.Create().Serialize(jsonWriter, model);
                jsonWriter.Flush();
                streamWriter.Flush();
                boundedStream.Flush();
                fileStream.Flush(true);
            }

            return new GlobalCoverageStagedArtifact(temporaryPath, fullDestinationPath, replaceExisting);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumBytes;
        private long _writtenBytes;

        internal BoundedWriteStream(Stream inner, long maximumBytes)
        {
            _inner = inner;
            _maximumBytes = maximumBytes;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _writtenBytes;

        public override long Position
        {
            get => _writtenBytes;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var nextLength = checked(_writtenBytes + count);
            if (nextLength > _maximumBytes)
            {
                throw new InvalidDataException("The global coverage serialized-byte limit was exceeded.");
            }

            _inner.Write(buffer, offset, count);
            _writtenBytes = nextLength;
        }
    }
}
