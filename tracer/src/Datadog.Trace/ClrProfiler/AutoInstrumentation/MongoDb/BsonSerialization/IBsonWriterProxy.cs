// <copyright file="IBsonWriterProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Duck Typing interface proxy for: https://github.com/mongodb/mongo-csharp-driver/blob/v2.8.x/src/MongoDB.Bson/IO/IBsonWriter.cs
/// </summary>
internal interface IBsonWriterProxy : IDuckType
{
    // properties

    /// <summary>
    /// Gets the position.
    /// Not all writers are able to report the position. Those that can't simply return zero.
    /// </summary>
    /// <value>
    /// The position.
    /// </value>
    long Position { get; }

    /// <summary>
    /// Gets the current serialization depth.
    /// </summary>
    int SerializationDepth { get; }

    // object Settings { get; } <- Reminder

    /// <summary>
    /// Gets the current state of the writer.
    /// </summary>
    object State { get; }

    // methods

    /// <summary>
    /// Closes the writer.
    /// </summary>
    void Close();

    /// <summary>
    /// Flushes any pending data to the output destination.
    /// </summary>
    void Flush();

    /// <summary>
    /// Pops the element name validator.
    /// </summary>
    void PopElementNameValidator();

    /// <summary>
    /// Pops the settings.
    /// </summary>
    void PopSettings();

    /// <summary>
    /// Pushes the element name validator.
    /// </summary>
    /// <param name="validator">The validator.</param>
    void PushElementNameValidator(object validator);

    /// <summary>
    /// Pushes new settings for the writer.
    /// </summary>
    /// <param name="configurator">The settings configurator.</param>
    void PushSettings(object configurator);

    /// <summary>
    /// Writes BSON binary data to the writer.
    /// </summary>
    /// <param name="binaryData">The binary data.</param>
    void WriteBinaryData(object binaryData);

    /// <summary>
    /// Writes a BSON Boolean to the writer.
    /// </summary>
    /// <param name="value">The Boolean value.</param>
    void WriteBoolean(bool value);

    /// <summary>
    /// Writes BSON binary data to the writer.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    void WriteBytes(byte[] bytes);

    /// <summary>
    /// Writes a BSON DateTime to the writer.
    /// </summary>
    /// <param name="value">The number of milliseconds since the Unix epoch.</param>
    void WriteDateTime(long value);

    /// <summary>
    /// Writes a BSON Decimal128 to the writer.
    /// </summary>
    /// <param name="value">The <c>MongoDB.Bson.Decimal128</c>.</param>
    void WriteDecimal128(object value);

    /// <summary>
    /// Writes a BSON Double to the writer.
    /// </summary>
    /// <param name="value">The Double value.</param>
    void WriteDouble(double value);

    /// <summary>
    /// Writes the end of a BSON array to the writer.
    /// </summary>
    void WriteEndArray();

    /// <summary>
    /// Writes the end of a BSON document to the writer.
    /// </summary>
    void WriteEndDocument();

    /// <summary>
    /// Writes a BSON Int32 to the writer.
    /// </summary>
    /// <param name="value">The Int32 value.</param>
    void WriteInt32(int value);

    /// <summary>
    /// Writes a BSON Int64 to the writer.
    /// </summary>
    /// <param name="value">The Int64 value.</param>
    void WriteInt64(long value);

    /// <summary>
    /// Writes a BSON JavaScript to the writer.
    /// </summary>
    /// <param name="code">The JavaScript code.</param>
    void WriteJavaScript(string code);

    /// <summary>
    /// Writes a BSON JavaScript to the writer (call WriteStartDocument to start writing the scope).
    /// </summary>
    /// <param name="code">The JavaScript code.</param>
    void WriteJavaScriptWithScope(string code);

    /// <summary>
    /// Writes a BSON MaxKey to the writer.
    /// </summary>
    void WriteMaxKey();

    /// <summary>
    /// Writes a BSON MinKey to the writer.
    /// </summary>
    void WriteMinKey();

    /// <summary>
    /// Writes the name of an element to the writer.
    /// </summary>
    /// <param name="name">The name of the element.</param>
    void WriteName(string name);

    /// <summary>
    /// Writes a BSON null to the writer.
    /// </summary>
    void WriteNull();

    /// <summary>
    /// Writes a BSON ObjectId to the writer.
    /// </summary>
    /// <param name="objectId">The ObjectId.</param>
    void WriteObjectId(object objectId);

    /// <summary>
    /// Writes a raw BSON array.
    /// </summary>
    /// <param name="slice">The byte buffer containing the raw BSON array.</param>
    void WriteRawBsonArray(object slice);

    /// <summary>
    /// Writes a raw BSON document.
    /// </summary>
    /// <param name="slice">The byte buffer containing the raw BSON document.</param>
    void WriteRawBsonDocument(object slice);

    /// <summary>
    /// Writes a BSON regular expression to the writer.
    /// </summary>
    /// <param name="regex">A BsonRegularExpression.</param>
    void WriteRegularExpression(object regex);

    /// <summary>
    /// Writes the start of a BSON array to the writer.
    /// </summary>
    void WriteStartArray();

    /// <summary>
    /// Writes the start of a BSON document to the writer.
    /// </summary>
    void WriteStartDocument();

    /// <summary>
    /// Writes a BSON String to the writer.
    /// </summary>
    /// <param name="value">The String value.</param>
    void WriteString(string value);

    /// <summary>
    /// Writes a BSON Symbol to the writer.
    /// </summary>
    /// <param name="value">The symbol.</param>
    void WriteSymbol(string value);

    /// <summary>
    /// Writes a BSON timestamp to the writer.
    /// </summary>
    /// <param name="value">The combined timestamp/increment value.</param>
    void WriteTimestamp(long value);

    /// <summary>
    /// Writes a BSON undefined to the writer.
    /// </summary>
    void WriteUndefined();

    /// <summary>
    /// Disposes of any resources used by the writer.
    /// </summary>
    void Dispose();
}
