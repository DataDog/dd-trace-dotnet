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
        private static readonly object[] Objects = { 3, DateTime.MinValue, TimeSpan.FromSeconds(3), DateTimeOffset.MinValue, Guid.Empty, "Hello", new int?(5), new DateTime?(DateTime.MinValue), ConsoleColor.Blue };

        [Fact]
        public void TestCanCallToString()
        {
            foreach (var obj in Objects)
            {
                var type = obj.GetType();
                Redaction.IsSafeToCallToString(type).Should().BeTrue($"Type {type} should be safe to call ToString on");
            }
        }
    }
}
