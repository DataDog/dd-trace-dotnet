// <copyright file="ReverseTypeToTestAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy
{
    public class ReverseTypeToTestAttribute : Attribute
    {
        private readonly string _typeName;
        private readonly string _assembly;

        public ReverseTypeToTestAttribute(string typeName, string assembly = "Datadog.Trace.DuckTyping.Tests")
        {
            _typeName = typeName;
            _assembly = assembly;
        }

        public Type TypeToTest => Type.GetType($"{_typeName}, {_assembly}");
    }
}
