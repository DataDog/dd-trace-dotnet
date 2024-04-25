// <copyright file="WithTracerTraitDiscoverer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    public class WithTracerTraitDiscoverer : ITraitDiscoverer
    {
        private const string Key = "WithTracer";
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            var attributeInfo = traitAttribute as ReflectionAttributeInfo;
            var category = attributeInfo?.Attribute as CategoryAttribute;
            yield return new KeyValuePair<string, string>(Key, "True");
        }
    }
}
