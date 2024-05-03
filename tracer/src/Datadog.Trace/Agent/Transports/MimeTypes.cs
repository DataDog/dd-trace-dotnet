// <copyright file="MimeTypes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Agent.Transports
{
    internal static class MimeTypes
    {
        public const string MsgPack = "application/msgpack";
        public const string Json = "application/json";
        public const string MultipartFormData = "multipart/form-data";
        public const string PlainText = "text/plain";
    }
}
