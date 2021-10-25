﻿// <copyright file="WrongArgumentTypeInterfaceImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongArgumentType
{
    // These types implicitly implement various interfaces
    public class WrongArgumentTypeInterfaceImplementations
    {
        [ReverseTypeToTest("Serilog.Core.ILogEventEnricher", "Serilog")]
        public class DuckChainArgumentsMethod
        {
            // reversed order of arguments
            [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.Vendors.Serilog.Events.ILogEventPropertyFactory, Datadog.Trace", "Datadog.Trace.Vendors.Serilog.Core.LogEvent, Datadog.Trace" })]
            public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
            }
        }

        [ReverseTypeToTest("Serilog.Core.ILogEventPropertyFactory", "Serilog")]
        public class DuckChainReturnMethod
        {
            // Wrong first argument
            [DuckReverseMethod]
            public ILogEventProperty CreateProperty(int name, object value, bool destructureObjects = false)
            {
                return null;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethod")]
        public class Method
        {
            [DuckReverseMethod]
            public bool TryGetValue(int value)
            {
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithOutParam")]
        public class MethodWithOutParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(out int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IMethodWithRefParam")]
        public class MethodWithRefParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.IInterfaces+IGenericMethod")]
        public class GenericMethod
        {
            [DuckReverseMethod]
            public T Echo<T>(int value) => default;
        }
    }
}
