// <copyright file="RcmCapabilitiesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Datadog.Trace.RemoteConfigurationManagement;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class RcmCapabilitiesTests
    {
        [Fact]
        public void CapabilitiesHaveNoGaps()
        {
            var fields = typeof(RcmCapabilitiesIndices).GetFields(BindingFlags.Static | BindingFlags.Public);

            fields.Should().NotBeEmpty();

            var values = new List<ulong>();

            foreach (var field in fields)
            {
                values.Add((ulong)(BigInteger)field.GetValue(null)!);
            }

            values.Sort();

            var expectedValues = Enumerable.Range(0, fields.Length)
                .Select(i => 1UL << i);

            values.Should().BeEquivalentTo(expectedValues);
        }
    }
}
