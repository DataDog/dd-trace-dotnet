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
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParam")]
        public class VirtualMethodWithOutParam
        {
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParam")]
        public class VirtualMethodWithRefParam
        {
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethod")]
        public class VirtualGenericMethod
        {
            public T Echo<T>(T value, int wrong) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithVirtualMethod")]
        public class VirtualMethodWithVirtualMethod
        {
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParamWithVirtualMethod")]
        public class VirtualMethodWithOutParamWithVirtualMethod
        {
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParamWithVirtualMethod")]
        public class VirtualMethodWithRefParamWithVirtualMethod
        {
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethodWithVirtualMethod")]
        public class VirtualGenericMethodWithVirtualMethod
        {
            public T Echo<T>(T value, int wrong) => value;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithVirtualProperty")]
        public class VirtualMethodWithVirtualProperty
        {
            public bool TryGetValue(string value, int wrong) => true;
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithOutParamWithVirtualProperty")]
        public class VirtualMethodWithOutParamWithVirtualProperty
        {
            public bool TryGetValue(out string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualMethodWithRefParamWithVirtualProperty")]
        public class VirtualMethodWithRefParamWithVirtualProperty
        {
            public bool TryGetValue(ref string value, int wrong)
            {
                value = "woop";
                return true;
            }
        }

        [ReverseTypeToTest("Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions.VirtualClasses+VirtualGenericMethodWithVirtualProperty")]
        public class VirtualGenericMethodWithVirtualProperty
        {
            public T Echo<T>(T value, int wrong) => value;
        }
    }
}
