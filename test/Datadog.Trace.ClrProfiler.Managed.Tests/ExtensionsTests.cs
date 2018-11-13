using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ExtensionsTests
    {
        private readonly Tracer _tracer;

        public ExtensionsTests()
        {
            var writerMock = new Mock<IAgentWriter>();
            _tracer = new Tracer(writerMock.Object);
        }

        [Fact]
        public void Trace_NoTask_NoException()
        {
            const string value = "value";

            var span = _tracer.StartSpan("Operation");
            object result = span.Trace(() => value);

            Assert.True(span.IsFinished);
            Assert.Equal(value, result);
        }

        [Fact]
        public void Trace_NoTask_WithException()
        {
            object result = null;

            var span = _tracer.StartSpan("Operation");

            Assert.Throws<TestException>(
                () =>
                {
                    result = span.Trace(
                        () => throw new TestException());
                });

            Assert.True(span.IsFinished);
            Assert.True(span.Error);
            Assert.Null(result);
        }

        [Fact]
        public async Task Trace_NonGenericTask_NoException()
        {
            bool flag = false;

            var span = _tracer.StartSpan("Operation");

            await (Task)span.Trace(
                () =>
                {
                    return Task.Run(
                        () => { flag = true; });
                });

            Assert.True(span.IsFinished);
            Assert.True(flag);
        }

        [Fact]
        public async Task Trace_NonGenericTask_WithException()
        {
            var span = _tracer.StartSpan("Operation");

            await Assert.ThrowsAsync<TestException>(
                async () =>
                {
                    await (Task)span.Trace(() => StartTask(true));
                });

            Assert.True(span.IsFinished);
            Assert.True(span.Error);
        }

        [Fact]
        public async Task Trace_GenericTask_NoException()
        {
            const string value = "value";

            var span = _tracer.StartSpan("Operation");

            string result = await (Task<string>)span.Trace(
                                () => StartTask(false, value));

            Assert.True(span.IsFinished);
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task Trace_GenericTask_WithException()
        {
            const string value = "value";

            var span = _tracer.StartSpan("Operation");

            await Assert.ThrowsAsync<TestException>(
                async () =>
                {
                    await (Task<string>)span.Trace(
                        () => StartTask(true, value));
                });

            Assert.True(span.IsFinished);
        }

        private Task StartTask(bool throwException)
        {
            return Task.Run(
                () =>
                {
                    if (throwException)
                    {
                        throw new TestException();
                    }
                });
        }

        private Task<T> StartTask<T>(bool throwException, T result)
        {
            return Task.Run(
                () =>
                {
                    if (throwException)
                    {
                        throw new TestException();
                    }

                    return result;
                });
        }
    }
}
