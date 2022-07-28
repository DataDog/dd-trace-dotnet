// <copyright file="SupportedTypesServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
// ReSharper disable once RedundantUsingDirective
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.Debugger.Snapshots;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class SupportedTypesServiceTests
    {
        private static readonly object[] Objects = { 3, DateTime.Now, TimeSpan.FromSeconds(3), DateTimeOffset.Now, Guid.NewGuid(), "Hello", new int?(5), new DateTime?(DateTime.Now), ConsoleColor.Blue };

        public static System.Collections.Generic.IEnumerable<object[]> ObjectsCanCallToStringOn() => Objects.Select(o => new object[] { o });

        [Theory]
        [MemberData(nameof(ObjectsCanCallToStringOn))]
        public void TestCanCallToString(object obj)
        {
            var type = obj.GetType();
            Assert.True(SupportedTypesService.IsSafeToCallToString(type), $"Type {type} should be safe to call ToString on");
        }
    }
}
