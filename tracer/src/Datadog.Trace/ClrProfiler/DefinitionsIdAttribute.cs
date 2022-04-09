// <copyright file="DefinitionsIdAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class DefinitionsIdAttribute : Attribute
    {
        public DefinitionsIdAttribute(string definitionsId, string derivedDefinitionsId)
        {
            DefinitionsId = definitionsId;
            DerivedDefinitionsId = derivedDefinitionsId;
        }

        internal string DefinitionsId { get; }

        internal string DerivedDefinitionsId { get; }
    }
}
