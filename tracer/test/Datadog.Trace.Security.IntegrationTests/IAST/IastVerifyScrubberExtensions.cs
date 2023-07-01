// <copyright file="IastVerifyScrubberExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using VerifyTests;

namespace Datadog.Trace.Security.IntegrationTests.IAST
{
    public static class IastVerifyScrubberExtensions
    {
        private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
        private static readonly Regex ClientIp = new(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}");
        private static readonly Regex NetworkClientIp = new(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}");
        private static readonly Regex HashRegex = new(@"(\S)*""hash"": (-){0,1}([0-9]){1,12},(\r|\n){1,2}      ");

        public static VerifySettings AddIastScrubbing(this VerifySettings settings, bool scrubHash = true)
        {
            settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);

            if (scrubHash)
            {
                settings.AddRegexScrubber(HashRegex, string.Empty);
            }

            return settings;
        }
    }
}
