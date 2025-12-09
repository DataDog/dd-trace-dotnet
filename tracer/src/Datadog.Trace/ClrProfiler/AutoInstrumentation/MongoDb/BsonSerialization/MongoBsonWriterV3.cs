// <copyright file="MongoBsonWriterV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Reverse duck Typing interface proxy for: https://github.com/mongodb/mongo-csharp-driver/blob/5edf5ba9941f170ecc6956005398a8736f12e38a/src/MongoDB.Bson/IO/IBsonWriter.cs
/// </summary>
internal sealed class MongoBsonWriterV3
{
    private readonly IBsonWriterProxyV3 _bsonWriterProxy;
    private readonly MongoBsonWriter _innerWriter;

    public MongoBsonWriterV3(IBsonWriterProxyV3 bsonWriterProxy, object jsonWriterSettings)
    {
        _bsonWriterProxy = bsonWriterProxy;
        // To avoid duplicating implementation, we delegate to the existing v2 implementation for most of the behaviour
        _innerWriter = new MongoBsonWriter(bsonWriterProxy, jsonWriterSettings);
    }

    [DuckReverseMethod]
    public long Position => _innerWriter.Position;

    [DuckReverseMethod]
    public int SerializationDepth => _innerWriter.SerializationDepth;

    [DuckReverseMethod]
    public object State => _innerWriter.State;

    [DuckReverseMethod]
    public object Settings => _innerWriter.Settings;

    // These are the only two v3-specific methods
    [DuckReverseMethod]
    public void WriteGuid(Guid guid) => _bsonWriterProxy.WriteGuid(guid);

    [DuckReverseMethod(ParameterTypeNames = [ClrNames.Guid, "MongoDB.Bson.GuidRepresentation"])]
    public void WriteGuid(Guid guid, int guidRepresentation) => _bsonWriterProxy.WriteGuid(guid);

    // The rest of these are wrappers around the v2 behaviour, just to avoid duplication
    [DuckReverseMethod]
    public void Close() => _innerWriter.Close();

    [DuckReverseMethod]
    public void Flush() => _innerWriter.Flush();

    [DuckReverseMethod]
    public void PopElementNameValidator() => _innerWriter.PopElementNameValidator();

    [DuckReverseMethod]
    public void PopSettings() => _innerWriter.PopSettings();

    [DuckReverseMethod]
    public void PushElementNameValidator(object validator) => _innerWriter.PushElementNameValidator(validator);

    [DuckReverseMethod]
    public void PushSettings(object configurator) => _innerWriter.PushSettings(configurator);

    [DuckReverseMethod]
    public void WriteBinaryData(IBsonBinaryDataProxy binaryData) => _innerWriter.WriteBinaryData(binaryData);

    [DuckReverseMethod]
    public void WriteBoolean(bool value) => _innerWriter.WriteBoolean(value);

    [DuckReverseMethod]
    public void WriteBytes(byte[] bytes) => _innerWriter.WriteBytes(bytes);

    [DuckReverseMethod]
    public void WriteDateTime(long value) => _innerWriter.WriteDateTime(value);

    [DuckReverseMethod]
    public void WriteDecimal128(object value) => _innerWriter.WriteDecimal128(value);

    [DuckReverseMethod]
    public void WriteDouble(double value) => _innerWriter.WriteDouble(value);

    [DuckReverseMethod]
    public void WriteEndArray() => _innerWriter.WriteEndArray();

    [DuckReverseMethod]
    public void WriteEndDocument() => _innerWriter.WriteEndDocument();

    [DuckReverseMethod]
    public void WriteInt32(int value) => _innerWriter.WriteInt32(value);

    [DuckReverseMethod]
    public void WriteInt64(long value) => _innerWriter.WriteInt64(value);

    [DuckReverseMethod]
    public void WriteJavaScript(string code) => _innerWriter.WriteJavaScript(code);

    [DuckReverseMethod]
    public void WriteJavaScriptWithScope(string code) => _innerWriter.WriteJavaScriptWithScope(code);

    [DuckReverseMethod]
    public void WriteMaxKey() => _innerWriter.WriteMaxKey();

    [DuckReverseMethod]
    public void WriteMinKey() => _innerWriter.WriteMinKey();

    [DuckReverseMethod]
    public void WriteName(string name) => _innerWriter.WriteName(name);

    [DuckReverseMethod]
    public void WriteNull() => _innerWriter.WriteNull();

    [DuckReverseMethod]
    public void WriteObjectId(object objectId) => _innerWriter.WriteObjectId(objectId);

    [DuckReverseMethod]
    public void WriteRawBsonArray(object slice) => _innerWriter.WriteRawBsonArray(slice);

    [DuckReverseMethod]
    public void WriteRawBsonDocument(object slice) => _innerWriter.WriteRawBsonDocument(slice);

    [DuckReverseMethod]
    public void WriteRegularExpression(object regex) => _innerWriter.WriteRegularExpression(regex);

    [DuckReverseMethod]
    public void WriteStartArray() => _innerWriter.WriteStartArray();

    [DuckReverseMethod]
    public void WriteStartDocument() => _innerWriter.WriteStartDocument();

    [DuckReverseMethod]
    public void WriteString(string value) => _innerWriter.WriteString(value);

    [DuckReverseMethod]
    public void WriteSymbol(string value) => _innerWriter.WriteSymbol(value);

    [DuckReverseMethod]
    public void WriteTimestamp(long value) => _innerWriter.WriteTimestamp(value);

    [DuckReverseMethod]
    public void WriteUndefined() => _innerWriter.WriteUndefined();

    [DuckReverseMethod]
    public void Dispose() => _innerWriter.Dispose();
}
