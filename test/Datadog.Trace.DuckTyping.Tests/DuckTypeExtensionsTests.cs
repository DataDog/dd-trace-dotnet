using System.Threading.Tasks;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckTypeExtensionsTests
    {
        [Fact]
        public void DuckCastTest()
        {
            Task task = (Task)Task.FromResult("Hello World");

            var iTaskString = task.DuckCast<ITaskString>();
            var objTaskString = task.DuckCast(typeof(ITaskString));

            Assert.Equal("Hello World", iTaskString.Result);
            Assert.True(iTaskString.GetType() == objTaskString.GetType());
        }

        [Fact]
        public void NullCheck()
        {
            object obj = null;
            var iTaskString = obj.DuckCast<ITaskString>();

            Assert.Null(iTaskString);
        }

        [Fact]
        public void TryDuckCastTest()
        {
            Task task = (Task)Task.FromResult("Hello World");

            bool tskResultBool = task.TryDuckCast<ITaskString>(out var tskResult);
            Assert.True(tskResultBool);
            Assert.Equal("Hello World", tskResult.Result);

            bool tskErrorBool = task.TryDuckCast<ITaskError>(out var tskResultError);
            Assert.False(tskErrorBool);
            Assert.Null(tskResultError);
        }

        [Fact]
        public void DuckAsTest()
        {
            Task task = (Task)Task.FromResult("Hello World");

            var tskResult = task.DuckAs<ITaskString>();
            var tskResultError = task.DuckAs<ITaskError>();

            Assert.Equal("Hello World", tskResult.Result);
            Assert.Null(tskResultError);
        }

        [Fact]
        public void DuckIsTest()
        {
            Task task = (Task)Task.FromResult("Hello World");

            bool bOk = task.DuckIs<ITaskString>();
            bool bError = task.DuckIs<ITaskError>();

            Assert.True(bOk);
            Assert.False(bError);
        }

        public interface ITaskString
        {
            string Result { get; }
        }

        public interface ITaskError
        {
            string ResultWrong { get; }
        }
    }
}
