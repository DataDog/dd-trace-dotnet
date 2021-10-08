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
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParam")]
        internal class AbstractMethodWithOutParam
        {
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParam")]
        internal class AbstractMethodWithRefParam
        {
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return string.Empty;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethod")]
        internal class AbstractGenericMethod
        {
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyProperty")]
        internal class AbstractGetOnlyProperty
        {
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractProperty")]
        internal class AbstractProperty
        {
            public int Value { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualMethod")]
        internal class AbstractMethodWithVirtualMethod
        {
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualMethod")]
        internal class AbstractMethodWithOutParamWithVirtualMethod
        {
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualMethod")]
        internal class AbstractMethodWithRefParamWithVirtualMethod
        {
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualMethod")]
        internal class AbstractGenericMethodWithVirtualMethod
        {
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyPropertyWithVirtualMethod")]
        internal class AbstractGetOnlyPropertyWithVirtualMethod
        {
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractPropertyWithVirtualMethod")]
        internal class AbstractPropertyWithVirtualMethod
        {
            public int Value { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualProperty")]
        internal class AbstractMethodWithVirtualProperty
        {
            public string TryGetValue(string value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualProperty")]
        internal class AbstractMethodWithOutParamWithVirtualProperty
        {
            public void TryGetValue(out string value)
            {
                value = "woop";
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualProperty")]
        internal class AbstractMethodWithRefParamWithVirtualProperty
        {
            public string TryGetValue(ref string value)
            {
                value = "woop";
                return value;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualProperty")]
        internal class AbstractGenericMethodWithVirtualProperty
        {
            public int Echo<T>(T value) => 100;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyPropertyWithVirtualProperty")]
        internal class AbstractGetOnlyPropertyWithVirtualProperty
        {
            public int Value { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractPropertyWithVirtualProperty")]
        internal class AbstractPropertyWithVirtualProperty
        {
            public int Value { get; set; }
        }
    }
}
