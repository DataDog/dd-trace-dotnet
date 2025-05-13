using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExceptionReplayTestDataAttribute : Attribute
    {
        public ExceptionReplayTestDataAttribute(int expectedNumberOfSnapshotsDefault, int expectedNumberOfSnaphotsFull)
        {
            ExpectedNumberOfSnapshotsDefault = expectedNumberOfSnapshotsDefault;
            ExpectedNumberOfSnaphotsFull = expectedNumberOfSnaphotsFull;
        }

        public int ExpectedNumberOfSnapshotsDefault { get; }
        public int ExpectedNumberOfSnaphotsFull { get; }
    }

}
