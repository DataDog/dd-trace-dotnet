// <copyright file="IInterfaces.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.OriginalTypeDefinitions
{
    public interface IInterfaces
    {
        public interface IMethod
        {
            bool TryGetValue(string value);
        }

        public interface IMethodWithOutParam
        {
            bool TryGetValue(out string value);
        }

        public interface IMethodWithRefParam
        {
            bool TryGetValue(ref string value);
        }

        public interface IGenericMethod
        {
            T Echo<T>(T value);
        }

        public interface IGetOnlyProperty
        {
            string Value { get; }
        }

        public interface IProperty
        {
            string Value { get; set; }
        }
    }
}
