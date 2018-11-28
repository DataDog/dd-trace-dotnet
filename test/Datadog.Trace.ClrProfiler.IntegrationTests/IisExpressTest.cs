using System;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class IisExpressTest : TestHelper, IClassFixture<IisExpressFixture>, IDisposable
    {
        public IisExpressTest(string sampleAppName, ITestOutputHelper output, IisExpressFixture fixture)
            : base(sampleAppName, output)
        {
            Fixture = fixture;

            // set the output for each test
            Fixture.Output = output;

            if (!Fixture.IsRunning)
            {
                Fixture.StartIis(sampleAppName);
            }
        }

        protected IisExpressFixture Fixture { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // disconnect the output after each test is done
                // since it can't be used outside of the context of a test
                Fixture.Output = null;
            }
        }
    }
}
