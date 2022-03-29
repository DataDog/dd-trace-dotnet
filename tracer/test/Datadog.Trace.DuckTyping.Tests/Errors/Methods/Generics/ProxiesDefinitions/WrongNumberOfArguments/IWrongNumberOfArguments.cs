// <copyright file="IWrongNumberOfArguments.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongNumberOfArguments
{
    public interface IWrongNumberOfArguments
    {
        public interface IGetDefault
        {
            T GetDefault<T>(int wrong);
        }

        public interface IWrapWithDuckAttribute
        {
            [Duck(ParameterTypeNames = new[] { "T1", "T2", "System.Int32" })]
            Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b, int wrong);
        }

        public interface IWrap
        {
            Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b, int wrong);
        }

        public interface IForEachScope
        {
            void ForEachScope<TState2>(Action<object, TState2> callback, TState2 state, int wrong);
        }

        public interface IGetDefaultInt
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            int GetDefaultInt(int wrong);
        }

        public interface IGetDefaultString
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            string GetDefaultString(int wrong);
        }

        public interface IWrapIntString
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            Tuple<int, string> WrapIntString(int a, string b, int wrong);
        }
    }
}
