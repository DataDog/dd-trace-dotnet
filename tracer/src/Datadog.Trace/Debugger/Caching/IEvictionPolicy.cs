// <copyright file="IEvictionPolicy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Caching
{
    internal interface IEvictionPolicy<TKey> : IDisposable
    {
        void Add(TKey key);

        void Remove(TKey key);

        void Access(TKey key);

        TKey Evict();
    }
}
