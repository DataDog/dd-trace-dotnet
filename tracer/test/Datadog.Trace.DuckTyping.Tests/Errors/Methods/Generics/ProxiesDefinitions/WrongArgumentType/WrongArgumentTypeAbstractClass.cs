// <copyright file="WrongArgumentTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongArgumentType
{
    public abstract class WrongArgumentTypeAbstractClass
    {
        public abstract class WrapWithDuckAttributeAbstractClass
        {
            [Duck(ParameterTypeNames = new[] { "T1", "System.String" })]
            public abstract Tuple<T1, T2> Wrap<T1, T2>(T1 a, string b);
        }

        public abstract class WrapAbstractClass
        {
            public abstract Tuple<T1, T2> Wrap<T1, T2>(T1 a, string b);
        }

        public abstract class ForEachScopeAbstractClass
        {
            public abstract void ForEachScope<TState2>(Action<object, string> callback, TState2 state);
        }

        public abstract class WrapIntStringAbstractClass
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public abstract Tuple<int, string> WrapIntString(int a, int b);
        }
    }
}
