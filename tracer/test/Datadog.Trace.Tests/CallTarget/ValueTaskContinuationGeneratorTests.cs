// <copyright file="ValueTaskContinuationGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class ValueTaskContinuationGeneratorTests
    {
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            return returnValue;
        }

        [Fact]
        public async Task SuccessTest()
        {
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, in state);

            await cTask;

            async ValueTask GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task ExceptionTest()
        {
            Exception ex = null;

            // Normal
            ex = await Assert.ThrowsAsync<CustomException>(() => GetPreviousTask().AsTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            // Using the continuation
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask>();
            var state = CallTargetState.GetDefault();
            ex = await Assert.ThrowsAsync<CustomException>(() => tcg.SetContinuation(this, GetPreviousTask(), null, in state).AsTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            async ValueTask GetPreviousTask()
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
            await Assert.ThrowsAsync<CustomCancellationException>(() => task.AsTask());
            Assert.True(task.IsCanceled);

            // Using the continuation
            task = GetPreviousTask();
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask>();
            var state = CallTargetState.GetDefault();
            await Assert.ThrowsAsync<CustomCancellationException>(() => tcg.SetContinuation(this, task, null, in state).AsTask());
            Assert.True(task.IsCanceled);

            ValueTask GetPreviousTask()
            {
                var cts = new CancellationTokenSource();

                var task = Task.FromResult(true).ContinueWith(
                    _ =>
                    {
                        cts.Cancel();
                        throw new CustomCancellationException(cts.Token);
                    },
                    cts.Token);

                return new ValueTask(task);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SynchronizationContextTest(bool preserveContext)
        {
            ContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTask> tcg;

            if (preserveContext)
            {
                tcg = new ValueTaskContinuationGenerator<PreserveContextTests, ValueTaskContinuationGeneratorTests, ValueTask>();
            }
            else
            {
                tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask>();
            }

            var synchronizationContext = new CustomSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            var tcs = new TaskCompletionSource<bool>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(tcs.Task), null, in state).AsTask();

            tcs.TrySetResult(true);
#pragma warning disable xUnit1031 // Test methods should not use Blocking operations
            Task.WaitAny(cTask, synchronizationContext.Task);
#pragma warning restore xUnit1031

            // If preserving context, the continuation should be posted to the synchronization context and cTask should never complete
            // If not, the cTask should complete without using the synchronization context
            var notCompletedTask = preserveContext ? cTask : synchronizationContext.Task;

            Assert.False(notCompletedTask.IsCompleted);

            async ValueTask GetPreviousTask(Task task)
            {
                await task.ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task SuccessGenericTest()
        {
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask<bool>, bool>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, in state);

            await cTask;

            async ValueTask<bool> GetPreviousTask()
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
            ex = await Assert.ThrowsAsync<CustomException>(() => GetPreviousTask().AsTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            // Using the continuation
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask<bool>, bool>();
            var state = CallTargetState.GetDefault();
            ex = await Assert.ThrowsAsync<CustomException>(() => tcg.SetContinuation(this, GetPreviousTask(), null, in state).AsTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            async ValueTask<bool> GetPreviousTask()
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
            await Assert.ThrowsAsync<CustomCancellationException>(() => task.AsTask());
            Assert.True(task.IsCanceled);

            // Using the continuation
            task = GetPreviousTask();
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask<bool>, bool>();
            var state = CallTargetState.GetDefault();
            await Assert.ThrowsAsync<CustomCancellationException>(() => tcg.SetContinuation(this, task, null, in state).AsTask());
            Assert.True(task.IsCanceled);

            ValueTask<bool> GetPreviousTask()
            {
                var cts = new CancellationTokenSource();

                var task = Task.FromResult(true).ContinueWith<bool>(
                    _ =>
                    {
                        cts.Cancel();
                        throw new CustomCancellationException(cts.Token);
                    },
                    cts.Token);

                return new ValueTask<bool>(task);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SynchronizationContextGenericTest(bool preserveContext)
        {
            ContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTask<bool>> tcg;

            if (preserveContext)
            {
                tcg = new ValueTaskContinuationGenerator<PreserveContextTests, ValueTaskContinuationGeneratorTests, ValueTask<bool>, bool>();
            }
            else
            {
                tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask<bool>, bool>();
            }

            var synchronizationContext = new CustomSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            var tcs = new TaskCompletionSource<bool>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(tcs.Task), null, in state).AsTask();

            // After setting the continuation, we resolve the task completion source.
            tcs.TrySetResult(true);
#pragma warning disable xUnit1031 // Test methods should not use Blocking operations
            Task.WaitAny(cTask, synchronizationContext.Task);
#pragma warning restore

            // If preserving context, the continuation should be posted to the synchronization context and cTask should never complete
            // If not, the cTask should complete without using the synchronization context
            var notCompletedTask = preserveContext ? cTask : synchronizationContext.Task;

            Assert.False(notCompletedTask.IsCompleted);

            async ValueTask<bool> GetPreviousTask(Task task)
            {
                await task.ConfigureAwait(false);
                return true;
            }
        }

        [Fact]
        public async Task SuccessGenericDuckTypeTest()
        {
            var tcg = new ValueTaskContinuationGenerator<IntegrationWithDuckType, ValueTaskContinuationGeneratorTests, ValueTask<ReturnValue>, ReturnValue>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, in state);

            var rValue = await cTask;
            Assert.Equal("ReturnValue[Modified]", rValue.Value);

            async ValueTask<ReturnValue> GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return new ReturnValue
                {
                    Value = "ReturnValue"
                };
            }
        }

        [Fact]
        public async Task SuccessGenericKnownTypeTest()
        {
            var tcg = new ValueTaskContinuationGenerator<IntegrationWithKnownType, ValueTaskContinuationGeneratorTests, ValueTask<string>, string>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, in state);

            var rValue = await cTask;
            Assert.Equal("ReturnValue[Modified]", rValue);

            async ValueTask<string> GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return "ReturnValue";
            }
        }

        [Fact]
        public async Task SuccessGenericKnownTypeAlreadyCompletedTest()
        {
            var tcg = new ValueTaskContinuationGenerator<IntegrationWithKnownType, ValueTaskContinuationGeneratorTests, ValueTask<string>, string>();
            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, new ValueTask<string>("ReturnValue"), null, in state);

            var rValue = await cTask;
            Assert.Equal("ReturnValue[Modified]", rValue);
        }

        [Theory]
        [CombinatorialData]
        public async Task ThrowingContinuationKeepsOriginalResultTest(bool alreadyCompleted)
        {
            var tcg = new ValueTaskContinuationGenerator<IntegrationThatThrows, ValueTaskContinuationGeneratorTests, ValueTask<string>, string>();
            var state = CallTargetState.GetDefault();
            var tcs = new TaskCompletionSource<string>();
            var previousTask = alreadyCompleted ? new ValueTask<string>("ReturnValue") : new ValueTask<string>(tcs.Task);
            var cTask = tcg.SetContinuation(this, previousTask, null, in state);
            tcs.SetResult("ReturnValue");

            // An integration failing must not change what the instrumented method returns
            var rValue = await cTask;
            Assert.Equal("ReturnValue", rValue);
        }

        [Theory]
        [CombinatorialData]
        public async Task ThrowingContinuationNoResultTest(bool alreadyCompleted)
        {
            var tcg = new ValueTaskContinuationGenerator<IntegrationThatThrows, ValueTaskContinuationGeneratorTests, ValueTask>();
            var state = CallTargetState.GetDefault();
            var tcs = new TaskCompletionSource<bool>();
            var previousTask = alreadyCompleted ? default : new ValueTask(tcs.Task);
            var cTask = tcg.SetContinuation(this, previousTask, null, in state);
            tcs.SetResult(true);

            // An integration failing must not reach the instrumented method's caller
            await cTask;
        }

        [Theory]
        [CombinatorialData]
        public async Task BubbleUpExceptionReachesTheCallerTest(bool alreadyCompleted, bool faulted)
        {
            // AppSec blocks by throwing an exception that wraps a CallTargetBubbleUpException, which must reach the application
            var tcg = new ValueTaskContinuationGenerator<IntegrationThatBubblesUp, ValueTaskContinuationGeneratorTests, ValueTask<string>, string>();
            var state = CallTargetState.GetDefault();
            var tcs = new TaskCompletionSource<string>();
            if (alreadyCompleted)
            {
                Complete(tcs, faulted);
            }

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                {
                    var cTask = tcg.SetContinuation(this, new ValueTask<string>(tcs.Task), null, in state);
                    Complete(tcs, faulted);
                    await cTask;
                });
            Assert.IsType<BubbleUpException>(ex.InnerException);
        }

        [Theory]
        [CombinatorialData]
        public async Task BubbleUpExceptionReachesTheCallerNoResultTest(bool alreadyCompleted, bool faulted)
        {
            var tcg = new ValueTaskContinuationGenerator<IntegrationThatBubblesUp, ValueTaskContinuationGeneratorTests, ValueTask>();
            var state = CallTargetState.GetDefault();
            var tcs = new TaskCompletionSource<bool>();
            if (alreadyCompleted)
            {
                Complete(tcs, faulted);
            }

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                {
                    var cTask = tcg.SetContinuation(this, new ValueTask(tcs.Task), null, in state);
                    Complete(tcs, faulted);
                    await cTask;
                });
            Assert.IsType<BubbleUpException>(ex.InnerException);
        }

        [Theory]
        [CombinatorialData]
        public async Task CompletedValueTaskIsOnlyConsumedOnceTest(bool integrationThrows)
        {
            // A ValueTask backed by an IValueTaskSource may only be consumed once,
            // so after reading its result we must never hand the original back to the caller
            ContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTask<string>> tcg;
            if (integrationThrows)
            {
                tcg = new ValueTaskContinuationGenerator<IntegrationThatThrows, ValueTaskContinuationGeneratorTests, ValueTask<string>, string>();
            }
            else
            {
                // Returns the original value unchanged
                tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask<string>, string>();
            }

            var state = CallTargetState.GetDefault();
            var cTask = tcg.SetContinuation(this, new ValueTask<string>(new SingleUseValueTaskSource("ReturnValue"), 0), null, in state);

            var rValue = await cTask;
            Assert.Equal("ReturnValue", rValue);
        }

        private static void Complete<T>(TaskCompletionSource<T> tcs, bool faulted)
        {
            if (faulted)
            {
                tcs.TrySetException(new CustomException("Internal Test Exception"));
            }
            else
            {
                tcs.TrySetResult(default);
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
            public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                return returnValue;
            }
        }

        internal class IntegrationWithDuckType
        {
            public interface IReturnValue
            {
                string Value { get; set; }
            }

            public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
                where TReturn : IReturnValue
            {
                returnValue.Value += "[Modified]";
                return returnValue;
            }
        }

        internal class ReturnValue
        {
            public string Value { get; set; }
        }

        internal class IntegrationWithKnownType
        {
            public static string OnAsyncMethodEnd<TTarget>(TTarget instance, string returnValue, Exception exception, in CallTargetState state)
            {
                return returnValue + "[Modified]";
            }
        }

        internal class IntegrationThatThrows
        {
            public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                throw new CustomException("Integration Exception");
            }
        }

        internal class IntegrationThatBubblesUp
        {
            public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                throw new InvalidOperationException("Blocked", new BubbleUpException());
            }
        }

        internal class BubbleUpException : CallTargetBubbleUpException
        {
        }

        internal class SingleUseValueTaskSource : IValueTaskSource<string>
        {
            private readonly string _result;
            private bool _consumed;

            public SingleUseValueTaskSource(string result)
            {
                _result = result;
            }

            public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Succeeded;

            public string GetResult(short token)
            {
                if (_consumed)
                {
                    throw new InvalidOperationException("The ValueTask result was already consumed");
                }

                _consumed = true;
                return _result;
            }

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => throw new NotSupportedException("The source is always completed");
        }
    }
}
