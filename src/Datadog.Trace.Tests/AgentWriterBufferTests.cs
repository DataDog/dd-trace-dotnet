using System.Linq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class AgentWriterBufferTests
    {
        [Fact]
        public void PushPop_1ElementIn_1ElementOut()
        {
            var buffer = new AgentWriterBuffer<int>(100);
            buffer.Push(42);
            var vals = buffer.Pop();
            Assert.Equal(42, vals.Single());
        }

        [Fact]
        public void Pop_Empty_Empty()
        {
            var buffer = new AgentWriterBuffer<int>(100);
            var vals = buffer.Pop();

            Assert.False(vals.Any());
        }

        [Fact]
        public void Push_MoreThanCapacity_False()
        {
            var buffer = new AgentWriterBuffer<int>(100);
            for (int i = 0; i < 100; i++)
            {
                Assert.True(buffer.Push(i));
            }

            Assert.False(buffer.Push(101));

            // Check that one random element of the queue was replaced
            var vals = buffer.Pop();
            var replaced = 0;
            for (int i = 0; i < 100; i++)
            {
                if (vals[i] != i)
                {
                    replaced++;
                }
            }

            Assert.Equal(1, replaced);
        }
    }
}
