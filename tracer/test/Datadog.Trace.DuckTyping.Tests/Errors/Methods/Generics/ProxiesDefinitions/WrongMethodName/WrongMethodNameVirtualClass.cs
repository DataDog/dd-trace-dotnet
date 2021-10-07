// <copyright file="WrongMethodNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongMethodName
{
    public class WrongMethodNameVirtualClass
    {
        public class GetDefaultVirtualClass
        {
            public virtual T NotGetDefault<T>() => default;
        }

        public class WrapVirtualClass
        {
            public virtual Tuple<T1, T2> NotWrap<T1, T2>(T1 a, T2 b) => default;
        }

        public class ForEachScopeVirtualClass
        {
            public virtual void NotForEachScope<TState2>(Action<object, TState2> callback, TState2 state)
            {
            }
        }

        public class GetDefaultIntVirtualClass
        {
            [Duck(Name = "NotGetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            public virtual int GetDefaultInt() => 100;
        }

        public class GetDefaultStringVirtualClass
        {
            [Duck(Name = "NotGetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            public virtual string GetDefaultString() => string.Empty;
        }

        public class WrapIntStringVirtualClass
        {
            [Duck(Name = "NotWrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public virtual Tuple<int, string> WrapIntString(int a, string b) => null;
        }
    }
}
