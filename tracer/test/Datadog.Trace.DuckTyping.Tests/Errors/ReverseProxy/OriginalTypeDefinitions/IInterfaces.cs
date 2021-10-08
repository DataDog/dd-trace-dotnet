// <copyright file="IInterfaces.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions
{
    internal interface IInterfaces
    {
        internal interface IMethod
        {
            bool TryGetValue(string value);
        }

        internal interface IMethodWithOutParam
        {
            bool TryGetValue(out string value);
        }

        internal interface IMethodWithRefParam
        {
            bool TryGetValue(ref string value);
        }

        internal interface IGenericMethod
        {
            T Echo<T>(T value);
        }

        internal interface IGetOnlyProperty
        {
            string Value { get; }
        }

        internal interface IProperty
        {
            string Value { get; set; }
        }
    }
}
