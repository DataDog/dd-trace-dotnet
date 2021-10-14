// <copyright file="WrongReturnTypeVirtualClassImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClassImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethod")]
        public class VirtualMethod
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParam")]
        public class VirtualMethodWithOutParam
        {
            [DuckReverseMethod]
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParam")]
        public class VirtualMethodWithRefParam
        {
            [DuckReverseMethod]
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethod")]
        public class VirtualGenericMethod
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGetOnlyProperty")]
        public class VirtualGetOnlyProperty
        {
            [DuckReverseMethod]
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualProperty")]
        public class VirtualProperty
        {
            [DuckReverseMethod]
            public int Value { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithVirtualMethod")]
        public class VirtualMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParamWithVirtualMethod")]
        public class VirtualMethodWithOutParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public string TryGetValue(out string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParamWithVirtualMethod")]
        public class VirtualMethodWithRefParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public void TryGetValue(ref string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethodWithVirtualMethod")]
        public class VirtualGenericMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGetOnlyPropertyWithVirtualMethod")]
        public class VirtualGetOnlyPropertyWithVirtualMethod
        {
            [DuckReverseMethod]
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualPropertyWithVirtualMethod")]
        public class VirtualPropertyWithVirtualMethod
        {
            [DuckReverseMethod]
            public int Value { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithVirtualProperty")]
        public class VirtualMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParamWithVirtualProperty")]
        public class VirtualMethodWithOutParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParamWithVirtualProperty")]
        public class VirtualMethodWithRefParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethodWithVirtualProperty")]
        public class VirtualGenericMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGetOnlyPropertyWithVirtualProperty")]
        public class VirtualGetOnlyPropertyWithVirtualProperty
        {
            [DuckReverseMethod]
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualPropertyWithVirtualProperty")]
        public class VirtualPropertyWithVirtualProperty
        {
            [DuckReverseMethod]
            public int Value { get; set; }
        }
    }
}
