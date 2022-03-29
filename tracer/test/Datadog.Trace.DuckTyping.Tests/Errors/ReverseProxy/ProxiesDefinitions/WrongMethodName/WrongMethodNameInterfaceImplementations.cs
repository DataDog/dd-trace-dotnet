// <copyright file="WrongMethodNameInterfaceImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongMethodName
{
    // These types implicitly implement various interfaces
    public class WrongMethodNameInterfaceImplementations
    {
        [ReverseTypeToTest("Serilog.Core.ILogEventEnricher", "Serilog")]
        public class DuckChainArgumentsMethod
        {
            [DuckReverseMethod(ParameterTypeNames = new[] { "Serilog.Events.LogEvent, Serilog", "Serilog.Core.ILogEventPropertyFactory, Serilog" })]
            public void NotEnrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
            }
        }

        [ReverseTypeToTest("Serilog.Core.ILogEventPropertyFactory", "Serilog")]
        public class DuckChainReturnMethod
        {
            [DuckReverseMethod]
            public ILogEventProperty NotCreateProperty(string name, object value, bool destructureObjects = false)
            {
                return null;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethod")]
        public class Method
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(string value)
            {
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithOutParam")]
        public class MethodWithOutParam
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithRefParam")]
        public class MethodWithRefParam
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGenericMethod")]
        public class GenericMethod
        {
            [DuckReverseMethod]
            public T NotEcho<T>(T value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGetOnlyProperty")]
        public class GetOnlyProperty
        {
            [DuckReverseMethod]
            public string NotValue
            {
                get => string.Empty;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IProperty")]
        public class Property
        {
            [DuckReverseMethod]
            public string NotValue { get; set; } = string.Empty;
        }
    }
}
