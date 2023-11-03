// <copyright file="IBsonSerializerLookupProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Proxy for static class https://github.com/mongodb/mongo-csharp-driver/blob/master/src/MongoDB.Bson/Serialization/BsonSerializer.cs
/// </summary>
internal interface IBsonSerializerLookupProxy
{
    // static lookup function
    // LookupSerializer _always_ returns non-null (it throws otherwise), so the duck type will be non null
    IBsonSerializerProxy LookupSerializer(Type type);
}
