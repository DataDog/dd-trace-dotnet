// <copyright file="IAmazonDynamoDbRequestWithKnownKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.IO;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb;

/// <summary>
/// Interface for duck typing AmazonDynamoDbRequest implementations with the TableName and Key properties.
/// https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Services/DynamoDBv2/Generated/Model/UpdateItemRequest.cs
/// https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Services/DynamoDBv2/Generated/Model/DeleteItemRequest.cs
/// </summary>
internal interface IAmazonDynamoDbRequestWithKnownKeys
{
    /// <summary>
    /// Gets the name of the table.
    /// Should never be null, but there's no guards, so _could_ be
    /// </summary>
    [DuckField(Name = "_tableName")]
    string? TableName { get; }

    /// <summary>
    /// Gets the keys of the modified item of type
    /// System.Collections.Generic.Dictionary`2[System.String,Amazon.DynamoDBv2.Model.AttributeValue].
    /// Should never be null, but there's no guards, so _could_ be
    /// </summary>
    [DuckField(Name = "_key")]
    object? Keys { get; }
}

/// <summary>
/// Interface for duck typing DynamoDB keys collection with indexer.
/// </summary>
internal interface IDynamoDbKeysObject : IDuckType
{
    /// <summary>
    /// Gets the collection of key names.
    /// </summary>
    [Duck(Name = "Keys")]
    IEnumerable<string>? KeyNames { get; }

    /// <summary>
    /// Gets the attribute value for the specified key.
    /// </summary>
    IDynamoDbAttributeValue this[string key] { get; }
}

/// <summary>
/// Interface for duck typing DynamoDB attribute values
/// </summary>
internal interface IDynamoDbAttributeValue : IDuckType
{
    /// <summary>
    /// Gets the string value, if present
    /// </summary>
    string? S { get; }

    /// <summary>
    /// Gets the numeric value as a string, if present
    /// </summary>
    string? N { get; }

    /// <summary>
    /// Gets the binary value, if present
    /// </summary>
    MemoryStream? B { get; }
}
