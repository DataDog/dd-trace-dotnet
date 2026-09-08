// <copyright file="TestParameters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Test parameters
    /// </summary>
    public sealed class TestParameters
    {
        private const string FingerprintFormatMetadataKey = "_dd.parameters_format";
        private const string FingerprintFormat = "sha256-v1";
        private const string FingerprintArgumentKey = "_dd.parameters_fingerprint";

        /// <summary>
        /// Gets or sets the test parameters metadata
        /// </summary>
        [JsonProperty("metadata")]
        public Dictionary<string, object?>? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the test arguments
        /// </summary>
        [JsonProperty("arguments")]
        public Dictionary<string, object?>? Arguments { get; set; }

        internal string ToJSON()
        {
            var json = JsonHelper.SerializeObject(this);
            if (Encoding.UTF8.GetByteCount(json) <= TruncatorTagsProcessor.MaxMetaValLen)
            {
                return json;
            }

            // test.parameters is a span meta value, so a longer value would be truncated into invalid JSON.
            // Preserve a compact, versioned identity that the ITR matcher can reproduce instead.
            var fingerprintParameters = new TestParameters
            {
                Metadata = new Dictionary<string, object?> { [FingerprintFormatMetadataKey] = FingerprintFormat },
                Arguments = new Dictionary<string, object?> { [FingerprintArgumentKey] = Sha256Helper.ComputeHashAsHexString(json, Encoding.UTF8) }
            };
            return JsonHelper.SerializeObject(fingerprintParameters);
        }

        internal bool TryGetFingerprint(out string fingerprint)
        {
            if (Metadata is { Count: 1 } &&
                Metadata.TryGetValue(FingerprintFormatMetadataKey, out var format) &&
                string.Equals(format as string, FingerprintFormat, System.StringComparison.Ordinal) &&
                Arguments is { Count: 1 } &&
                Arguments.TryGetValue(FingerprintArgumentKey, out var fingerprintValue) &&
                fingerprintValue is string { Length: 64 } fingerprintString)
            {
                fingerprint = fingerprintString;
                return true;
            }

            fingerprint = string.Empty;
            return false;
        }

        internal string GetFingerprint()
        {
            return Sha256Helper.ComputeHashAsHexString(JsonHelper.SerializeObject(this), Encoding.UTF8);
        }
    }
}
