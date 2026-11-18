// <copyright file="NamedRawFile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal readonly struct NamedRawFile
    {
        public NamedRawFile(RemoteConfigurationPath path, byte[] value)
        {
            Path = path;
            RawFile = value;
        }

        public RemoteConfigurationPath Path { get; }

        public byte[] RawFile { get; }

        public NamedTypedFile<T?> Deserialize<T>() => Deserialize<T>(DateParseHandling.DateTime);

        public NamedTypedFile<T?> Deserialize<T>(DateParseHandling dateParseHandling)
        {
            using var stream = new MemoryStream(RawFile);
            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader)
            {
                ArrayPool = JsonArrayPool.Shared,
                DateParseHandling = dateParseHandling,
            };

            // CreateDefault() leaves DateParseHandling unset, so the reader's value applies.
            return new NamedTypedFile<T?>(Path.Path, JsonSerializer.CreateDefault().Deserialize<T>(jsonReader));
        }
    }
}
