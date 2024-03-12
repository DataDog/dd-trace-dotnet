// <copyright file="TruncatedVulnerabilities.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.Iast;

/// <summary>
/// Custom JSON serializer for <see cref="Datadog.Trace.Iast.Source"/> struct
/// </summary>
internal struct TruncatedVulnerabilities
{
    private const string MaxSizeExceeded = "MAX SIZE EXCEEDED";

    private List<Vulnerability> vulnerabilities;

    public TruncatedVulnerabilities(List<Vulnerability> vulnerabilities)
    {
        this.vulnerabilities = vulnerabilities;
    }

    public List<Vulnerability> Vulnerabilities => vulnerabilities;

    public class VulnerabilityConverter : JsonConverter<Vulnerability?>
    {
        public VulnerabilityConverter()
        {
        }

        public override bool CanRead => true;

        public override Vulnerability? ReadJson(JsonReader reader, Type objectType, Vulnerability? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Vulnerability? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("type");
                writer.WriteValue(value.Value.Type);
                writer.WritePropertyName("evidence");
                serializer.Serialize(writer, value.Value.Evidence);
                writer.WritePropertyName("hash");
                writer.WriteValue(value.Value.Hash);
                writer.WritePropertyName("location");
                serializer.Serialize(writer, value.Value.Location);

                writer.WriteEndObject();
            }
        }
    }

    public class EvidenceConverter : JsonConverter<Evidence?>
    {
        public EvidenceConverter()
        {
        }

        public override bool CanRead => true;

        public override Evidence? ReadJson(JsonReader reader, Type objectType, Evidence? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Evidence? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                writer.WriteValue(MaxSizeExceeded);
                writer.WriteEndObject();
            }
        }
    }
}
