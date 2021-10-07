// <copyright file="WrongNumberOfArgumentsVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongNumberOfArguments
{
    public class WrongNumberOfArgumentsVirtualClass
    {
        public class GetDefaultVirtualClass
        {
            public virtual T GetDefault<T>(int wrong) => default;
        }

        public class WrapVirtualClass
        {
            public virtual Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b, int wrong) => default;
        }

        public class ForEachScopeVirtualClass
        {
            public virtual void ForEachScope<TState2>(Action<object, TState2> callback, TState2 state, int wrong)
            {
            }
        }

        public class GetDefaultIntVirtualClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            public virtual int GetDefaultInt(int wrong) => 100;
        }

        public class GetDefaultStringVirtualClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            public virtual string GetDefaultString(int wrong) => string.Empty;
        }

        public class WrapIntStringVirtualClass
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public virtual Tuple<int, string> WrapIntString(int a, string b, int wrong) => null;
        }
    }
}
