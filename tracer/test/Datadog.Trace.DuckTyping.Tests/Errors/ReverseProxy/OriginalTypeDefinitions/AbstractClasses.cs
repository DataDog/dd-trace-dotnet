// <copyright file="AbstractClasses.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions
{
    public abstract class AbstractClasses
    {
        public abstract class AbstractMethod
        {
            public abstract bool TryGetValue(string value);
        }

        public abstract class AbstractMethodWithOutParam
        {
            public abstract bool TryGetValue(out string value);
        }

        public abstract class AbstractMethodWithRefParam
        {
            public abstract bool TryGetValue(ref string value);
        }

        public abstract class AbstractGenericMethod
        {
            public abstract T Echo<T>(T value);
        }

        public abstract class AbstractGetOnlyProperty
        {
            public abstract string Value { get; }
        }

        public abstract class AbstractProperty
        {
            public abstract string Value { get; set; }
        }

        public abstract class AbstractMethodWithVirtualMethod
        {
            public abstract bool TryGetValue(string value);

            public virtual bool NotOverriden() => true;
        }

        public abstract class AbstractMethodWithOutParamWithVirtualMethod
        {
            public abstract bool TryGetValue(out string value);

            public virtual bool NotOverriden() => true;
        }

        public abstract class AbstractMethodWithRefParamWithVirtualMethod
        {
            public abstract bool TryGetValue(ref string value);

            public virtual bool NotOverriden() => true;
        }

        public abstract class AbstractGenericMethodWithVirtualMethod
        {
            public abstract T Echo<T>(T value);

            public virtual bool NotOverriden() => true;
        }

        public abstract class AbstractGetOnlyPropertyWithVirtualMethod
        {
            public abstract string Value { get; }

            public virtual bool NotOverriden() => true;
        }

        public abstract class AbstractPropertyWithVirtualMethod
        {
            public abstract string Value { get; set; }

            public virtual bool NotOverriden() => true;
        }

        public abstract class AbstractMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract bool TryGetValue(string value);
        }

        public abstract class AbstractMethodWithOutParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract bool TryGetValue(out string value);
        }

        public abstract class AbstractMethodWithRefParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract bool TryGetValue(ref string value);
        }

        public abstract class AbstractGenericMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract T Echo<T>(T value);
        }

        public abstract class AbstractGetOnlyPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract string Value { get; }
        }

        public abstract class AbstractPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract string Value { get; set; }
        }
    }
}
