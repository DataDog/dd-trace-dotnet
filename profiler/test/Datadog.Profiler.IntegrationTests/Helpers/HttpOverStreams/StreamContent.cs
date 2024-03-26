// <copyright file="StreamContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Profiler.IntegrationTests.Helpers.HttpOverStreams
{
    internal class StreamContent : IHttpContent
    {
        public StreamContent(Stream stream, long? length)
        {
            Stream = stream;
            Length = length;
        }

        public Stream Stream { get; }

        public long? Length { get; }

        public Task CopyToAsync(Stream destination)
        {
            return Stream.CopyToAsync(destination);
        }

        public async Task CopyToAsync(byte[] buffer)
        {
            if (!Length.HasValue)
            {
                throw new InvalidOperationException("Unable to CopyToAsync with buffer when content Length is unknown");
            }

            if (Length > buffer.Length)
            {
                throw new ArgumentException($"Provided buffer was smaller {buffer.Length} than the content length {Length}");
            }

            var length = 0;
            long remaining = Length.Value;
            while (true)
            {
                var bytesToRead = (int)Math.Min(remaining, int.MaxValue);
                var bytesRead = await Stream.ReadAsync(buffer, offset: length, count: bytesToRead).ConfigureAwait(false);

                length += bytesRead;
                remaining -= bytesRead;

                if (bytesRead == 0 || remaining <= 0)
                {
                    return;
                }
            }
        }
    }
}
