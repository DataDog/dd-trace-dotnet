// <copyright file="VirtualClasses.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions
{
    internal class VirtualClasses
    {
        internal class VirtualMethod
        {
            public virtual bool TryGetValue(string value) => true;
        }

        internal class VirtualMethodWithOutParam
        {
            public virtual bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }
        }

        internal class VirtualMethodWithRefParam
        {
            public virtual bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }
        }

        internal class VirtualGenericMethod
        {
            public virtual T Echo<T>(T value) => value;
        }

        internal class VirtualGetOnlyProperty
        {
            public virtual string Value { get; }
        }

        internal class VirtualProperty
        {
            public virtual string Value { get; set; }
        }

        internal class VirtualMethodWithVirtualMethod
        {
            public virtual bool TryGetValue(string value) => true;

            public virtual bool NotOverriden() => true;
        }

        internal class VirtualMethodWithOutParamWithVirtualMethod
        {
            public virtual bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }

            public virtual bool NotOverriden() => true;
        }

        internal class VirtualMethodWithRefParamWithVirtualMethod
        {
            public virtual bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }

            public virtual bool NotOverriden() => true;
        }

        internal class VirtualGenericMethodWithVirtualMethod
        {
            public virtual T Echo<T>(T value) => value;

            public virtual bool NotOverriden() => true;
        }

        internal class VirtualGetOnlyPropertyWithVirtualMethod
        {
            public virtual string Value { get; }

            public virtual bool NotOverriden() => true;
        }

        internal class VirtualPropertyWithVirtualMethod
        {
            public virtual string Value { get; set; }

            public virtual bool NotOverriden() => true;
        }

        internal class VirtualMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual bool TryGetValue(string value) => true;
        }

        internal class VirtualMethodWithOutParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }
        }

        internal class VirtualMethodWithRefParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }
        }

        internal class VirtualGenericMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual T Echo<T>(T value) => value;
        }

        internal class VirtualGetOnlyPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual string Value { get; }
        }

        internal class VirtualPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual string Value { get; set; }
        }
    }
}
