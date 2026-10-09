// <copyright file="IAmazonDynamoDbRequestWithTableName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb;

/// <summary>
/// Interface for duck typing AmazonDynamoDbRequest implementations with the TableName property
/// </summary>
[DuckType("Amazon.DynamoDBv2.Model.GetItemRequest", "AWSSDK.DynamoDBv2")]
[DuckType("Amazon.DynamoDBv2.Model.PutItemRequest", "AWSSDK.DynamoDBv2")]
internal interface IAmazonDynamoDbRequestWithTableName
{
    /// <summary>
    /// Gets the Name of the Table
    /// Should never be null, but there's no guards, so _could_ be
    /// </summary>
    string? TableName { get; }
}
