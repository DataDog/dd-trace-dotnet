// <copyright file="SchemaExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Any;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Interfaces;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Models;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Writers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

internal static class SchemaExtractor
{
    private const int MaxProtobufSchemas = 100;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SchemaExtractor));

    private static readonly SmallCacheOrNoCache<string, Schema> SchemaCache = new(MaxProtobufSchemas, "protobuf schema names");

    /// <summary>
    /// Get the current span, add some tags about the schema,
    /// and once in a while, tag it with the whole protobuf schema used
    /// </summary>
    internal static void EnrichActiveSpanWith(MessageDescriptorProxy? descriptor, string operationName)
    {
        var tracer = Tracer.Instance;

        var settings = tracer.CurrentTraceSettings;
        if (!tracer.Settings.IsDataStreamsSchemaExtractionEnabled || !settings.Settings.IsIntegrationEnabled(IntegrationId.Protobuf))
        {
            return;
        }

        var activeSpan = (tracer.ActiveScope as Scope)?.Span;
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
        activeSpan.SetTag(Tags.SchemaName, descriptor.Value.FullName);
        activeSpan.SetTag(Tags.SchemaOperation, operationName);

        // check rate limit
        var dsm = tracer.TracerManager.DataStreamsManager;
        if (!dsm.ShouldExtractSchema(activeSpan, operationName, out var weight))
        {
            return;
        }

        // check cache (it will be disabled if too many schemas)
        var schema = SchemaCache.GetOrAdd(
            descriptor.Value.Name,
            _ =>
            {
                var schema = Extractor.ExtractSchemas(descriptor.Value);
                Log.Debug<string, int>("Extracted new protobuf schema with name '{Name}' of size {Size} characters.", descriptor.Value.Name, schema.JsonDefinition.Length);
                return schema;
            });

        activeSpan.SetTag(Tags.SchemaDefinition, schema.JsonDefinition);
        activeSpan.SetTag(Tags.SchemaId, schema.Hash.ToString(CultureInfo.InvariantCulture));
        activeSpan.SetTag(Tags.SchemaWeight, weight.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class Extractor
    {
        // those two properties need to be consistent across tracers to ensure consistent hashes when proto messages cross language boundaries.
        // take extra care if modifying them.
        private const int MaxExtractionDepth = 10;
        private const int MaxProperties = 1000;

        // hashing an empty string is a no-op, allowing us to retrieve the default value for 'initialHash'
        private static readonly ulong BaseHash = FnvHash64.GenerateHash(string.Empty, FnvHash64.Version.V1A);

        private readonly IDictionary<string, OpenApiSchema> _schemas;
        private ulong _computedHash = BaseHash;
        private int _propertiesCount;
        private bool _maxPropsLogged;

        /// <param name="componentsSchemas">The Dictionary that will be filled when extraction is performed</param>
        private Extractor(IDictionary<string, OpenApiSchema> componentsSchemas)
        {
            _schemas = componentsSchemas;
        }

        public static Schema ExtractSchemas(MessageDescriptorProxy descriptor)
        {
            var components = new OpenApiComponents();
            var hash = new Extractor(components.Schemas).FillSchemasWith(descriptor); // fill the component's schemas
            var doc = new OpenApiDocument { Components = components };
            return new Schema(doc, hash);
        }

        /// <summary>
        /// Add the given message's schema and all its sub-messages schemas to the Dictionary that was given to the ctor.
        /// Returns a hash identifying the schema.
        ///
        /// The hash must be stable between tracers/languages to ensure proper tracking across polyglot services.
        /// It is computed iterating on fields by number order, using in order:
        ///  - SPECIAL, if relevant:
        ///     - the name of the referenced type
        ///     - the enum values
        ///  - the field number
        ///  - the type ID as defined in https://github.com/protocolbuffers/protobuf/blob/main/src/google/protobuf/type.proto
        ///  - the zero-based depth (allowing to distinguish a nested type)
        /// If a field references a sub-message, that sub-message is expended first, before the current field is added to the hash.
        /// Extraction stops after 10 nested messages, this affects the hash, so it must be kept consistent across tracers.
        ///
        /// The content of the hash has been chosen so that, as much as possible,
        /// changes to the schema that don't affect the wire format don't change the hash, such as:
        ///  - renaming fields
        ///  - changing the order of fields declaration without changing their number
        /// While changes that impact the wire format should change the hash, such as:
        ///  - changing a field number
        ///  - changing the hierarchy of sub-messages
        /// </summary>
        private ulong FillSchemasWith(MessageDescriptorProxy descriptor, int depth = 1 /* start at 1 to match what java is doing */)
        {
            if (depth > MaxExtractionDepth)
            {
                Log.Debug("Reached max depth of {MaxDepth} when extracting protobuf schema for field {Field} of schema {SchemaName}, will not extract further.", MaxExtractionDepth, descriptor.Name, descriptor.File.Name);
                return _computedHash;
            }

            if (!_schemas.ContainsKey(descriptor.FullName))
            {
                var schema = new OpenApiSchema { Type = "object" };
                _schemas.Add(descriptor.FullName, schema);
                // It's important that we extract the fields AFTER adding the key to the dict, to make sure we don't re-extract on recursive message types
                schema.Properties = ExtractFields(descriptor, depth);
            }

            return _computedHash;
        }

        private Dictionary<string, OpenApiSchema> ExtractFields(MessageDescriptorProxy descriptor, int depth)
        {
            var properties = new Dictionary<string, OpenApiSchema>();

            foreach (var o in descriptor.Fields.InFieldNumberOrder())
            {
                if (_propertiesCount >= MaxProperties)
                {
                    if (!_maxPropsLogged)
                    {
                        Log.Debug("Reached max properties count of {MaxProperties} while extracting protobuf schema {SchemaName}, will stop extracting the schema now", MaxProperties, property1: descriptor.File.Name);
                        _maxPropsLogged = true;
                    }

                    return properties;
                }

                var field = o.DuckCast<IFieldDescriptorProxy>()!;
                var fieldName = field.Name;
                string? type = null, format = null, description = null;
                OpenApiReference? reference = null;
                IList<IOpenApiAny>? enumValues = null;

                // the csharp implementation of protobuf uses an enum with different values to handle types internally.
                // we must convert it back to the "common" value for consistency with other tracers
                var protoType = field.FieldType.ToProtoType();
                switch (protoType)
                {
                    case ProtobufDotnetProtoType.Double:
                        type = "number";
                        format = "double";
                        break;
                    case ProtobufDotnetProtoType.Float:
                        type = "number";
                        format = "float";
                        break;
                    case ProtobufDotnetProtoType.Int64:
                        type = "integer";
                        format = "int64";
                        break;
                    case ProtobufDotnetProtoType.Uint64:
                    case ProtobufDotnetProtoType.Sint64:
                        // OpenAPI does not directly support unsigned integers, treated as integers
                        type = "integer";
                        format = "uint64";
                        break;
                    case ProtobufDotnetProtoType.Int32:
                    case ProtobufDotnetProtoType.Sint32:
                        type = "integer";
                        format = "int32";
                        break;
                    case ProtobufDotnetProtoType.Fixed64:
                        // Treated as an integer because OpenAPI does not have a fixed64 format.
                        type = "integer";
                        format = "fixed64";
                        break;
                    case ProtobufDotnetProtoType.Fixed32:
                        type = "integer";
                        format = "fixed32";
                        break;
                    case ProtobufDotnetProtoType.Bool:
                        type = "boolean";
                        break;
                    case ProtobufDotnetProtoType.String:
                        type = "string";
                        break;
                    case ProtobufDotnetProtoType.Group:
                        // Groups are deprecated and usually represented as nested messages in OpenAPI
                        type = "object";
                        description = "Group type";
                        break;
                    case ProtobufDotnetProtoType.Message:
                        FillSchemasWith(field.MessageType, depth + 1); // Recursively add nested schemas (conditions apply)
                        reference = new OpenApiReference { Id = field.MessageType.Name, Type = ReferenceType.Schema };
                        _computedHash = FnvHash64.GenerateHash(field.MessageType.FullName, FnvHash64.Version.V1A, _computedHash);
                        break;
                    case ProtobufDotnetProtoType.Bytes:
                        type = "string";
                        format = "byte";
                        break;
                    case ProtobufDotnetProtoType.Uint32:
                        // As with UINT64, treated as integers or strings because OpenAPI does not directly
                        // support unsigned integers
                        type = "integer";
                        format = "uint32";
                        break;
                    case ProtobufDotnetProtoType.Sfixed32:
                        type = "integer";
                        format = "sfixed32";
                        break;
                    case ProtobufDotnetProtoType.Sfixed64:
                        type = "integer";
                        format = "sfixed64";
                        break;
                    case ProtobufDotnetProtoType.Enum:
                        type = "string";
                        enumValues = new List<IOpenApiAny>(field.EnumType.Values.Count);
                        foreach (var e in field.EnumType.Values)
                        {
                            var enumVal = e.DuckCast<IDescriptorProxy>()!;
                            enumValues.Add(new OpenApiString(enumVal.Name));
                            _computedHash = FnvHash64.GenerateHash(enumVal.Name, FnvHash64.Version.V1A, _computedHash);
                        }

                        break;
                    default:
                        // OpenAPI does not have a direct mapping for unknown types, usually treated as strings or
                        // omitted
                        type = "string";
                        description = "Unknown type";
                        break;
                }

                _computedHash = FnvHash64.GenerateHash(field.FieldNumber.ToString(CultureInfo.InvariantCulture), FnvHash64.Version.V1A, _computedHash);
                _computedHash = FnvHash64.GenerateHash(((int)protoType).ToString(CultureInfo.InvariantCulture), FnvHash64.Version.V1A, _computedHash);
                _computedHash = FnvHash64.GenerateHash(depth.ToString(CultureInfo.InvariantCulture), FnvHash64.Version.V1A, _computedHash);

                var property = new OpenApiSchema
                {
                    Type = type,
                    Description = description,
                    Reference = reference,
                    Format = format,
                    Enum = enumValues,
                };
                if (field.IsRepeated)
                {
                    // note: maps are seen as arrays of auto-generated XxxEntry type
                    property = new OpenApiSchema { Type = "array", Items = property };
                }

                property.Extensions = new Dictionary<string, IOpenApiExtension> { { "x-protobuf-number", new OpenApiInteger(field.FieldNumber) } };

                properties.Add(fieldName, property);
                _propertiesCount++;
            }

            return properties;
        }
    }

    private sealed class Schema
    {
        public Schema(OpenApiDocument openApiDoc, ulong hash)
        {
            using var writer = new StringWriter();
            try
            {
                openApiDoc.SerializeAsV3(new OpenApiJsonWriter(writer, new OpenApiJsonWriterSettings { Terse = true /* no pretty print */ }));
                JsonDefinition = writer.ToString();
            }
            catch (Exception e)
            {
                // Happens if some mandatory elements of the OpenAPI definition are missing for instance
                JsonDefinition = string.Empty;
                Log.Warning(e, "Error while writing protobuf schema to JSON, stopped after {PartialJson}", writer.ToString());
            }

            Hash = hash;
        }

        internal string JsonDefinition { get; }

        internal ulong Hash { get; }
    }
}
