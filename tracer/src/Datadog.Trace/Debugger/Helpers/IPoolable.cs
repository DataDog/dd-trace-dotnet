// <copyright file="IPoolable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Helpers
{
    internal interface IPoolable<TSetParameters>
    {
        void Set(TSetParameters parameters);

        void Reset();
    }
}
