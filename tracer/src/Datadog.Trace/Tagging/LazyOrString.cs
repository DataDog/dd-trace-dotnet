// <copyright file="LazyOrString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tagging
{
    internal class LazyOrString
    {
        private readonly Lazy<string> _lazyValue;
        private readonly string _value;

        internal LazyOrString(string value)
        {
            _value = value;
        }

        internal LazyOrString(Func<string> valueFactory)
        {
            _lazyValue = new(valueFactory);
        }

        private LazyOrString(Lazy<string> value)
        {
            _lazyValue = value;
        }

        public static implicit operator LazyOrString(Lazy<string> value) => new(value);

        public static implicit operator LazyOrString(string value) => new(value);

        public override string ToString() => _value ?? _lazyValue.Value;
    }
}
