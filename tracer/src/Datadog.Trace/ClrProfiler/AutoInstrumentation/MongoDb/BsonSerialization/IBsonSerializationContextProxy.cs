// <copyright file="IBsonSerializationContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Proxy for calling static methods on https://github.com/mongodb/mongo-csharp-driver/blob/master/src/MongoDB.Bson/Serialization/BsonSerializationContext.cs
/// </summary>
internal interface IBsonSerializationContextProxy
{
    object CreateRoot(object proxy, object? configurator);
}
