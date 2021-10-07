// <copyright file="IWrongArgumentType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongArgumentType
{
    public interface IWrongArgumentType
    {
        public interface IWrap
        {
            Tuple<T1, T2> Wrap<T1, T2>(T1 a, string b);
        }

        public interface IForEachScope
        {
            void ForEachScope<TState2>(Action<object, string> callback, TState2 state);
        }

        public interface IWrapIntString
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            Tuple<int, string> WrapIntString(int a, int b);
        }
    }
}
