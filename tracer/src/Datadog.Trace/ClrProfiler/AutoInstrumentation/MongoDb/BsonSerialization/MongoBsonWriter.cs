// <copyright file="MongoBsonWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

#pragma warning disable CS1591

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Duck Typing interface proxy for: https://github.com/mongodb/mongo-csharp-driver/blob/v2.8.x/src/MongoDB.Bson/IO/IBsonWriter.cs
/// </summary>
internal class MongoBsonWriter
{
    private readonly IBsonWriterProxy _bsonWriterProxy;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoBsonWriter"/> class.
    /// </summary>
    /// <param name="bsonWriterProxy">Current instance of IBsonWriterProxy</param>
    /// <param name="jsonWriterSettings">The settings object passed to <paramref name="bsonWriterProxy"/> - used to work around JsonWriter implementation
    /// That uses method hiding of Settings property</param>
    public MongoBsonWriter(IBsonWriterProxy bsonWriterProxy, object jsonWriterSettings)
    {
        _bsonWriterProxy = bsonWriterProxy;
        Settings = jsonWriterSettings;
    }

    [DuckReverseMethod]
    public long Position => _bsonWriterProxy.Position;

    [DuckReverseMethod]
    public int SerializationDepth => _bsonWriterProxy.SerializationDepth;

    [DuckReverseMethod]
    public object Settings { get; }

    [DuckReverseMethod]
    public object State => _bsonWriterProxy.State;

    [DuckReverseMethod]
    public void Close()
    {
        _bsonWriterProxy.Close();
    }

    [DuckReverseMethod]
    public void Flush()
    {
        _bsonWriterProxy.Flush();
    }

    [DuckReverseMethod]
    public void PopElementNameValidator()
    {
        _bsonWriterProxy.PopElementNameValidator();
    }

    [DuckReverseMethod]
    public void PopSettings()
    {
        _bsonWriterProxy.PopSettings();
    }

    [DuckReverseMethod]
    public void PushElementNameValidator(object validator)
    {
        _bsonWriterProxy.PushElementNameValidator(validator);
    }

    [DuckReverseMethod]
    public void PushSettings(object configurator)
    {
        _bsonWriterProxy.PushSettings(configurator);
    }

    [DuckReverseMethod]
    public void WriteBinaryData(IBsonBinaryDataProxy binaryData)
    {
        // instead of writing bytes, we write a string, "<binary>", to avoid writing very large data
        // however, we need to make sure _not_ to intercept _other_ binary data like "Guids"
        // _bsonWriterProxy.WriteBytes(bytes);
        switch (binaryData.SubType)
        {
            case 0x03: // UuidLegacy
            case 0x04: // UuidStandard
                // keep outputting these
                _bsonWriterProxy.WriteBinaryData(binaryData.Instance);
                break;
            default:
                _bsonWriterProxy.WriteString("<Truncated>");
                break;
        }
    }

    [DuckReverseMethod]
    public void WriteBoolean(bool value)
    {
        _bsonWriterProxy.WriteBoolean(value);
    }

    [DuckReverseMethod]
    public void WriteBytes(byte[] bytes)
    {
        _bsonWriterProxy.WriteBytes(bytes);
    }

    [DuckReverseMethod]
    public void WriteDateTime(long value)
    {
        _bsonWriterProxy.WriteDateTime(value);
    }

    [DuckReverseMethod]
    public void WriteDecimal128(object value)
    {
        _bsonWriterProxy.WriteDecimal128(value);
    }

    [DuckReverseMethod]
    public void WriteDouble(double value)
    {
        _bsonWriterProxy.WriteDouble(value);
    }

    [DuckReverseMethod]
    public void WriteEndArray()
    {
        _bsonWriterProxy.WriteEndArray();
    }

    [DuckReverseMethod]
    public void WriteEndDocument()
    {
        _bsonWriterProxy.WriteEndDocument();
    }

    [DuckReverseMethod]
    public void WriteInt32(int value)
    {
        _bsonWriterProxy.WriteInt32(value);
    }

    [DuckReverseMethod]
    public void WriteInt64(long value)
    {
        _bsonWriterProxy.WriteInt64(value);
    }

    [DuckReverseMethod]
    public void WriteJavaScript(string code)
    {
        _bsonWriterProxy.WriteJavaScript(code);
    }

    [DuckReverseMethod]
    public void WriteJavaScriptWithScope(string code)
    {
        _bsonWriterProxy.WriteJavaScriptWithScope(code);
    }

    [DuckReverseMethod]
    public void WriteMaxKey()
    {
        _bsonWriterProxy.WriteMaxKey();
    }

    [DuckReverseMethod]
    public void WriteMinKey()
    {
        _bsonWriterProxy.WriteMinKey();
    }

    [DuckReverseMethod]
    public void WriteName(string name)
    {
        _bsonWriterProxy.WriteName(name);
    }

    [DuckReverseMethod]
    public void WriteNull()
    {
        _bsonWriterProxy.WriteNull();
    }

    [DuckReverseMethod]
    public void WriteObjectId(object objectId)
    {
        _bsonWriterProxy.WriteObjectId(objectId);
    }

    [DuckReverseMethod]
    public void WriteRawBsonArray(object slice)
    {
        _bsonWriterProxy.WriteRawBsonArray(slice);
    }

    [DuckReverseMethod]
    public void WriteRawBsonDocument(object slice)
    {
        _bsonWriterProxy.WriteRawBsonDocument(slice);
    }

    [DuckReverseMethod]
    public void WriteRegularExpression(object regex)
    {
        _bsonWriterProxy.WriteRegularExpression(regex);
    }

    [DuckReverseMethod]
    public void WriteStartArray()
    {
        _bsonWriterProxy.WriteStartArray();
    }

    [DuckReverseMethod]
    public void WriteStartDocument()
    {
        _bsonWriterProxy.WriteStartDocument();
    }

    [DuckReverseMethod]
    public void WriteString(string value)
    {
        _bsonWriterProxy.WriteString(value);
    }

    [DuckReverseMethod]
    public void WriteSymbol(string value)
    {
        _bsonWriterProxy.WriteSymbol(value);
    }

    [DuckReverseMethod]
    public void WriteTimestamp(long value)
    {
        _bsonWriterProxy.WriteTimestamp(value);
    }

    [DuckReverseMethod]
    public void WriteUndefined()
    {
        _bsonWriterProxy.WriteUndefined();
    }

    [DuckReverseMethod]
    public void Dispose()
    {
        _bsonWriterProxy.Dispose();
    }
}
