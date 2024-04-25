// <copyright file="MultipartFormItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Diagnostics.CodeAnalysis;

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

        [MemberNotNullWhen(true, nameof(ContentInStream))]
        [MemberNotNullWhen(false, nameof(ContentInBytes))]
        public bool IsStream => ContentInStream is not null;

        /// <summary>
        /// Checks if the <see cref="MultipartFormItem"/> is valid, such as having valid names and content types.
        /// Returns true if the item is valid, and false otherwise. If not valid, records the issues as warnings
        /// </summary>
        public bool IsValid(IDatadogLogger log)
        {
            // Check name is not null (required)
            if (Name is null)
            {
                log.Warning("Error encoding multipart form item name is null. Ignoring item");
                return false;
            }

            // Not valid if the name contains ' or "
            if (Name.IndexOf("\"", StringComparison.Ordinal) != -1 || Name.IndexOf("'", StringComparison.Ordinal) != -1)
            {
                log.Warning("Error encoding multipart form item name: {Name}. Ignoring item.", Name);
                return false;
            }

            // Do the same checks for FileName if not null
            if (FileName is not null
             && (FileName.IndexOf("\"", StringComparison.Ordinal) != -1 || FileName.IndexOf("'", StringComparison.Ordinal) != -1))
            {
                log.Warning("Error encoding multipart form item filename: {FileName}. Ignoring item.", FileName);
                return false;
            }

            return true;
        }
    }
}
