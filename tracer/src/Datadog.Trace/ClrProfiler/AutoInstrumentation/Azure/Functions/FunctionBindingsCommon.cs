// <copyright file="FunctionBindingsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Json;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class FunctionBindingsCommon
    {
        private const string BindingsFeatureTypeName = "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FunctionBindingsCommon));

        internal static FunctionBindingsFeatureStruct? GetBindingsFeature<T>(T context)
            where T : IFunctionContext
        {
            if (context.Features is null)
            {
                return null;
            }

            foreach (var feature in context.Features)
            {
                if (feature.Key.FullName == BindingsFeatureTypeName)
                {
                    return feature.Value?.TryDuckCast<FunctionBindingsFeatureStruct>(out var bindingsFeature) == true ? bindingsFeature : null;
                }
            }

            return null;
        }

        internal static bool TryParseJson<T>(object? jsonObject, [NotNullWhen(true)] out T? result)
            where T : class
        {
            result = null;
            if (jsonObject is not string jsonString)
            {
                return false;
            }

            try
            {
                result = JsonHelper.DeserializeObject<T>(jsonString);
                return result is not null;
            }
            catch (Exception ex)
            {
                Log.Debug<int>(ex, "Failed to parse JSON payload with length {PayloadLength}", jsonString.Length);
                return false;
            }
        }
    }
}

#endif
