using System;
using System.Threading;
using Xunit;

namespace Datadog.ProfilerLib.Tests
{
    public class StaticMethod
    {
        private static bool wasBeforeCalled;
        private static bool wasOriginalCalled;
        private static bool wasAfterCalled;
        private static bool wasExceptionCalled;
        private static Exception interceptedException;

        private static void ResetStaticVariables()
        {
            wasAfterCalled = false;
            wasBeforeCalled = false;
            wasExceptionCalled = false;
            wasOriginalCalled = false;
            interceptedException = null;
        }

        private static Object Before(bool b)
        {
            wasBeforeCalled = true;
            return null;
        }

        private static void Original(bool b)
        {
            wasOriginalCalled = true;
            if(b)
            {
                throw new ArgumentException();
            }
        }

        private static void After(bool b, Object context)
        {
            wasAfterCalled = true;
        }

        private static void Exception(bool b, Object context, Exception ex)
        {
            wasExceptionCalled = true;
            interceptedException = ex;
        }

        [Fact]
        public void InstrumentStaticMethodWithout_CallbacksAreCalledBeforeAndAfter()
        {
            ResetStaticVariables();
            Profiler.Instrument(
                () => Original(default(bool)),
                () => Before(default(bool)),
                () => After(default(bool), default(object)),
                () => Exception(default(bool), default(object), default(Exception)));
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            Original(false);
            Assert.True(wasBeforeCalled, "Before callback should have been called");
            Assert.True(wasOriginalCalled, "Original method should have been called");
            Assert.True(wasAfterCalled, "After callback should have been called");
            Assert.False(wasExceptionCalled, "Exception callback should not have been called");
        }

        [Fact]
        public void InstrumentStaticMethodWithException_CallbacksAreCalledBeforeAndOnException()
        {
            ResetStaticVariables();
            Profiler.Instrument(
                () => Original(default(bool)),
                () => Before(default(bool)),
                () => After(default(bool), default(object)),
                () => Exception(default(bool), default(object), default(Exception)));
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            Assert.Throws<ArgumentException>(() => Original(true));
            Assert.True(wasBeforeCalled, "Before callback should have been called");
            Assert.True(wasOriginalCalled, "Original method should have been called");
            Assert.False(wasAfterCalled, "After callback should not have been called");
            Assert.True(wasExceptionCalled, "Exception callback should have been called");
            Assert.True(interceptedException is ArgumentException, "The intercepted exception should have the same type as the thrown one");
        }
    }
}
