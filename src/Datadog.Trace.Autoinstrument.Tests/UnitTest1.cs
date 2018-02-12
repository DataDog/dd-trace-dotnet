using Datadog.Trace.TestUtils;
using StackExchange.Redis;
using System.Linq;
using System.Threading;
using Xunit;

namespace Datadog.Trace.Autoinstrument.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            /*
            var writer = new MockWriter();
            var tracer = new Tracer(writer);
            Tracer.Instance = tracer;
            */

            RedisInstrumentation.Instrument();
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1");
            IDatabase db = redis.GetDatabase();
            for (int i = 0; i < 100; i++)
            {
                var result = db.StringGet("jkj");
            }

            Thread.Sleep(1000);
            /*
            Assert.Single(writer.Traces);
            Assert.Equal("Redis.StringGet", writer.Traces.Single().Single().OperationName);
            */
        }
    }
}
