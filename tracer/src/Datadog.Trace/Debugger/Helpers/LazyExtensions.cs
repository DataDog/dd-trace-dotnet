// <copyright file="LazyExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class LazyExtensions
    {
        public static Lazy<TConcrete> Cast<TInterface, TConcrete>(this Lazy<TInterface> lazy)
            where TConcrete : class, TInterface
        {
            return new Lazy<TConcrete>(() => (TConcrete)lazy.Value);
        }
    }
}
