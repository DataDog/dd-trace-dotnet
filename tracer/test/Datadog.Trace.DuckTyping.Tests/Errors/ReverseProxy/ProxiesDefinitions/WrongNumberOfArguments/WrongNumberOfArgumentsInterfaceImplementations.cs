// <copyright file="WrongNumberOfArgumentsInterfaceImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongNumberOfArguments
{
    // These types implicitly implement various interfaces
    public class WrongNumberOfArgumentsInterfaceImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.Vendors.Serilog.Core.ILogEventEnricher", "Datadog.Trace")]
        public class DuckChainArgumentsMethod
        {
            // extra argument, not in attribute
            [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.Vendors.Serilog.Events.LogEvent, Datadog.Trace", "Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory, Datadog.Trace" })]
            public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory, string wrong)
            {
            }
        }

        [ReverseTypeToTest("Datadog.Trace.Vendors.Serilog.Core.ILogEventEnricher", "Datadog.Trace")]
        public class DuckChainArgumentsMethod2
        {
            // extra argument, in attribute too
            [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.Vendors.Serilog.Events.LogEvent, Datadog.Trace", "Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory, Datadog.Trace", "System.String" })]
            public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory, string wrong)
            {
            }
        }

        [ReverseTypeToTest("Datadog.Trace.Vendors.Serilog.Core.ILogEventPropertyFactory", "Datadog.Trace")]
        public class DuckChainReturnMethod
        {
            [DuckReverseMethod]
            public ILogEventProperty CreateProperty(string name, object value, string wrong, bool destructureObjects = false)
            {
                return null;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethod")]
        public class Method
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong)
            {
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithOutParam")]
        public class MethodWithOutParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = string.Empty;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithRefParam")]
        public class MethodWithRefParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = string.Empty;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGenericMethod")]
        public class GenericMethod
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }
    }
}
