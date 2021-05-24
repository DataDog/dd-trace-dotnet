// <copyright file="Property.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tagging
{
    internal class Property<TTags, TResult> : IProperty<TResult>
    {
        public Property(string key, Func<TTags, TResult> getter, Action<TTags, TResult> setter)
        {
            Key = key;
            Getter = tags => getter((TTags)tags);
            Setter = (tags, value) => setter((TTags)tags, value);
        }

        public virtual bool IsReadOnly => false;

        public string Key { get; }

        public Func<ITags, TResult> Getter { get; }

        public Action<ITags, TResult> Setter { get; }
    }
}
