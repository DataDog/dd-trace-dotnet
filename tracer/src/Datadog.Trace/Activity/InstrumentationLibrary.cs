// <copyright file="InstrumentationLibrary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal class InstrumentationLibrary
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InstrumentationLibrary));

        private static readonly InstrumentationLibrary[] Registry =
            [
                // Package: OpenTelemetry.Instrumentation.Hangfire, Versions: v1.6.0-beta.1+
                new("OpenTelemetry.Instrumentation.Hangfire", "OpenTelemetry.Instrumentation.Hangfire.Implementation.HangfireInstrumentation", "OpenTelemetry.Trace.HangfireInstrumentationOptions")
            ];

        private InstrumentationLibrary(string assemblyName, string instrumentationTypeName, string? instrumentationOptionsTypeName)
        {
            AssemblyName = assemblyName;
            InstrumentationTypeName = instrumentationTypeName;
            InstrumentationOptionsTypeName = instrumentationOptionsTypeName;
        }

        private string AssemblyName { get; }

        private string InstrumentationTypeName { get; }

        private string? InstrumentationOptionsTypeName { get; }

        public static void Initialize()
        {
            foreach (var item in Registry)
            {
                Log.Debug("Attempting to load OpenTelemetry instrumentation type {InstrumentationTypeName}.", item.InstrumentationTypeName);

                if (Type.GetType($"{item.InstrumentationTypeName}, {item.AssemblyName}", throwOnError: false) is Type instrumentationType)
                {
                    if (!string.IsNullOrEmpty(item.InstrumentationOptionsTypeName)
                        && Type.GetType($"{item.InstrumentationOptionsTypeName}, {item.AssemblyName}", throwOnError: false) is Type instrumentationOptionsTypeName
                        && instrumentationType.GetConstructor(new[] { instrumentationOptionsTypeName }) is ConstructorInfo typeCtor
                        && instrumentationOptionsTypeName.GetConstructor(Type.EmptyTypes) is ConstructorInfo optionsCtor)
                    {
                        try
                        {
                            Log.Debug("Initializing OpenTelemetry instrumentation type {InstrumentationTypeName} with options {InstrumentationOptionsTypeName}.", item.InstrumentationTypeName, item.InstrumentationOptionsTypeName);
                            var options = optionsCtor.Invoke(null);
                            typeCtor.Invoke([options]);

                            Log.Debug("Successfully initialized OpenTelemetry instrumentation type {InstrumentationTypeName} with options {InstrumentationOptionsTypeName}.", item.InstrumentationTypeName, item.InstrumentationOptionsTypeName);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error initializing OpenTelemetry instrumentation type {InstrumentationTypeName} with options {InstrumentationOptionsTypeName}", item.InstrumentationTypeName, item.InstrumentationOptionsTypeName);
                        }
                    }

                    try
                    {
                        Log.Debug("Initializing OpenTelemetry instrumentation type {InstrumentationTypeName}.", item.InstrumentationTypeName);
                        Activator.CreateInstance(instrumentationType);
                        Log.Debug("Successfully initialized OpenTelemetry instrumentation type {InstrumentationTypeName}.", item.InstrumentationTypeName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error initializing OpenTelemetry instrumentation type {InstrumentationTypeName}", item.InstrumentationTypeName);
                    }
                }

                Log.Debug("Unable to load OpenTelemetry instrumentation type {InstrumentationTypeName}.", item.InstrumentationTypeName);
            }
        }
    }
}
