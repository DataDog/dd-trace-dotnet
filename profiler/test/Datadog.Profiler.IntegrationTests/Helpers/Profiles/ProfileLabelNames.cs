// <copyright file="ProfileLabelNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    /// <summary>
    /// Label names the profiler writes to its samples.
    /// </summary>
    internal static class ProfileLabelNames
    {
        public const string ThreadName = "thread name";

        public const string ThreadId = "thread id";

        public const string Event = "event";

        public const string ObjectLifetime = "object lifetime";

        public const string BlockingThreadId = "blocking thread id";

        public const string ContentionType = "contention type";

        public const string RequestUrl = "request url";

        public const string RedirectUrl = "redirect url";

        public const string ResponseStatusCode = "response status code";

        public const string ResponseError = "response error";

        public const string ResponseThreadId = "response.thread_id";

        public const string ResponseThreadName = "response.thread_name";

        public const string ResponseContentDuration = "response_content.duration";

        public const string RequestDuration = "request.duration";

        public const string DnsSuccess = "dns.success";

        public const string DnsDuration = "dns.duration";

        public const string SocketDuration = "socket.duration";

        public const string TlsDuration = "tls.duration";
    }
}
