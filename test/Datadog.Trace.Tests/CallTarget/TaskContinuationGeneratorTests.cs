// <copyright file="TaskContinuationGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
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
        public async Task CancelledTest()
        {
            // Normal
            var task = GetPreviousTask();
            await Assert.ThrowsAsync<CustomCancellationException>(() => task);
            Assert.Equal(TaskStatus.Canceled, task.Status);

            // Using the continuation
            var tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task>();
            task = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());
            await Assert.ThrowsAsync<CustomCancellationException>(() => task);
            Assert.Equal(TaskStatus.Canceled, task.Status);

            static Task GetPreviousTask()
            {
                var cts = new CancellationTokenSource();

                return Task.FromResult(true).ContinueWith(
                    _ =>
                    {
                        cts.Cancel();
                        throw new CustomCancellationException(cts.Token);
                    },
                    cts.Token);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SynchronizationContextTest(bool preserveContext)
        {
            ContinuationGenerator<TaskContinuationGeneratorTests, Task> tcg;

            if (preserveContext)
            {
                tcg = new TaskContinuationGenerator<PreserveContextTests, TaskContinuationGeneratorTests, Task>();
            }
            else
            {
                tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task>();
            }

            var synchronizationContext = new CustomSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());

            Task.WaitAny(cTask, synchronizationContext.Task);

            // If preserving context, the continuation should be posted to the synchronization context and cTask should never complete
            // If not, the cTask should complete without using the synchronization context
            var notCompletedTask = preserveContext ? cTask : synchronizationContext.Task;

            Assert.False(notCompletedTask.IsCompleted);

            async Task GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
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

        [Fact]
        public async Task CancelledGenericTest()
        {
            // Normal
            var task = GetPreviousTask();
            await Assert.ThrowsAsync<CustomCancellationException>(() => task);
            Assert.Equal(TaskStatus.Canceled, task.Status);

            // Using the continuation
            var tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task<bool>, bool>();
            task = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());
            await Assert.ThrowsAsync<CustomCancellationException>(() => task);
            Assert.Equal(TaskStatus.Canceled, task.Status);

            static Task<bool> GetPreviousTask()
            {
                var cts = new CancellationTokenSource();

                return Task.FromResult(true).ContinueWith<bool>(
                    _ =>
                    {
                        cts.Cancel();
                        throw new CustomCancellationException(cts.Token);
                    },
                    cts.Token);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SynchronizationContextGenericTest(bool preserveContext)
        {
            ContinuationGenerator<TaskContinuationGeneratorTests, Task<bool>> tcg;

            if (preserveContext)
            {
                tcg = new TaskContinuationGenerator<PreserveContextTests, TaskContinuationGeneratorTests, Task<bool>, bool>();
            }
            else
            {
                tcg = new TaskContinuationGenerator<TaskContinuationGeneratorTests, TaskContinuationGeneratorTests, Task<bool>, bool>();
            }

            var synchronizationContext = new CustomSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());

            Task.WaitAny(cTask, synchronizationContext.Task);

            // If preserving context, the continuation should be posted to the synchronization context and cTask should never complete
            // If not, the cTask should complete without using the synchronization context
            var notCompletedTask = preserveContext ? cTask : synchronizationContext.Task;

            Assert.False(notCompletedTask.IsCompleted);

            async Task<bool> GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return true;
            }
        }

        internal class CustomException : Exception
        {
            public CustomException(string message)
                : base(message)
            {
            }
        }

        internal class CustomCancellationException : OperationCanceledException
        {
            public CustomCancellationException(CancellationToken token)
                : base(token)
            {
            }
        }

        internal class CustomSynchronizationContext : SynchronizationContext
        {
            private readonly TaskCompletionSource<bool> _tcs = new();

            public Task Task => _tcs.Task;

            public override void Post(SendOrPostCallback d, object state)
            {
                _tcs.TrySetResult(true);
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                _tcs.TrySetResult(true);
            }
        }

        internal class PreserveContextTests
        {
            [PreserveContext]
            public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            {
                return returnValue;
            }
        }
    }
}
