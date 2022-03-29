// <copyright file="WrongMethodNameAbstractClassImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongMethodName
{
    // These types implicitly implement various classes
    public class WrongMethodNameAbstractClassImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethod")]
        public class AbstractMethod
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(string value) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParam")]
        public class AbstractMethodWithOutParam
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(out string value)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParam")]
        public class AbstractMethodWithRefParam
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(ref string value)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethod")]
        public class AbstractGenericMethod
        {
            [DuckReverseMethod]
            public T NotEcho<T>(T value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyProperty")]
        public class AbstractGetOnlyProperty
        {
            [DuckReverseMethod]
            public string NotValue { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractProperty")]
        public class AbstractProperty
        {
            [DuckReverseMethod]
            public string NotValue { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualMethod")]
        public class AbstractMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(string value) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualMethod")]
        public class AbstractMethodWithOutParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(out string value)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualMethod")]
        public class AbstractMethodWithRefParamWithVirtualMethod
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(ref string value)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualMethod")]
        public class AbstractGenericMethodWithVirtualMethod
        {
            [DuckReverseMethod]
            public T NotEcho<T>(T value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyPropertyWithVirtualMethod")]
        public class AbstractGetOnlyPropertyWithVirtualMethod
        {
            [DuckReverseMethod]
            public string NotValue { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractPropertyWithVirtualMethod")]
        public class AbstractPropertyWithVirtualMethod
        {
            [DuckReverseMethod]
            public string NotValue { get; set; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualProperty")]
        public class AbstractMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(string value) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualProperty")]
        public class AbstractMethodWithOutParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(out string value)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualProperty")]
        public class AbstractMethodWithRefParamWithVirtualProperty
        {
            [DuckReverseMethod]
            public bool NotTryGetValue(ref string value)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualProperty")]
        public class AbstractGenericMethodWithVirtualProperty
        {
            [DuckReverseMethod]
            public T NotEcho<T>(T value) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGetOnlyPropertyWithVirtualProperty")]
        public class AbstractGetOnlyPropertyWithVirtualProperty
        {
            [DuckReverseMethod]
            public string NotValue { get; }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractPropertyWithVirtualProperty")]
        public class AbstractPropertyWithVirtualProperty
        {
            [DuckReverseMethod]
            public string NotValue { get; set; }
        }
    }
}
