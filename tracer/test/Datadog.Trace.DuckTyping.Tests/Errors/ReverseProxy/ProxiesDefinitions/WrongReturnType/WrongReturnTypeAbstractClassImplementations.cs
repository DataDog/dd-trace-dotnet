// <copyright file="WrongReturnTypeAbstractClassImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongReturnType
{
    // These types implicitly implement various classes
    public class WrongReturnTypeAbstractClassImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethod")]
        public class AbstractMethod
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParam")]
        internal class AbstractMethodWithOutParam
        {
            [DuckReverseMethod]
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParam")]
        internal class AbstractMethodWithRefParam
        {
            [DuckReverseMethod]
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return string.Empty;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethod")]
        internal class AbstractGenericMethod
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyProperty")]
        internal class AbstractGetOnlyProperty
        {
            [DuckReverseMethod]
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractProperty")]
        internal class AbstractProperty
        {
            [DuckReverseMethod]
            public int Value { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualMethod")]
        internal class AbstractMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualMethod")]
        internal class AbstractMethodWithOutParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualMethod")]
        internal class AbstractMethodWithRefParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualMethod")]
        internal class AbstractGenericMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyPropertyWithVirtualMethod")]
        internal class AbstractGetOnlyPropertyWithVirtualMethod
        {
            [DuckReverseMethod]
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractPropertyWithVirtualMethod")]
        internal class AbstractPropertyWithVirtualMethod
        {
            [DuckReverseMethod]
            public int Value { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualProperty")]
        internal class AbstractMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualProperty")]
        internal class AbstractMethodWithOutParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualProperty")]
        internal class AbstractMethodWithRefParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualProperty")]
        internal class AbstractGenericMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyPropertyWithVirtualProperty")]
        internal class AbstractGetOnlyPropertyWithVirtualProperty
        {
            [DuckReverseMethod]
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractPropertyWithVirtualProperty")]
        internal class AbstractPropertyWithVirtualProperty
        {
            [DuckReverseMethod]
            public int Value { get; set; }
        }
    }
}
