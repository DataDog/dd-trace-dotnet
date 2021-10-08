// <copyright file="ValidInterfaceImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.Valid
{
    // These types implicitly implement various interfaces
    public class ValidInterfaceImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.Vendors.Serilog.Core.ILogEventEnricher", "Datadog.Trace")]
        public class DuckChainArgumentsMethod
        {
            [DuckReverseMethod("Datadog.Trace.Vendors.Serilog.Events.LogEvent", "Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory")]
            public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
            }
        }

        [ReverseTypeToTest("Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory", "Datadog.Trace")]
        public class DuckChainReturnMethod
        {
            [DuckReverseMethod("Datadog.Trace.Vendors.Serilog.Events.LogEvent", "Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory")]
            public ILogEventProperty CreateProperty(string name, object value, bool destructureObjects = false)
            {
                return null;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethod")]
        public class Method
        {
            public bool TryGetValue(string value)
            {
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithOutParam")]
        public class MethodWithOutParam
        {
            public bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithRefParam")]
        public class MethodWithRefParam
        {
            public bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGenericMethod")]
        public class GenericMethod
        {
            public T Echo<T>(T value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGetOnlyProperty")]
        public class GetOnlyProperty
        {
            public string Value
            {
                get => string.Empty;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IProperty")]
        public class Property
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}
