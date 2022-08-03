// <copyright file="MultipartFormItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;

namespace Datadog.Trace.Agent
{
    internal readonly struct MultipartFormItem
    {
        public readonly string Name;
        public readonly string ContentType;
        public readonly string? FileName;
        public readonly ArraySegment<byte>? ContentInBytes;
        public readonly Stream? ContentInStream;

        public MultipartFormItem(string name, string contentType, string? fileName, ArraySegment<byte> contentInBytes)
        {
            Name = name;
            ContentType = contentType;
            FileName = fileName;
            ContentInBytes = contentInBytes;
            ContentInStream = null;
        }

        public MultipartFormItem(string name, string contentType, string? fileName, Stream contentInStream)
        {
            Name = name;
            ContentType = contentType;
            FileName = fileName;
            ContentInBytes = null;
            ContentInStream = contentInStream;
        }
    }
}
