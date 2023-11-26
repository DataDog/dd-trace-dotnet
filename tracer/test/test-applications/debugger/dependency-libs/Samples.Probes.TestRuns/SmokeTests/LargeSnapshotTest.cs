using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 20, skipOnFrameworks: new[] { "net5.0", "net462", "netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.1"})]
    public class LargeSnapshotTest : IRun
    {
        public void Run()
        {
            PingPong(GetPopulatedBigObject());
        }

        private BigObject PingPong(BigObject bo)
        {
            var bo2 = new BigObject().Populate();
            return bo2;
        }

        private BigObject GetPopulatedBigObject() => new BigObject().Populate();
    }

    public class BigObject
    {
        public string AtoZ { get; set; } = "The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog.";

        public List<BigObject> Children { get; set; } = new();

        public BigObject Populate(int currentDepth = 5, int currentBreadth = 30)
        {
            if (currentDepth == 0)
            {
                return this;
            }

            for (int i = 0; i < currentBreadth; i++)
            {
                BigObject child = new BigObject();
                child.Populate(currentDepth - 1, currentBreadth);
                this.Children.Add(child);
            }

            return this;
        }
    }
}
