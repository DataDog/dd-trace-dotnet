// <copyright file="SupportedTypesServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
// ReSharper disable once RedundantUsingDirective
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.Debugger.Snapshots;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class SupportedTypesServiceTests
    {
        private static readonly Type[] Types =
        {
            typeof(int),
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(DateTimeOffset),
            typeof(Guid),
            typeof(string),
            typeof(int?),
            typeof(DateTime?),
            typeof(TimeSpan?),
            typeof(DateTimeOffset?),
            typeof(ConsoleColor)
        };

        [Fact]
        public void TestCanCallToString()
        {
            foreach (var type in Types)
            {
                Redaction.IsSafeToCallToString(type).Should().BeTrue($"Type {type} should be safe to call ToString on");
            }
        }
    }
}
