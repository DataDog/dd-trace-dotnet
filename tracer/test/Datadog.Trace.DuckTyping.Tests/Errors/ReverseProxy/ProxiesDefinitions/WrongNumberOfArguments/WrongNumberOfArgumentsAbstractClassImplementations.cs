﻿// <copyright file="WrongNumberOfArgumentsAbstractClassImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongNumberOfArguments
{
    // These types implicitly implement various classes
    public class WrongNumberOfArgumentsAbstractClassImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethod")]
        public class AbstractMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParam")]
        internal class AbstractMethodWithOutParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParam")]
        internal class AbstractMethodWithRefParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethod")]
        internal class AbstractGenericMethod
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualMethod")]
        internal class AbstractMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualMethod")]
        internal class AbstractMethodWithOutParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualMethod")]
        internal class AbstractMethodWithRefParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualMethod")]
        internal class AbstractGenericMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualProperty")]
        internal class AbstractMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualProperty")]
        internal class AbstractMethodWithOutParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualProperty")]
        internal class AbstractMethodWithRefParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualProperty")]
        internal class AbstractGenericMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }
    }
}
