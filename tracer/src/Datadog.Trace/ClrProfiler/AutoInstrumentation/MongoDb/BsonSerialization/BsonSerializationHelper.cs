// <copyright file="BsonSerializationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Reflection.Emit;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

internal static class BsonSerializationHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BsonSerializationHelper));
    private static readonly BsonHelper? Helper;

    static BsonSerializationHelper()
    {
        try
        {
            Helper = BsonHelper.Create();
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error creating BsonHelper");
            Helper = null;
        }
    }

    /// <summary>
    /// Serializes a bson document object, stripping out binary data, and truncating
    /// at the maximum number of characters accepted in tags
    /// </summary>
    public static string? ToShortString(object? obj)
    {
        if (obj is null)
        {
            return null;
        }

        if (Helper is not { } helper)
        {
            // Fallback if there was an issue creating the proxies in the constructor
            return obj.ToString();
        }

        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        try
        {
            SerializeWithCustomWriter(obj, sb, helper);
            return StringBuilderCache.GetStringAndRelease(sb);
        }
        catch (Exception ex)
        {
            StringBuilderCache.Release(sb);
            Log.Error(ex, "Error during custom BSON serialization");
            // Fallback to default
            return obj.ToString();
        }
    }

    private static void SerializeWithCustomWriter(object obj, StringBuilder sb, BsonHelper helper)
    {
        using var stringWriter = new TruncatedTextWriter(sb);

        // Create a "real" JsonWriter
        var jsonWriterSettings = helper.JsonWriterSettingsProxy.Defaults;
        var jsonWriter = helper.CreateJsonWriterFunc(stringWriter, jsonWriterSettings).DuckCast<IBsonWriterProxy>();

        // Wrap the real writer with our custom proxy that has extra behaviours
        var customBsonWriter = new MongoBsonWriter(jsonWriter, jsonWriterSettings);
        var customWriterProxy = customBsonWriter.DuckImplement(helper.IBsonWriterType);

        // Find the serializer and serializer
        var nominalType = obj.GetType();
        var serializer = helper.BsonSerializerLookupProxy.LookupSerializer(nominalType);
        var rootContext = helper.BsonSerializationContextProxy.CreateRoot(customWriterProxy, null);
        var bsonSerializationArgs = helper.CreateBsonSerializationArgsFunc(nominalType);
        serializer.Serialize(rootContext, bsonSerializationArgs, obj);

        stringWriter.Flush();
    }

    private class BsonHelper
    {
#pragma warning disable SA1401 // Field should be private - avoiding copying of struct proxies
        internal readonly IBsonSerializationContextProxy BsonSerializationContextProxy;
        internal readonly JsonWriterSettingsProxy JsonWriterSettingsProxy;
        internal readonly IBsonSerializerLookupProxy BsonSerializerLookupProxy;
        internal readonly Func<TextWriter, object, object> CreateJsonWriterFunc;
        internal readonly Func<Type, object> CreateBsonSerializationArgsFunc;
        internal readonly Type IBsonWriterType;

        private BsonHelper(
            IBsonSerializationContextProxy bsonSerializationContextProxy,
            JsonWriterSettingsProxy jsonWriterSettingsProxy,
            IBsonSerializerLookupProxy bsonSerializerLookupProxy,
            Func<TextWriter, object, object> createJsonWriterFunc,
            Func<Type, object> createBsonSerializationArgsFunc,
            Type ibsonWriterType)
        {
            BsonSerializationContextProxy = bsonSerializationContextProxy;
            JsonWriterSettingsProxy = jsonWriterSettingsProxy;
            BsonSerializerLookupProxy = bsonSerializerLookupProxy;
            CreateJsonWriterFunc = createJsonWriterFunc;
            CreateBsonSerializationArgsFunc = createBsonSerializationArgsFunc;
            IBsonWriterType = ibsonWriterType;
        }

        public static BsonHelper? Create()
        {
            // Resolving these types once on app startup.
            // If any of them are null, we have a problem, and can't use the
            // custom serializer. We would _like_ to do this work using a marker
            // type, similar to the way we do in CachedMessageHeadersHelper, but
            // we don't have generic access to the MongoDB.Bson assembly (the types
            // we instrument and their args are in a different assembly) so this is
            // the best we can do.
            var bsonSerializationArgsType = Type.GetType("MongoDB.Bson.Serialization.BsonSerializationArgs, MongoDB.Bson", false);
            if (bsonSerializationArgsType is null)
            {
                Log.Information("Error creating BsonHelper, could not find MongoDB.Bson.Serialization.BsonSerializationArgs type");
                return null;
            }

            var assembly = bsonSerializationArgsType.Assembly;

            var bsonSerializerType = assembly.GetType("MongoDB.Bson.Serialization.BsonSerializer", throwOnError: false);
            if (bsonSerializerType is null)
            {
                Log.Information("Error creating BsonHelper, could not find MongoDB.Bson.Serialization.BsonSerializer type");
                return null;
            }

            var bsonSerializationContextType = assembly.GetType("MongoDB.Bson.Serialization.BsonSerializationContext", throwOnError: false);
            if (bsonSerializationContextType is null)
            {
                Log.Information("Error creating BsonHelper, could not find MongoDB.Bson.Serialization.BsonSerializationContext type");
                return null;
            }

            var jsonWriterSettingsType = assembly.GetType("MongoDB.Bson.IO.JsonWriterSettings", throwOnError: false);
            if (jsonWriterSettingsType is null)
            {
                Log.Information("Error creating BsonHelper, could not find MongoDB.Bson.IO.JsonWriterSettings type");
                return null;
            }

            var jsonWriterType = assembly.GetType("MongoDB.Bson.IO.JsonWriter", throwOnError: false);
            if (jsonWriterType is null)
            {
                Log.Information("Error creating BsonHelper, could not find MongoDB.Bson.IO.JsonWriter type");
                return null;
            }

            var ibsonWriterType = assembly.GetType("MongoDB.Bson.IO.IBsonWriter", throwOnError: false);
            if (ibsonWriterType is null)
            {
                Log.Information("Error creating BsonHelper, could not find MongoDB.Bson.IO.IBsonWriter type");
                return null;
            }

            // We found all the required types, now try to create the proxies/activators
            var proxyResult = DuckType.GetOrCreateProxyType(typeof(IBsonSerializerLookupProxy), bsonSerializerType);
            if (!proxyResult.Success)
            {
                return null;
            }

            var bsonSerializerLookupProxy = (IBsonSerializerLookupProxy)proxyResult.CreateInstance(null!);

            proxyResult = DuckType.GetOrCreateProxyType(typeof(IBsonSerializationContextProxy), bsonSerializationContextType);
            if (!proxyResult.Success)
            {
                return null;
            }

            var bsonSerializationContextProxy = (IBsonSerializationContextProxy)proxyResult.CreateInstance(null!);

            proxyResult = DuckType.GetOrCreateProxyType(typeof(JsonWriterSettingsProxy), jsonWriterSettingsType);
            if (!proxyResult.Success)
            {
                return null;
            }

            var jsonWriterSettingsProxy = (JsonWriterSettingsProxy)proxyResult.CreateInstance(null!);

            // Create JSonWriter
            var jsonWriterCtor = jsonWriterType.GetConstructor(new[] { typeof(TextWriter), jsonWriterSettingsType })!;

            DynamicMethod createJsonWriterMethod = new DynamicMethod(
                $"MongoJsonWriterSerializer",
                jsonWriterType,
                parameterTypes: new[] { typeof(TextWriter), typeof(object) },
                typeof(DuckType).Module,
                true);

            ILGenerator createJsonWriterIl = createJsonWriterMethod.GetILGenerator();
            createJsonWriterIl.Emit(OpCodes.Ldarg_0);
            createJsonWriterIl.Emit(OpCodes.Ldarg_1);
            // createJsonWriterIl.Emit(OpCodes.Castclass, jsonWriterSettingsType); // Not technically necessary
            createJsonWriterIl.Emit(OpCodes.Newobj, jsonWriterCtor);
            createJsonWriterIl.Emit(OpCodes.Ret);

            var createJsonWriterFunc = (Func<TextWriter, object, object>)createJsonWriterMethod.CreateDelegate(typeof(Func<TextWriter, object, object>));

            var bsonSerializationArgsCtor = bsonSerializationArgsType.GetConstructor(new[] { typeof(Type), typeof(bool), typeof(bool) })!;

            // Create BsonSerializationArgs
            DynamicMethod createBsonSerializationArgs = new DynamicMethod(
                $"MongoBsonSerializationArgsType",
                typeof(object), // boxed bsonSerializationArgsType
                parameterTypes: new[] { typeof(Type) },
                typeof(DuckType).Module,
                true);

            ILGenerator bsonWriterIl = createBsonSerializationArgs.GetILGenerator();
            bsonWriterIl.Emit(OpCodes.Ldarg_0);
            bsonWriterIl.Emit(OpCodes.Ldc_I4_0);
            bsonWriterIl.Emit(OpCodes.Ldc_I4_0);
            bsonWriterIl.Emit(OpCodes.Newobj, bsonSerializationArgsCtor);
            bsonWriterIl.Emit(OpCodes.Box, bsonSerializationArgsType);
            bsonWriterIl.Emit(OpCodes.Ret);

            var createBsonSerializationArgsFunc = (Func<Type, object>)createBsonSerializationArgs.CreateDelegate(typeof(Func<Type, object>));

            var helper = new BsonHelper(
                bsonSerializationContextProxy: bsonSerializationContextProxy,
                jsonWriterSettingsProxy: jsonWriterSettingsProxy,
                bsonSerializerLookupProxy: bsonSerializerLookupProxy,
                createJsonWriterFunc: createJsonWriterFunc,
                createBsonSerializationArgsFunc: createBsonSerializationArgsFunc,
                ibsonWriterType: ibsonWriterType);

            // smoke test - we don't verify the Duck types until we actually try to create a proxy,
            // so we do it once here, just to confirm that it will work later
            try
            {
                SerializeWithCustomWriter(string.Empty, new StringBuilder(1), helper);
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Error creating BsonHelper - execution error");
                return null;
            }

            return helper;
        }
    }
}
