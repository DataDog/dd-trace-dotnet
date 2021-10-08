// <copyright file="AbstractClasses.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions
{
    internal abstract class AbstractClasses
    {
        internal abstract class AbstractMethod
        {
            public abstract bool TryGetValue(string value);
        }

        internal abstract class AbstractMethodWithOutParam
        {
            public abstract bool TryGetValue(out string value);
        }

        internal abstract class AbstractMethodWithRefParam
        {
            public abstract bool TryGetValue(ref string value);
        }

        internal abstract class AbstractGenericMethod
        {
            public abstract T Echo<T>(T value);
        }

        internal abstract class AbstractGetOnlyProperty
        {
            public abstract string Value { get; }
        }

        internal abstract class AbstractProperty
        {
            public abstract string Value { get; set; }
        }

        internal abstract class AbstractMethodWithVirtualMethod
        {
            public abstract bool TryGetValue(string value);

            public virtual bool NotOverriden() => true;
        }

        internal abstract class AbstractMethodWithOutParamWithVirtualMethod
        {
            public abstract bool TryGetValue(out string value);

            public virtual bool NotOverriden() => true;
        }

        internal abstract class AbstractMethodWithRefParamWithVirtualMethod
        {
            public abstract bool TryGetValue(ref string value);

            public virtual bool NotOverriden() => true;
        }

        internal abstract class AbstractGenericMethodWithVirtualMethod
        {
            public abstract T Echo<T>(T value);

            public virtual bool NotOverriden() => true;
        }

        internal abstract class AbstractGetOnlyPropertyWithVirtualMethod
        {
            public abstract string Value { get; }

            public virtual bool NotOverriden() => true;
        }

        internal abstract class AbstractPropertyWithVirtualMethod
        {
            public abstract string Value { get; set; }

            public virtual bool NotOverriden() => true;
        }

        internal abstract class AbstractMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract bool TryGetValue(string value);
        }

        internal abstract class AbstractMethodWithOutParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract bool TryGetValue(out string value);
        }

        internal abstract class AbstractMethodWithRefParamWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract bool TryGetValue(ref string value);
        }

        internal abstract class AbstractGenericMethodWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract T Echo<T>(T value);
        }

        internal abstract class AbstractGetOnlyPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract string Value { get; }
        }

        internal abstract class AbstractPropertyWithVirtualProperty
        {
            public virtual bool NotOverriden { get; set; }

            public abstract string Value { get; set; }
        }
    }
}
