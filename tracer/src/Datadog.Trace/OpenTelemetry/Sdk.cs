// <copyright file="Sdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.OpenTelemetry
{
    internal static class Sdk
    {
        public static void Initialize()
        {
            // Initialize no-op OpenTelemetry API implementations that the OpenTelemetry SDK would typically overwrite
            // Note: Since this initialization is run near the time of application startup, any further updates by the
            //       OpenTelemetry SDK or user should overwrite these settings
            var propagatorsType = Type.GetType("OpenTelemetry.Context.Propagation.Propagators, OpenTelemetry.Api", throwOnError: false);
            if (propagatorsType is not null
                && propagatorsType.Assembly.GetName().Version >= new Version(1, 0, 0))
            {
                var defaultTextMapPropagatorProperty = propagatorsType.GetProperty("DefaultTextMapPropagator", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var setMethod = defaultTextMapPropagatorProperty?.GetSetMethod(true);

                var textMapPropagatorType = Type.GetType("OpenTelemetry.Context.Propagation.TextMapPropagator, OpenTelemetry.Api", throwOnError: false);
                var traceContextPropagatorType = Type.GetType("OpenTelemetry.Context.Propagation.TraceContextPropagator, OpenTelemetry.Api", throwOnError: false);
                var baggagePropagatorType = Type.GetType("OpenTelemetry.Context.Propagation.BaggagePropagator, OpenTelemetry.Api", throwOnError: false);
                var compositeTextMapPropagatorType = Type.GetType("OpenTelemetry.Context.Propagation.CompositeTextMapPropagator, OpenTelemetry.Api", throwOnError: false);

                if (setMethod is not null
                    && textMapPropagatorType is not null
                    && traceContextPropagatorType is not null
                    && baggagePropagatorType is not null
                    && compositeTextMapPropagatorType is not null)
                {
                    var propagatorsArray = Array.CreateInstance(textMapPropagatorType, 2);
                    propagatorsArray.SetValue(Activator.CreateInstance(traceContextPropagatorType), 0);
                    propagatorsArray.SetValue(Activator.CreateInstance(baggagePropagatorType), 1);

                    if (Activator.CreateInstance(compositeTextMapPropagatorType, propagatorsArray) is { } compositePropagator)
                    {
                        setMethod.Invoke(null, new object[] { compositePropagator });
                    }
                }
            }
        }
    }
}
