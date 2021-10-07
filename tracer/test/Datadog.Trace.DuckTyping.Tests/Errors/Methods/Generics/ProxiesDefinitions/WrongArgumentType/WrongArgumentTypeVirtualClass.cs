// <copyright file="WrongArgumentTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongArgumentType
{
    public class WrongArgumentTypeVirtualClass
    {
        public class WrapVirtualClass
        {
            public virtual Tuple<T1, T2> Wrap<T1, T2>(T1 a, string b) => default;
        }

        public class ForEachScopeVirtualClass
        {
            public virtual void ForEachScope<TState2>(Action<object, string> callback, TState2 state)
            {
            }
        }

        public class WrapIntStringVirtualClass
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public virtual Tuple<int, string> WrapIntString(int a, int b) => null;
        }
    }
}
