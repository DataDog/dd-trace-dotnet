// <copyright file="HotChocolateCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate;
using Datadog.Trace.Processors;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    public class HotChocolateCommonTests
    {
        private const int MaxLen = TruncatorTagsProcessor.MaxMetaValLen;

        [Fact]
        public void ConstructErrorMessage_SmallError_NotTruncated()
        {
            var errors = new List<IError>
            {
                new TestError
                {
                    Message = "boom",
                    Locations = new object[] { new TestLocation { Line = 3, Column = 7 } },
                },
            };

            var result = HotChocolateCommon.ConstructErrorMessage(errors);

            result.Length.Should().BeLessThan(MaxLen);
            result.Should().Contain("boom");
            result.Should().Contain("\"line\": 3");
            result.Should().Contain("\"column\": 7");
        }

        [Fact]
        public void ConstructErrorMessage_LongMessage_TruncatedToMaxLen()
        {
            var errors = new List<IError>
            {
                new TestError
                {
                    Message = new string('x', MaxLen + 100),
                    Locations = new object[] { new TestLocation { Line = 1, Column = 1 } },
                },
            };

            var result = HotChocolateCommon.ConstructErrorMessage(errors);

            result.Length.Should().Be(MaxLen);
        }

        private sealed class TestError : IError
        {
            public string? Code { get; set; }

            public IEnumerable? Locations { get; set; }

            public string? Message { get; set; }

            public IPath? Path { get; set; }

            public Exception? Exception { get; set; }

            public IReadOnlyDictionary<string, object>? Extensions { get; set; }
        }

        private sealed class TestLocation
        {
            public int Line { get; set; }

            public int Column { get; set; }
        }
    }
}
