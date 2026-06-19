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

        [Theory]
        [InlineData(typeof(List<int>), true)]
        [InlineData(typeof(HashSet<int>), true)]
        [InlineData(typeof(SortedList), false)]
        [InlineData(typeof(SortedList<string, int>), false)]
        [InlineData(typeof(Dictionary<string, int>), false)]
        [InlineData(typeof(ConditionalWeakTable<object, object>), false)]
        public void SupportedCollectionTypes(Type type, bool expected)
        {
            Redaction.IsSupportedCollection(type).Should().Be(expected);
        }

        [Theory]
        [InlineData(typeof(Dictionary<string, int>), true)]
        [InlineData(typeof(SortedDictionary<string, int>), true)]
        [InlineData(typeof(SortedList), true)]
        [InlineData(typeof(SortedList<string, int>), true)]
        [InlineData(typeof(Hashtable), true)]
        [InlineData(typeof(List<int>), false)]
        [InlineData(typeof(HashSet<int>), false)]
        public void SupportedDictionaryTypes(Type type, bool expected)
        {
            Redaction.IsSupportedDictionary(type).Should().Be(expected);
        }
    }
}
