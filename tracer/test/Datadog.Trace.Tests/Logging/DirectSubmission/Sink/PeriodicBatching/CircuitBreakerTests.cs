// <copyright file="CircuitBreakerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class CircuitBreakerTests
    {
        private const int FailuresUntilBroken = 5;
        private readonly CircuitBreaker _circuitBreaker;

        public CircuitBreakerTests()
        {
            _circuitBreaker = new CircuitBreaker(FailuresUntilBroken);
        }

        public static IEnumerable<object[]> GetAllStatuses()
            => Enum.GetValues(typeof(CircuitStatus))
                   .Cast<CircuitStatus>()
                   .Select(x => new object[] { x });

        [Fact]
        public void SuccessAfterHalfBreakClosesCircuit()
        {
            TryEnterState(CircuitStatus.HalfBroken);
            for (var i = 0; i < FailuresUntilBroken - 1; i++)
            {
                _circuitBreaker.MarkSuccess().Should().Be(CircuitStatus.Closed);
            }
        }

        [Fact]
        public void FailureAfterHalfBreakBreaksCircuit()
        {
            TryEnterState(CircuitStatus.HalfBroken);
            _circuitBreaker.MarkFailure().Should().Be(CircuitStatus.Broken);
        }

        [Fact]
        public void PermanentBreakIsNeverUndone()
        {
            TryEnterState(CircuitStatus.PermanentlyBroken);
            for (var i = 0; i < 100 - 1; i++)
            {
                _circuitBreaker.MarkSuccess().Should().Be(CircuitStatus.PermanentlyBroken);
            }
        }

        [Fact]
        public void SuccessAfterBrokenMovesToHalfBreak()
        {
            TryEnterState(CircuitStatus.Broken);
            _circuitBreaker.MarkSuccess().Should().Be(CircuitStatus.HalfBroken);
        }

        [Theory]
        [MemberData(nameof(GetAllStatuses))]
        internal void SkipDoesntChangeStatus_ExceptForBroken(CircuitStatus status)
        {
            TryEnterState(status);

            if (status == CircuitStatus.Broken)
            {
                _circuitBreaker.MarkSkipped().Should().Be(CircuitStatus.HalfBroken);
            }
            else
            {
                _circuitBreaker.MarkSkipped().Should().Be(status);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllStatuses))]
        internal void CanEnterExpectedState(CircuitStatus status)
        {
            TryEnterState(status);
        }

        private void TryEnterState(CircuitStatus required)
        {
            CircuitStatus actual = default;
            switch (required)
            {
                case CircuitStatus.Closed:
                    for (var i = 0; i < 100; i++)
                    {
                        actual = _circuitBreaker.MarkSuccess();
                    }

                    break;

                case CircuitStatus.Broken:
                    actual = _circuitBreaker.MarkSuccess();
                    for (var i = 0; i < FailuresUntilBroken; i++)
                    {
                        actual = _circuitBreaker.MarkFailure();
                    }

                    break;

                case CircuitStatus.PermanentlyBroken:
                    for (var i = 0; i < FailuresUntilBroken; i++)
                    {
                        actual = _circuitBreaker.MarkFailure();
                    }

                    break;

                case CircuitStatus.HalfBroken:
                    TryEnterState(CircuitStatus.Broken);
                    actual = _circuitBreaker.MarkSuccess();
                    break;
                default:
                    throw new InvalidOperationException("Unknown status: " + required);
            }

            actual.Should().Be(required);
        }
    }
}
