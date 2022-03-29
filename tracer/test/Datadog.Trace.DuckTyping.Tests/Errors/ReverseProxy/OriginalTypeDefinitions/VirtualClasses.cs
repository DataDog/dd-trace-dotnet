// <copyright file="VirtualClasses.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions
{
    public class VirtualClasses
    {
        public class VirtualMethod
        {
            public virtual bool TryGetValue(string value) => true;
        }

        public class VirtualMethodWithOutParam
        {
            public virtual bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }
        }

        public class VirtualMethodWithRefParam
        {
            public virtual bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }
        }

        public class VirtualGenericMethod
        {
            public virtual T Echo<T>(T value) => value;
        }

        public class VirtualGetOnlyProperty
        {
            public virtual string Value { get; }
        }

        public class VirtualProperty
        {
            public virtual string Value { get; set; }
        }

        public class VirtualMethodWithVirtualMethod
        {
            public virtual bool TryGetValue(string value) => true;

            public virtual bool NotOverriden() => true;
        }

        public class VirtualMethodWithOutParamWithVirtualMethod
        {
            public virtual bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }

            public virtual bool NotOverriden() => true;
        }

        public class VirtualMethodWithRefParamWithVirtualMethod
        {
            public virtual bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }

            public virtual bool NotOverriden() => true;
        }

        public class VirtualGenericMethodWithVirtualMethod
        {
            public virtual T Echo<T>(T value) => value;

            public virtual bool NotOverriden() => true;
        }

        public class VirtualGetOnlyPropertyWithVirtualMethod
        {
            public virtual string Value { get; }

            public virtual bool NotOverriden() => true;
        }

        public class VirtualPropertyWithVirtualMethod
        {
            public virtual string Value { get; set; }

            public virtual bool NotOverriden() => true;
        }

        public class VirtualMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual bool TryGetValue(string value) => true;
        }

        public class VirtualMethodWithOutParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual bool TryGetValue(out string value)
            {
                value = string.Empty;
                return true;
            }
        }

        public class VirtualMethodWithRefParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual bool TryGetValue(ref string value)
            {
                value = string.Empty;
                return true;
            }
        }

        public class VirtualGenericMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual T Echo<T>(T value) => value;
        }

        public class VirtualGetOnlyPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual string Value { get; }
        }

        public class VirtualPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public virtual string Value { get; set; }
        }
    }
}
