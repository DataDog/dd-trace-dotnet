// <copyright file="SupportedTypesServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
// ReSharper disable once RedundantUsingDirective
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

        [Theory]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(HashSet<int>))]
        [InlineData(typeof(Dictionary<string, int>))]
        [InlineData(typeof(Hashtable))]
        [InlineData(typeof(ConditionalWeakTable<object, object>))]
        public void CollectionTypesAreNotSafeToCallToString(Type type)
        {
            Redaction.IsSafeToCallToString(type).Should().BeFalse($"Type {type} should use structural handling instead of ToString()");
        }
    }
}
