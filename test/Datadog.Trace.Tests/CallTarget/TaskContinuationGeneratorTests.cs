// <copyright file="TaskContinuationGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class TaskContinuationGeneratorTests
    {
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            return returnValue;
        }

        [Fact]
        public async Task SuccessTest()
        {
            var tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task>();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());

            await cTask;

            async Task GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task ExceptionTest()
        {
            Exception ex = null;

            // Normal
            ex = await Assert.ThrowsAsync<CustomException>(() => GetPreviousTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            // Using the continuation
            var tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task>();
            ex = await Assert.ThrowsAsync<CustomException>(() => tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault()));
            Assert.Equal("Internal Test Exception", ex.Message);

            async Task GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                throw new CustomException("Internal Test Exception");
            }
        }

        [Fact]
        public async Task SuccessGenericTest()
        {
            var tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task<bool>, bool>();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());

            await cTask;

            async Task<bool> GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return true;
            }
        }

        [Fact]
        public async Task ExceptionGenericTest()
        {
            Exception ex = null;

            // Normal
            ex = await Assert.ThrowsAsync<CustomException>(() => GetPreviousTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            // Using the continuation
            var tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task<bool>, bool>();
            ex = await Assert.ThrowsAsync<CustomException>(() => tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault()));
            Assert.Equal("Internal Test Exception", ex.Message);

            async Task<bool> GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                throw new CustomException("Internal Test Exception");
            }
        }

        internal class CustomException : Exception
        {
            public CustomException(string message)
                : base(message)
            {
            }
        }
    }
}
