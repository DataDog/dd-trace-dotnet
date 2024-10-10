// <copyright file="IastVerifyScrubberExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyTests;

namespace Datadog.Trace.Security.IntegrationTests.IAST
{
    public static class IastVerifyScrubberExtensions
    {
        private static readonly (Regex RegexPattern, string Replacement) ClientIp = (new Regex(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}"), string.Empty);
        private static readonly (Regex RegexPattern, string Replacement) NetworkClientIp = (new Regex(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}"), string.Empty);
        private static readonly (Regex RegexPattern, string Replacement) RequestTaintedRegex = (new Regex(@"_dd.iast.telemetry.request.tainted:(\s)*([1-9])(\d*).?(\d*),"), "_dd.iast.telemetry.request.tainted:,");
        private static readonly (Regex RegexPattern, string Replacement) TelemetryExecutedSinks = (new Regex(@"_dd\.iast\.telemetry\.executed\.sink\.weak_.+: .{3},"), string.Empty);

        private static readonly (Regex RegexPattern, string Replacement) SpanIdRegex = (new Regex("\"spanId\": \\d+"), "\"spanId\": XXX");
        private static readonly (Regex RegexPattern, string Replacement) LineRegex = (new Regex("\"line\": \\d+"), "\"line\": XXX");

        private static readonly Type MetaStructHelperType = Type.GetType("Datadog.Trace.AppSec.Rasp.MetaStructHelper, Datadog.Trace");
        private static readonly MethodInfo MetaStructByteArrayToObject = MetaStructHelperType.GetMethod("ByteArrayToObject", BindingFlags.Public | BindingFlags.Static);

        public static VerifySettings AddIastScrubbing(this VerifySettings settings, bool forceMetaStruct = false)
        {
            AddIastSerializationModification(settings, forceMetaStruct);
            var scrubbers = new List<(Regex RegexPattern, string Replacement)>();
            return AddIastScrubbing(settings, scrubbers);
        }

        public static VerifySettings AddIastScrubbing(this VerifySettings settings, IEnumerable<(Regex RegexPattern, string Replacement)> extraScrubbers)
        {
            settings.AddRegexScrubber(ClientIp);
            settings.AddRegexScrubber(NetworkClientIp);
            settings.AddRegexScrubber(RequestTaintedRegex);
            settings.AddRegexScrubber(TelemetryExecutedSinks);

            settings.AddRegexScrubber(SpanIdRegex);
            settings.AddRegexScrubber(LineRegex);

            if (extraScrubbers != null)
            {
                foreach (var scrubber in extraScrubbers)
                {
                    settings.AddRegexScrubber(scrubber.RegexPattern, scrubber.Replacement);
                }
            }

            settings.ScrubEmptyLines();
            return settings;
        }

        public static VerifySettings AddIastSerializationModification(this VerifySettings settings, bool forceMetaStruct = false)
        {
            settings.ModifySerialization(
                serializationSettings =>
                {
                    serializationSettings.MemberConverter<MockSpan, Dictionary<string, string>>(
                        sp => sp.Tags,
                        (target, value) =>
                        {
                            if (target.MetaStruct != null)
                            {
                                IastMetaStructScrubbing(target, forceMetaStruct);

                                // Remove all data from meta structs keys, no need to get the binary data for other keys
                                foreach (var key in target.MetaStruct.Keys.ToList())
                                {
                                    target.MetaStruct[key] = [];
                                }
                            }

                            return VerifyHelper.ScrubStringTags(target, target.Tags);
                        });
                });

            return settings;
        }

        public static void IastMetaStructScrubbing(MockSpan target, bool forceMetaStruct = false)
        {
            // We want to retrieve the iast data from the meta struct to validate it in snapshots
            // But that's hard to debug if we only see the binary data
            // So move the meta struct iast data to a fake tag to validate it in snapshots
            if (target.MetaStruct.TryGetValue("iast", out var iast))
            {
                var iastMetaStruct = MetaStructByteArrayToObject.Invoke(null, [iast]);
                var json = JsonConvert.SerializeObject(iastMetaStruct, Formatting.Indented);
                target.Tags[Tags.IastJson] = json;

                target.MetaStruct.Remove("iast");

                // Let the snapshot know that the data comes from the meta struct
                if (forceMetaStruct)
                {
                    target.Tags[Tags.IastJson + ".metastruct.test"] = "true";
                }
            }
        }
    }
}
