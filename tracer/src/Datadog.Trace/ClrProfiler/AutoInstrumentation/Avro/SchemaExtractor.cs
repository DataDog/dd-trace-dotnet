// <copyright file="SchemaExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Globalization;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Avro;

internal class SchemaExtractor
{
    internal static void EnrichActiveSpanWith(ISchemaProxy? descriptor, string operationName)
    {
        if (descriptor == null || descriptor.Instance == null || !Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.Avro))
        {
            return;
        }

        SchemaTrackingCommon.EnrichActiveSpanWith(new MessageDescriptorWrapper(descriptor), operationName, "avro");
    }

    private class MessageDescriptorWrapper : SchemaTrackingCommon.IDescriptorWrapper
    {
        private static readonly ConditionalWeakTable<object, Schema> AvroSchemaToDsmSchema = new();

        private readonly ISchemaProxy _descriptor;

        public MessageDescriptorWrapper(ISchemaProxy descriptor)
        {
            _descriptor = descriptor;
        }

        public string? Name
        {
            get => _descriptor.Fullname;
        }

        public Schema? GetSchema()
        {
            var instance = _descriptor.Instance;
            if (instance is null)
            {
                return null;
            }

            if (AvroSchemaToDsmSchema.TryGetValue(instance, out var schema))
            {
                return schema;
            }

            var json = instance.ToString(); // costly, does the serialization every time
            if (json is null)
            {
                return null;
            }

            var id = FnvHash64.GenerateHash(json, FnvHash64.Version.V1A);
            schema = new Schema(json, id.ToString(CultureInfo.InvariantCulture));

#if NETCOREAPP3_1_OR_GREATER
            AvroSchemaToDsmSchema.AddOrUpdate(instance, schema);
#else
            AvroSchemaToDsmSchema.GetValue(instance, x => schema);
#endif

            return schema;
        }
    }
}
