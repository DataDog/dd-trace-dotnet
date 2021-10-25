// <copyright file="WrongReturnTypeInterfaceImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongReturnType
{
    // These types implicitly implement various interfaces
    public class WrongReturnTypeInterfaceImplementations
    {
        [ReverseTypeToTest("Serilog.Core.ILogEventEnricher", "Serilog")]
        public class DuckChainArgumentsMethod
        {
            [DuckReverseMethod(ParameterTypeNames = new[] { "Serilog.Events.LogEvent, Serilog", "Serilog.Core.ILogEventPropertyFactory, Serilog" })]
            public bool Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory) => true;
        }

        [ReverseTypeToTest("Serilog.Core.ILogEventPropertyFactory", "Serilog")]
        public class DuckChainReturnMethod
        {
            [DuckReverseMethod]
            public bool CreateProperty(string name, object value, bool destructureObjects = false)
            {
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethod")]
        public class Method
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithOutParam")]
        public class MethodWithOutParam
        {
            [DuckReverseMethod]
            public void TryGetValue(out string value)
            {
                value = string.Empty;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithRefParam")]
        public class MethodWithRefParam
        {
            [DuckReverseMethod]
            public string TryGetValue(ref string value)
            {
                value = string.Empty;
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGenericMethod")]
        public class GenericMethod
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGetOnlyProperty")]
        public class GetOnlyProperty
        {
            [DuckReverseMethod]
            public int Value
            {
                get => 100;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IProperty")]
        public class Property
        {
            [DuckReverseMethod]
            public int Value { get; set; } = 100;
        }
    }
}
