// <copyright file="WrongNumberOfArgumentsVirtualClassImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongNumberOfArguments
{
    public class WrongNumberOfArgumentsVirtualClassImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethod")]
        public class VirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParam")]
        public class VirtualMethodWithOutParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParam")]
        public class VirtualMethodWithRefParam
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethod")]
        public class VirtualGenericMethod
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithVirtualMethod")]
        public class VirtualMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParamWithVirtualMethod")]
        public class VirtualMethodWithOutParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParamWithVirtualMethod")]
        public class VirtualMethodWithRefParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethodWithVirtualMethod")]
        public class VirtualGenericMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithVirtualProperty")]
        public class VirtualMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParamWithVirtualProperty")]
        public class VirtualMethodWithOutParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParamWithVirtualProperty")]
        public class VirtualMethodWithRefParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethodWithVirtualProperty")]
        public class VirtualGenericMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public T Echo<T>(T value, int wrong) => value;
        }
    }
}
