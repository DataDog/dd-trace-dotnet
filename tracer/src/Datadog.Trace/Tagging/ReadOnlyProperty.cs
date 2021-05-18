// <copyright file="ReadOnlyProperty.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tagging
{
    internal class ReadOnlyProperty<TTags, TResult> : Property<TTags, TResult>
    {
        public ReadOnlyProperty(string key, Func<TTags, TResult> getter)
            : base(key, getter, (_, _) => { })
        {
        }

        public override bool IsReadOnly => true;
    }
}
