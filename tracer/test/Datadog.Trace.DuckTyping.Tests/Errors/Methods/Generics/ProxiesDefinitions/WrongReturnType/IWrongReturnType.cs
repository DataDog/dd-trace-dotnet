// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IGetDefault
        {
            string GetDefault<T>();
        }

        public interface IWrap
        {
            Tuple<T1, string> Wrap<T1, T2>(T1 a, T2 b);
        }

        public interface IForEachScope
        {
            string ForEachScope<TState2>(Action<object, TState2> callback, TState2 state);
        }

        public interface IGetDefaultInt
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            string GetDefaultInt();
        }

        public interface IGetDefaultString
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            int GetDefaultString();
        }

        public interface IWrapIntString
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            string WrapIntString(int a, string b);
        }
    }
}
