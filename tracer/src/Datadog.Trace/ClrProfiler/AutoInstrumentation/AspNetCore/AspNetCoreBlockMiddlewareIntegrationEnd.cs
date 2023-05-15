// <copyright file="AspNetCoreBlockMiddlewareIntegrationEnd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.DiagnosticListeners;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = ApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
        MinimumVersion = Major3,
        MaximumVersion = Major7,
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = InternalApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
        MinimumVersion = Major2,
        MaximumVersion = Major2,
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetCoreBlockMiddlewareIntegrationEnd
    {
        private const string Major2 = "2";
        private const string Major3 = "3";
        private const string Major7 = "7";
        private const string AssemblyName = "Microsoft.AspNetCore.Http";

        private const string ApplicationBuilder = "Microsoft.AspNetCore.Builder.ApplicationBuilder";
        private const string InternalApplicationBuilder = "Microsoft.AspNetCore.Builder.Internal.ApplicationBuilder";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IApplicationBuilder
        {
            var service = (IEnumerable<IConfigureOptions<RouteOptions>>)instance.ApplicationServices.GetService(typeof(IEnumerable<IConfigureOptions<RouteOptions>>));

            var methodProbes = new List<Tuple<string, NativeMethodProbeDefinition>>();

            foreach (IConfigureOptions<RouteOptions> configureOptions in service)
            {
                var field = configureOptions.GetType().GetField("_dataSources", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    continue;
                }

                var endpointDataSources = field.GetValue(configureOptions);

                if (endpointDataSources == null)
                {
                    continue;
                }

                Type enumerableType = endpointDataSources.GetType();
                MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
                var endpointDataSourcesEnumerator = (IEnumerator)getEnumeratorMethod.Invoke(endpointDataSources, null);
                while (endpointDataSourcesEnumerator.MoveNext())
                {
                    var endpointDataSource = endpointDataSourcesEnumerator.Current;
                    var endpoints = GetProperty(endpointDataSource, "Endpoints").GetValue(endpointDataSource);

                    if (endpoints == null)
                    {
                        continue;
                    }

                    enumerableType = endpoints.GetType();
                    getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
                    var endpointsEnumerator = (IEnumerator)getEnumeratorMethod.Invoke(endpoints, null);
                    while (endpointsEnumerator.MoveNext())
                    {
                        var endpoint = endpointsEnumerator.Current;
                        var metadata = GetProperty(endpoint, "Metadata").GetValue(endpoint);
                        Type controllerActionDescriptorType = Type.GetType("Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor, Microsoft.AspNetCore.Mvc.Core");

                        if (controllerActionDescriptorType == null)
                        {
                            continue;
                        }

                        var getMetadata = metadata.GetType().GetMethod("GetMetadata").MakeGenericMethod(controllerActionDescriptorType);

                        if (getMetadata == null)
                        {
                            continue;
                        }

                        var controllerActionDescriptor = getMetadata.Invoke(metadata, null);
                        if (controllerActionDescriptor == null)
                        {
                            continue;
                        }

                        MethodInfo method = (MethodInfo)GetProperty(controllerActionDescriptor, "MethodInfo").GetValue(controllerActionDescriptor);

                        string displayName = (string)GetProperty(controllerActionDescriptor, "DisplayName").GetValue(controllerActionDescriptor);

                        if (method == null || string.IsNullOrEmpty(displayName))
                        {
                            continue;
                        }

                        methodProbes.Add(Tuple.Create(displayName, new NativeMethodProbeDefinition($"SpanOrigin_EntrySpan_{method.DeclaringType.FullName}_{method.Name}", method.DeclaringType.FullName, method.Name, targetParameterTypesFullName: null)));
                    }
                }
            }

            if (methodProbes.Any())
            {
                // Add all ProbeProcessor(s)
                methodProbes.ForEach(
                    tuple =>
                    {
                        var displayName = tuple.Item1;
                        var method = tuple.Item2;

                        var templateStr = $"Entry Span : {displayName}";
                        var template = templateStr + "{1}";
                        var json = @"{
    ""Ignore"": ""1""
}";
                        var segments = new SnapshotSegment[] { new(null, null, templateStr), new("1", json, null) };

                        var methodProbeDef = new LogProbe
                        {
                            CaptureSnapshot = true,
                            Id = method.ProbeId,
                            Where = new Where
                            {
                                MethodName = method.TargetMethod,
                                TypeName = method.TargetType
                            },
                            EvaluateAt = EvaluateAt.Entry,
                            Template = template,
                            Segments = segments,
                            Sampling = new Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 1000000 }
                        };
                        ProbeExpressionsProcessor.Instance.AddProbeProcessor(methodProbeDef);
                    });

                // Install probes
                DebuggerNativeMethods.InstrumentProbes(
                    methodProbes.Select(t => t.Item2).ToArray(),
                    Array.Empty<NativeLineProbeDefinition>(),
                    Array.Empty<NativeSpanProbeDefinition>(),
                    Array.Empty<NativeRemoveProbeRequest>());
            }

            instance.Components.Insert(0, rd => new BlockingMiddleware(rd).Invoke);
            instance.Components.Add(rd => new BlockingMiddleware(rd, endPipeline: true).Invoke);

            return default;
        }

        private static PropertyInfo GetProperty(object obj, string propertyName)
        {
            return obj.GetType().GetProperties().FirstOrDefault(p => p.Name == propertyName);
        }
    }
}
#endif
