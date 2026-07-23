// <copyright file="GlobalCoverageInputReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageInputReader
{
    private readonly GlobalCoverageArtifactLimits _limits;

    internal GlobalCoverageInputReader(GlobalCoverageArtifactLimits? limits = null)
    {
        _limits = limits ?? GlobalCoverageArtifactLimits.Default;
    }

    private static bool HashesMatch(byte[] first, byte[] second)
    {
        if (first.Length != second.Length)
        {
            return false;
        }

        var difference = 0;
        for (var i = 0; i < first.Length; i++)
        {
            difference |= first[i] ^ second[i];
        }

        return difference == 0;
    }

    internal bool TryRead(string path, out GlobalCoverageInfo? model)
        => TryRead(path, expectedInput: null, out model);

    internal bool TryRead(string path, GlobalCoverageCertifiedInput? expectedInput, out GlobalCoverageInfo? model)
    {
        model = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var expectedLength = stream.Length;
            if (expectedLength < 0 || expectedLength > _limits.MaximumSerializedBytes)
            {
                return false;
            }

            if (expectedInput is not null && expectedInput.Length != expectedLength)
            {
                return false;
            }

            var preflightHash = RunPreflight(stream);
            if (stream.Length != expectedLength ||
                (expectedInput is not null && !HashesMatch(expectedInput.Hash, preflightHash)))
            {
                return false;
            }

            stream.Seek(0, SeekOrigin.Begin);
            var deserialized = Deserialize(stream, out var deserializeHash);
            if (stream.Length != expectedLength || !HashesMatch(preflightHash, deserializeHash))
            {
                return false;
            }

            ValidateModel(deserialized);
            model = deserialized;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or OverflowException or OutOfMemoryException or CryptographicException)
        {
            return false;
        }
    }

    internal void ValidateModel(GlobalCoverageInfo model)
    {
        if (model.Components.Count > _limits.MaximumComponents)
        {
            throw new InvalidDataException("The global coverage component limit was exceeded.");
        }

        var entryCount = 0;
        long identityCharacters = 0;
        long bitmapBytes = 0;
        foreach (var component in model.Components)
        {
            if (component is null)
            {
                throw new InvalidDataException("The global coverage input contains a null component.");
            }

            identityCharacters = checked(identityCharacters + (component.Name?.Length ?? 0));
            foreach (var file in component.Files)
            {
                if (file is null)
                {
                    throw new InvalidDataException("The global coverage input contains a null file.");
                }

                entryCount = checked(entryCount + 1);
                identityCharacters = checked(identityCharacters + (file.Path?.Length ?? 0));
                AddBitmap(file.ExecutableBitmap);
                AddBitmap(file.ExecutedBitmap);
            }
        }

        if (entryCount > _limits.MaximumEntries)
        {
            throw new InvalidDataException("The global coverage entry limit was exceeded.");
        }

        if (identityCharacters > _limits.MaximumIdentityCharacters)
        {
            throw new InvalidDataException("The global coverage path/name character limit was exceeded.");
        }

        void AddBitmap(byte[]? bitmap)
        {
            if (bitmap is null)
            {
                return;
            }

            if (bitmap.Length > _limits.MaximumBitmapBytes)
            {
                throw new InvalidDataException("A global coverage bitmap exceeds the per-bitmap limit.");
            }

            bitmapBytes = checked(bitmapBytes + bitmap.Length);
            if (bitmapBytes > _limits.MaximumModelBitmapBytes)
            {
                throw new InvalidDataException("The global coverage model bitmap limit was exceeded.");
            }
        }
    }

    private byte[] RunPreflight(FileStream stream)
    {
        using var sha256 = SHA256.Create();
        using var hashingStream = new HashingReadStream(stream, sha256);
        new GlobalCoverageJsonPreflightScanner(_limits).Scan(hashingStream);

        return sha256.Hash ?? throw new CryptographicException("Unable to hash the global coverage input.");
    }

    private GlobalCoverageInfo Deserialize(FileStream stream, out byte[] hash)
    {
        using var sha256 = SHA256.Create();
        GlobalCoverageInfo? model;
        using (var hashingStream = new HashingReadStream(stream, sha256))
        using (var streamReader = new StreamReader(hashingStream, new UTF8Encoding(false, true), true, _limits.ScannerBufferCharacters, true))
        using (var jsonReader = new JsonTextReader(streamReader) { MaxDepth = _limits.MaximumDepth, DateParseHandling = DateParseHandling.None })
        {
            model = JsonSerializer.Create().Deserialize<GlobalCoverageInfo>(jsonReader);
            if (model is null)
            {
                throw new InvalidDataException("The global coverage input does not contain a model.");
            }

            if (jsonReader.Read())
            {
                throw new InvalidDataException("The global coverage input contains trailing JSON tokens.");
            }
        }

        hash = sha256.Hash ?? throw new CryptographicException("Unable to hash the global coverage input.");
        return model;
    }

    private sealed class HashingReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly HashAlgorithm _hash;
        private bool _completed;

        internal HashingReadStream(Stream inner, HashAlgorithm hash)
        {
            _inner = inner;
            _hash = hash;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (read > 0)
            {
                _hash.TransformBlock(buffer, offset, read, buffer, offset);
            }
            else if (!_completed)
            {
                _completed = true;
                _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
