// <copyright file="IDefaultGenericMethodDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public interface IDefaultGenericMethodDuckType
    {
        T GetDefault<T>();

        Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b);

        void ForEachScope<TState2>(Action<object, TState2> callback, TState2 state);
    }
}
