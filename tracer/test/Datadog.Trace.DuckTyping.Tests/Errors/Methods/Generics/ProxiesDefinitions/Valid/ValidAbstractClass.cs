// <copyright file="ValidAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.Valid
{
    public abstract class ValidAbstractClass
    {
        public abstract class GetDefaultAbstractClass
        {
            public abstract T GetDefault<T>();
        }

        public abstract class WrapAbstractClass
        {
            public abstract Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b);
        }

        public abstract class ForEachScopeAbstractClass
        {
            public abstract void ForEachScope<TState2>(Action<object, TState2> callback, TState2 state);
        }

        public abstract class GetDefaultIntAbstractClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            public abstract int GetDefaultInt();
        }

        public abstract class GetDefaultStringAbstractClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            public abstract string GetDefaultString();
        }

        public abstract class WrapIntStringAbstractClass
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public abstract Tuple<int, string> WrapIntString(int a, string b);
        }
    }
}
