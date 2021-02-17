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

        public interface ITaskString
        {
            string Result { get; }
        }
    }
}
