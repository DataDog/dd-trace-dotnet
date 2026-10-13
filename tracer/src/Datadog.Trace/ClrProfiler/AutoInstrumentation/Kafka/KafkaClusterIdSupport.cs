// <copyright file="KafkaClusterIdSupport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal static class KafkaClusterIdSupport
{
    // DescribeCluster was added in librdkafka 2.3.0. The low byte is 0xff for a stable release.
    private const int MinimumNativeVersion = 0x020300ff;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(KafkaClusterIdSupport));

    // Assembly identity matters when clients use different load contexts. Weak keys avoid retaining them,
    // and Lazy ensures concurrent constructors query the native library only once per assembly.
    private static readonly ConditionalWeakTable<Assembly, Lazy<Type?>> OptionsTypes = new();

    public static Type? GetDescribeClusterOptionsType(Assembly kafkaAssembly) =>
        OptionsTypes.GetValue(kafkaAssembly, static assembly => new Lazy<Type?>(() => GetSupportedOptionsType(assembly))).Value;

    private static Type? GetSupportedOptionsType(Assembly kafkaAssembly)
    {
        try
        {
            var optionsType = kafkaAssembly.GetType("Confluent.Kafka.Admin.DescribeClusterOptions");
            if (optionsType is null)
            {
                Log.Debug("Skipping Kafka cluster_id discovery: Confluent.Kafka >= 2.3.0 is required");
                return null;
            }

            // Query the library already initialized by this client's successful constructor. The managed
            // package version does not identify the native DLL actually loaded by the application.
            var libraryType = kafkaAssembly.GetType("Confluent.Kafka.Library");
            var versionProperty = libraryType?.GetProperty("Version", BindingFlags.Public | BindingFlags.Static);
            if (versionProperty?.PropertyType != typeof(int) || versionProperty.GetValue(null) is not int nativeVersion)
            {
                Log.Error("Unable to determine the loaded librdkafka version: expected Confluent.Kafka.Library.Version to be a public static Int32 property. Skipping Kafka cluster_id discovery");
                return null;
            }

            if (nativeVersion < MinimumNativeVersion)
            {
                // Older libraries can return a null AdminOptions pointer for DescribeCluster, causing
                // an access violation before Confluent.Kafka attempts to call the missing native API.
                Log.Debug<int>("Skipping Kafka cluster_id discovery: loaded librdkafka version 0x{NativeVersion:x8} is below the required 2.3.0", nativeVersion);
                return null;
            }

            return optionsType;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Skipping Kafka cluster_id discovery: the loaded librdkafka version could not be read");
            return null;
        }
    }
}
