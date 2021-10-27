// <copyright file="IWrongMethodName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongMethodName
{
    public interface IWrongMethodName
    {
        public interface IGetDefault
        {
            T NotGetDefault<T>();
        }

        public interface IWrapWithDuckAttribute
        {
            [Duck(ParameterTypeNames = new[] { "T1", "T2" })]
            Tuple<T1, T2> NotWrap<T1, T2>(T1 a, T2 b);
        }

        public interface IWrap
        {
            Tuple<T1, T2> NotWrap<T1, T2>(T1 a, T2 b);
        }

        public interface IForEachScope
        {
            void NotForEachScope<TState2>(Action<object, TState2> callback, TState2 state);
        }

        public interface IGetDefaultInt
        {
            [Duck(Name = "NotGetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            int GetDefaultInt();
        }

        public interface IGetDefaultString
        {
            [Duck(Name = "NotGetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            string GetDefaultString();
        }

        public interface IWrapIntString
        {
            [Duck(Name = "NotWrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            Tuple<int, string> WrapIntString(int a, string b);
        }
    }
}
