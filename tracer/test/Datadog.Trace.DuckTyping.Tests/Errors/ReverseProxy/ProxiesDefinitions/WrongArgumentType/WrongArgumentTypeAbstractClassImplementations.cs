// <copyright file="WrongArgumentTypeAbstractClassImplementations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongArgumentType
{
    // These types implicitly implement various classes
    public class WrongArgumentTypeAbstractClassImplementations
    {
        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethod")]
        public class AbstractMethod
        {
            public bool TryGetValue(int value) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParam")]
        internal class AbstractMethodWithOutParam
        {
            public bool TryGetValue(out int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParam")]
        internal class AbstractMethodWithRefParam
        {
            public bool TryGetValue(ref int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethod")]
        internal class AbstractGenericMethod
        {
            public T Echo<T>(int value) => default;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualMethod")]
        internal class AbstractMethodWithVirtualMethod
        {
            public bool TryGetValue(int value) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualMethod")]
        internal class AbstractMethodWithOutParamWithVirtualMethod
        {
            public bool TryGetValue(out int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualMethod")]
        internal class AbstractMethodWithRefParamWithVirtualMethod
        {
            public bool TryGetValue(ref int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualMethod")]
        internal class AbstractGenericMethodWithVirtualMethod
        {
            public T Echo<T>(int value) => default;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithVirtualProperty")]
        internal class AbstractMethodWithVirtualProperty
        {
            public bool TryGetValue(int value) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithOutParamWithVirtualProperty")]
        internal class AbstractMethodWithOutParamWithVirtualProperty
        {
            public bool TryGetValue(out int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractMethodWithRefParamWithVirtualProperty")]
        internal class AbstractMethodWithRefParamWithVirtualProperty
        {
            public bool TryGetValue(ref int value)
            {
                value = 100;
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.AbstractClasses+AbstractGenericMethodWithVirtualProperty")]
        internal class AbstractGenericMethodWithVirtualProperty
        {
            public T Echo<T>(int value) => default;
        }
    }
}
