// <copyright file="SchemaExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Any;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Models;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Writers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

internal class SchemaExtractor
{
    internal const int MaxProtobufSchemas = 100;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SchemaExtractor>();

    private static readonly SmallCacheOrNoCache<string, Schema> SchemaCache = new(MaxProtobufSchemas, "protobuf schema names");

    /// <summary>
    /// Get the current span, add some tags about the schema,
    /// and once in a while, tag it with the whole protobuf schema used
    /// </summary>
    internal static void EnrichActiveSpanWith(IMessageDescriptorProxy? descriptor, string operationName)
    {
        var activeSpan = (Tracer.Instance.ActiveScope as Scope)?.Span;
        if (activeSpan == null || descriptor == null)
        {
            return;
        }

        if (activeSpan.GetTag(Tags.SchemaType) == "protobuf")
        {
            // we already instrumented this, we are most likely in a recursive call due to nested schemas.
            return;
        }

        activeSpan.SetTag(Tags.SchemaType, "protobuf");
        activeSpan.SetTag(Tags.SchemaName, descriptor.Name);
        activeSpan.SetTag(Tags.SchemaOperation, operationName);

        // check rate limit
        var dsm = Tracer.Instance.TracerManager.DataStreamsManager;
        if (!dsm.ShouldExtractSchema(activeSpan, operationName, out var weight))
        {
            return;
        }

        // check cache (it will be disabled if too many schemas)
        var schema = SchemaCache.GetOrAdd(
            descriptor.Name,
            _ =>
            {
                var openApiSchema = ExtractSchema(descriptor);
                var schema = new Schema(openApiSchema);
                Log.Debug<string, int>("Extracted new protobuf schema with name '{Name}' of size {Size} characters.", descriptor.Name, schema.JsonDefinition.Length);
                return schema;
            });

        activeSpan.SetTag(Tags.SchemaDefinition, schema.JsonDefinition);
        activeSpan.SetTag(Tags.SchemaId, schema.Hash.ToString(CultureInfo.InvariantCulture));
        activeSpan.SetTag(Tags.SchemaWeight, weight.ToString(CultureInfo.InvariantCulture));
    }

    private static OpenApiSchema ExtractSchema(IMessageDescriptorProxy descriptor)
    {
        return new OpenApiSchema { Type = "object", Properties = ExtractFields(descriptor) };
    }

    private static IDictionary<string, OpenApiSchema> ExtractFields(IMessageDescriptorProxy descriptor)
    {
        var properties = new Dictionary<string, OpenApiSchema>();
        foreach (var o in descriptor.Fields.InDeclarationOrder())
        {
            var field = o.DuckCast<IFieldDescriptorProxy>()!;
            var fieldName = field.Name; // use JsonName ?
            string? type = null, format = null, description = null;
            OpenApiReference? reference = null;
            IList<IOpenApiAny>? enumValues = null;
            IDictionary<string, OpenApiSchema>? subProperties = null;
            switch (field.FieldType)
            {
                case 0:
                    type = "number";
                    format = "double";
                    break;
                case 1:
                    type = "number";
                    format = "float";
                    break;
                case 2:
                    type = "integer";
                    format = "int64";
                    break;
                case 3:
                case 16: // sint64
                    // OpenAPI does not directly support unsigned integers, treated as integers
                    type = "integer";
                    format = "uint64";
                    break;
                case 4:
                case 15: // sint32
                    type = "integer";
                    format = "int32";
                    break;
                case 5:
                    // Treated as an integer because OpenAPI does not have a fixed64 format.
                    type = "integer";
                    format = "fixed64";
                    break;
                case 6:
                    type = "integer";
                    format = "fixed32";
                    break;
                case 7:
                    type = "boolean";
                    break;
                case 8:
                    type = "string";
                    break;
                case 9: // group
                    // Groups are deprecated and usually represented as nested messages in OpenAPI
                    type = "object";
                    description = "Group type";
                    break;
                case 10: // message
                    reference = new OpenApiReference { Id = "#/components/schemas/" + field.MessageType.Name };
                    // Recursively add nested message schemas
                    subProperties = ExtractFields(field.MessageType);
                    break;
                case 11:
                    type = "string";
                    format = "byte";
                    break;
                case 12:
                    // As with UINT64, treated as integers or strings because OpenAPI does not directly
                    // support unsigned integers
                    type = "integer";
                    format = "uint32";
                    break;
                case 13:
                    type = "integer";
                    format = "sfixed32";
                    break;
                case 14:
                    type = "integer";
                    format = "sfixed64";
                    break;
                // cases 15 and 16 are above
                case 17: // enum
                    type = "string";
                    enumValues = new List<IOpenApiAny>(field.EnumType.Values.Count);
                    foreach (var e in field.EnumType.Values)
                    {
                        var enumVal = e.DuckCast<IEnumValueDescriptorProxy>()!;
                        enumValues.Add(new OpenApiString(enumVal.Name));
                    }

                    break;
                default:
                    // OpenAPI does not have a direct mapping for unknown types, usually treated as strings or
                    // omitted
                    type = "string";
                    description = "Unknown type";
                    break;
            }

            var property = new OpenApiSchema
            {
                Type = type,
                Description = description,
                Reference = reference,
                Format = format,
                Enum = enumValues,
                Properties = subProperties
            };
            if (field.IsRepeated)
            {
                property = new OpenApiSchema { Type = "array", Items = property };
            }

            properties.Add(fieldName, property);
        }

        return properties;
    }

    private class Schema
    {
        public Schema(OpenApiSchema openApiSchema)
        {
            using (var writer = new StringWriter())
            {
                openApiSchema.SerializeAsV3WithoutReference(new OpenApiJsonWriter(writer));
                JsonDefinition = writer.ToString();
            }

            Hash = FnvHash64.GenerateHash(JsonDefinition, FnvHash64.Version.V1A);
        }

        internal string JsonDefinition { get; }

        internal ulong Hash { get; }
    }
}
