// <copyright file="WrongNumberOfArgumentsAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongNumberOfArguments
{
    public abstract class WrongNumberOfArgumentsAbstractClass
    {
        public abstract class GetDefaultAbstractClass
        {
            public abstract T GetDefault<T>(int wrong);
        }

        public abstract class WrapWithDuckAttributeAbstractClass
        {
            [Duck(ParameterTypeNames = new[] { "T1", "T2", "System.Int32" })]
            public abstract Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b, int wrong);
        }

        public abstract class WrapAbstractClass
        {
            public abstract Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b, int wrong);
        }

        public abstract class ForEachScopeAbstractClass
        {
            public abstract void ForEachScope<TState2>(Action<object, TState2> callback, TState2 state, int wrong);
        }

        public abstract class GetDefaultIntAbstractClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            public abstract int GetDefaultInt(int wrong);
        }

        public abstract class GetDefaultStringAbstractClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            public abstract string GetDefaultString(int wrong);
        }

        public abstract class WrapIntStringAbstractClass
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public abstract Tuple<int, string> WrapIntString(int a, string b, int wrong);
        }
    }
}
