// <copyright file="IProperty.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tagging
{
    internal interface IProperty<TResult>
    {
        bool IsReadOnly { get; }

        string Key { get; }

        Func<ITags, TResult> Getter { get; }

        Action<ITags, TResult> Setter { get; }
    }
}
