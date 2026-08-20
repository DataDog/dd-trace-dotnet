// <copyright file="GraphQLCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net;
using Datadog.Trace.Processors;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    public class GraphQLCommonTests
    {
        private const int MaxLen = TruncatorTagsProcessor.MaxMetaValLen;

        [Fact]
        public void ConstructErrorMessage_SmallError_NotTruncated()
        {
            var errors = new TestExecutionErrors(
                new TestExecutionError
                {
                    Message = "boom",
                    Code = "MY_CODE",
                    Path = new object[] { "user", "name" },
                    Locations = new object[] { new TestLocation { Line = 3, Column = 7 } },
                });

            var result = GraphQLCommon.ConstructErrorMessage(errors);

            result.Length.Should().BeLessThan(MaxLen);
            result.Should().Contain("boom");
            result.Should().Contain("\"code\": \"MY_CODE\"");
            result.Should().Contain("user.name");
            result.Should().Contain("\"line\": 3");
            result.Should().Contain("\"column\": 7");
        }

        [Fact]
        public void ConstructErrorMessage_LongMessage_TruncatedToMaxLen()
        {
            var errors = new TestExecutionErrors(
                new TestExecutionError
                {
                    Message = new string('x', MaxLen + 100),
                    Path = new object[] { "user", "name" },
                    Locations = new object[] { new TestLocation { Line = 1, Column = 1 } },
                });

            var result = GraphQLCommon.ConstructErrorMessage(errors);

            result.Length.Should().Be(MaxLen);
        }

        private sealed class TestExecutionErrors : IExecutionErrors
        {
            private readonly IExecutionError[] _errors;

            public TestExecutionErrors(params IExecutionError[] errors)
            {
                _errors = errors;
            }

            public int Count => _errors.Length;

            public IExecutionError this[int index] => _errors[index];
        }

        private sealed class TestExecutionError : IExecutionError
        {
            public string? Code { get; set; }

            public IEnumerable? Locations { get; set; }

            public string? Message { get; set; }

            public IEnumerable? Path { get; set; }

            public string? StackTrace { get; set; }

            public object? Instance => this;

            public Type Type => GetType();

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
                => throw new NotImplementedException();

            public override string ToString() => string.Empty;
        }

        private sealed class TestLocation
        {
            public int Line { get; set; }

            public int Column { get; set; }
        }
    }
}
