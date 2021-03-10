#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class ValueTaskContinuationGeneratorTests
    {
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            return returnValue;
        }

        [Fact]
        public async ValueTask SuccessTest()
        {
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask>();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());

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
            ex = await Assert.ThrowsAsync<CustomException>(() => tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault()).AsTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            async ValueTask GetPreviousTask()
            {
                await Task.Delay(1000).ConfigureAwait(false);
                throw new CustomException("Internal Test Exception");
            }
        }

        [Fact]
        public async Task SuccessGenericTest()
        {
            var tcg = new ValueTaskContinuationGenerator<ValueTaskContinuationGeneratorTests, ValueTaskContinuationGeneratorTests, ValueTask<bool>, bool>();
            var cTask = tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault());

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
            ex = await Assert.ThrowsAsync<CustomException>(() => tcg.SetContinuation(this, GetPreviousTask(), null, CallTargetState.GetDefault()).AsTask());
            Assert.Equal("Internal Test Exception", ex.Message);

            async ValueTask<bool> GetPreviousTask()
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
#endif
