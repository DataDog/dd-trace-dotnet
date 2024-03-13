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
using Datadog.Trace.Debugger.TimeTravel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AspNetCoreBlockMiddlewareIntegrationEnd.AssemblyName,
        TypeName = AspNetCoreBlockMiddlewareIntegrationEnd.ApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
        MinimumVersion = AspNetCoreBlockMiddlewareIntegrationEnd.Major3,
        MaximumVersion = "8",
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [InstrumentMethod(
        AssemblyName = AspNetCoreBlockMiddlewareIntegrationEnd.AssemblyName,
        TypeName = AspNetCoreBlockMiddlewareIntegrationEnd.InternalApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
        MinimumVersion = AspNetCoreBlockMiddlewareIntegrationEnd.Major2,
        MaximumVersion = AspNetCoreBlockMiddlewareIntegrationEnd.Major2,
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetCoreBlockMiddlewareIntegrationEnd
    {
        private const string Major2 = "2";
        private const string Major3 = "3";
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
            var methodProbes = GetMethodProbeLocations(instance);

            if (methodProbes.Any())
            {
                methodProbes.ForEach(
                    tuple =>
                    {
                        FakeProbeCreator.CreateAndInstallProbe("SpanEntry", tuple.Item2);
                        TimeTravelInitiator.InitiateTimeTravel(tuple.Item2);
                    });

           
                instance.Components.Insert(0, rd => new BlockingMiddleware(rd).Invoke);
                instance.Components.Add(rd => new BlockingMiddleware(rd, endPipeline: true).Invoke);

                return default;
            }

            return default;
        }

        private static List<Tuple<string, MethodInfo>> GetMethodProbeLocations<TTarget>(TTarget instance) where TTarget : IApplicationBuilder
        {
            var service = (IEnumerable<IConfigureOptions<RouteOptions>>)instance.ApplicationServices.GetService(typeof(IEnumerable<IConfigureOptions<RouteOptions>>));

            var methodProbes = new List<Tuple<string, MethodInfo>>();

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

                        methodProbes.Add(Tuple.Create(displayName, method));
                    }
                }
            }

            return methodProbes;
        }

        private static PropertyInfo GetProperty(object obj, string propertyName)
        {
            return obj.GetType().GetProperties().FirstOrDefault(p => p.Name == propertyName);
        }
    }
}
#endif
