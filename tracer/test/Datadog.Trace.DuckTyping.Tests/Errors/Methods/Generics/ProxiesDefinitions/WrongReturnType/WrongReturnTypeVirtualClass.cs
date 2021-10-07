// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class GetDefaultVirtualClass
        {
            public virtual string GetDefault<T>() => default;
        }

        public class WrapVirtualClass
        {
            public virtual Tuple<T1, string> Wrap<T1, T2>(T1 a, T2 b) => default;
        }

        public class ForEachScopeVirtualClass
        {
            public virtual string ForEachScope<TState2>(Action<object, TState2> callback, TState2 state) => string.Empty;
        }

        public class GetDefaultIntVirtualClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            public virtual string GetDefaultInt() => string.Empty;
        }

        public class GetDefaultStringVirtualClass
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            public virtual int GetDefaultString() => 100;
        }

        public class WrapIntStringVirtualClass
        {
            [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public virtual string WrapIntString(int a, string b) => null;
        }
    }
}
