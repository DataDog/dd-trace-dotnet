using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.ProfilerLib.Tests
{
    public class InstanceMethod
    {
        internal class InstrumentedClass
        {
            public bool WasBeforeCalled { get; set; }

            public bool WasAfterCalled { get; set; }

            public bool WasOriginalCalled { get; set; }

            public bool WasExceptionCalled { get; set; }

            public Exception InterceptedException { get; set; }

            public void Original(bool b)
            {
                WasOriginalCalled = true;
                if (b)
                {
                    throw new ArgumentException();
                }
            }

            public int OriginalWithReturn(int i)
            {
                WasOriginalCalled = true;
                return i;
            }

            public async Task OriginalAsync(bool b)
            {
                WasOriginalCalled = true;
                if (b)
                {
                    throw new ArgumentException();
                }
                await Task.Delay(TimeSpan.FromTicks(1));
            }

            public async Task<int> OriginalAsyncWithReturn(int i)
            {
                WasOriginalCalled = true;
                await Task.Delay(TimeSpan.FromTicks(1));
                return i;
            }
        }

        private static Object Before(Object thisObj, bool b)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasBeforeCalled = true;
            return null;
        }

        private static void After(Object thisObj, bool b, Object context)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasAfterCalled = true;
        }

        private static void Exception(Object thisObj, bool b, Object context, Exception ex)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasExceptionCalled = true;
            thisObjTyped.InterceptedException = ex;
        }

        [Fact]
        public void InstrumentInstanceMethod_ThisObjectIsProperlyPassedAround()
        {
            Profiler.Instrument<InstrumentedClass>((x) => x.Original(default(bool)),
                () => Before(default(Object), default(bool)),
                () => After(default(Object), default(bool), default(Object)),
                () => Exception(default(Object), default(bool), default(Object), default(Exception))
                );
            var c = new InstrumentedClass();

            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            c.Original(false);

            Assert.True(c.WasBeforeCalled, "Before callback should have been called");
            Assert.True(c.WasAfterCalled, "After callback should have been called");
            Assert.False(c.WasExceptionCalled, "Exception callback should not have been called");
            Assert.True(c.WasOriginalCalled, "Original method should have been called");
        }

        [Fact]
        public void InstrumentInstanceMethodWithException_ThisObjectIsProperlyPassedAround()
        {
            Profiler.Instrument<InstrumentedClass>((x) => x.Original(default(bool)),
                () => Before(default(Object), default(bool)),
                () => After(default(Object), default(bool), default(Object)),
                () => Exception(default(Object), default(bool), default(Object), default(Exception))
                );
            var c = new InstrumentedClass();

            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            Assert.Throws<ArgumentException>(() => c.Original(true));

            Assert.True(c.WasBeforeCalled, "Before callback should have been called");
            Assert.True(c.WasExceptionCalled, "Exception callback should have been called");
            Assert.Equal(c.InterceptedException.GetType(), typeof(ArgumentException));
            Assert.False(c.WasAfterCalled, "After callback should not have been called");
            Assert.True(c.WasOriginalCalled, "Original method should have been called");
        }

        private static Object Before(Object thisObj, int i)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasBeforeCalled = true;
            return null;
        }

        private static int After(Object thisObj, int i, Object context, int ret)
        {
            Assert.Equal(i, ret);
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasAfterCalled = true;
            return ret;
        }

        private static void Exception(Object thisObj, int i, Object context, Exception ex)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasExceptionCalled = true;
            thisObjTyped.InterceptedException = ex;
        }

        [Fact]
        public void InstrumentInstanceMethodWithReturn_ReturnValueProperlyFlows()
        {
            Profiler.Instrument<InstrumentedClass>((x) => x.OriginalWithReturn(default(int)),
                () => Before(default(Object), default(int)),
                () => After(default(Object), default(int), default(Object), default(int)),
                () => Exception(default(Object), default(int), default(Object), default(Exception))
                );
            var c = new InstrumentedClass();

            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            const int val = 10;
            var ret = c.OriginalWithReturn(val);

            Assert.Equal(ret, val);
            Assert.True(c.WasBeforeCalled, "Before callback should have been called");
            Assert.False(c.WasExceptionCalled, "Exception callback should not have been called");
            Assert.True(c.WasAfterCalled, "After callback should have been called");
            Assert.True(c.WasOriginalCalled, "Original method should have been called");
        }

        private static async Task AfterAsync(Object thisObj, bool b, Object context, Task t)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasAfterCalled = true;
            try
            {
                await t;
            }
            catch(Exception ex)
            {
                thisObjTyped.InterceptedException = ex;
            }
        }

        [Fact]
        public void InstrumentInstanceMethodAsync_WaitAsync()
        {
            Profiler.Instrument<InstrumentedClass>((x) => x.OriginalAsync(default(bool)),
                () => Before(default(Object), default(bool)),
                () => AfterAsync(default(Object), default(bool), default(Object), default(Task)),
                () => Exception(default(Object), default(bool), default(Object), default(Exception))
                );
            var c = new InstrumentedClass();

            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            c.OriginalAsync(false).Wait();

            Assert.True(c.WasBeforeCalled, "Before callback should have been called");
            Assert.False(c.WasExceptionCalled, "Exception callback should not have been called");
            Assert.True(c.WasAfterCalled, "After callback should have been called");
            Assert.True(c.WasOriginalCalled, "Original method should have been called");
        }

        [Fact]
        public void InstrumentInstanceMethodAsyncException_ThrowsException()
        {
            Profiler.Instrument<InstrumentedClass>((x) => x.OriginalAsync(default(bool)),
                () => Before(default(Object), default(bool)),
                () => AfterAsync(default(Object), default(bool), default(Object), default(Task)),
                () => Exception(default(Object), default(bool), default(Object), default(Exception))
                );
            var c = new InstrumentedClass();

            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            Assert.ThrowsAsync<ArgumentException>(async () => await c.OriginalAsync(true));

            Assert.True(c.WasBeforeCalled, "Before callback should have been called");
            Assert.False(c.WasExceptionCalled, "Exception callback should not have been called");
            Assert.True(c.WasAfterCalled, "After callback should have been called");
            Assert.Equal(c.InterceptedException.GetType(), typeof(ArgumentException));
            Assert.True(c.WasOriginalCalled, "Original method should have been called");
        }

        private static async Task<int> AfterAsyncWithReturn(Object thisObj, int i, Object context, Task<int> t)
        {
            var thisObjTyped = thisObj as InstrumentedClass;
            Assert.NotNull(thisObjTyped);
            thisObjTyped.WasAfterCalled = true;
            var val = await t;
            Assert.Equal(i, val);
            return val;
        }

        [Fact]
        public void InstrumentInstanceMethodAsyncWithReturn_ReturnedValueIsCorrect()
        {
            Profiler.Instrument<InstrumentedClass>((x) => x.OriginalAsyncWithReturn(default(int)),
                () => Before(default(Object), default(int)),
                () => AfterAsyncWithReturn(default(Object), default(int), default(Object), default(Task<int>)),
                () => Exception(default(Object), default(int), default(Object), default(Exception))
                );
            var c = new InstrumentedClass();

            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            const int value = 13;
            var result = c.OriginalAsyncWithReturn(value).Result;

            Assert.Equal(value, result);
            Assert.True(c.WasBeforeCalled, "Before callback should have been called");
            Assert.False(c.WasExceptionCalled, "Exception callback should not have been called");
            Assert.True(c.WasAfterCalled, "After callback should have been called");
            Assert.True(c.WasOriginalCalled, "Original method should have been called");
        }
    }
}
