// <copyright file="WrongMethodNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongMethodName
{
    public abstract class WrongMethodNameAbstractClass
    {
        public abstract class GetDefaultAbstractClass
        {
            public abstract T NotGetDefault<T>();
        }

        public abstract class WrapWithDuckAttributeAbstractClass
        {
            [Duck(ParameterTypeNames = new[] { "T1", "T2" })]
            public abstract Tuple<T1, T2> NotWrap<T1, T2>(T1 a, T2 b);
        }

        public abstract class WrapAbstractClass
        {
            public abstract Tuple<T1, T2> NotWrap<T1, T2>(T1 a, T2 b);
        }

        public abstract class ForEachScopeAbstractClass
        {
            public abstract void NotForEachScope<TState2>(Action<object, TState2> callback, TState2 state);
        }

        public abstract class GetDefaultIntAbstractClass
        {
            [Duck(Name = "NotGetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            public abstract int GetDefaultInt();
        }

        public abstract class GetDefaultStringAbstractClass
        {
            [Duck(Name = "NotGetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            public abstract string GetDefaultString();
        }

        public abstract class WrapIntStringAbstractClass
        {
            [Duck(Name = "NotWrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public abstract Tuple<int, string> WrapIntString(int a, string b);
        }
    }
}
