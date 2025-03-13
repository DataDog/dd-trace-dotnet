using System;

namespace Benchmarks.Trace
{
    public class BenchmarkAgent1Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent1Attribute() : base(Agent.BenchmarkAgent1)
        {
        }
    }
    public class BenchmarkAgent2Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent2Attribute() : base(Agent.BenchmarkAgent2)
        {
        }
    }
    public class BenchmarkAgent3Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent3Attribute() : base(Agent.BenchmarkAgent3)
        {
        }
    }
    public class BenchmarkAgent4Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent4Attribute() : base(Agent.BenchmarkAgent4)
        {
        }
    }
    public class BenchmarkAgent5Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent5Attribute() : base(Agent.BenchmarkAgent5)
        {
        }
    }
    public class BenchmarkAgent6Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent6Attribute() : base(Agent.BenchmarkAgent6)
        {
        }
    }
    public class BenchmarkAgent7Attribute : AgentFilterAttribute
    {
        public BenchmarkAgent7Attribute() : base(Agent.BenchmarkAgent7)
        {
        }
    }

    public abstract class AgentFilterAttribute : Attribute
    {
        public AgentFilterAttribute(Agent agent)
        {
            RunOn = agent;
        }

        public Agent RunOn { get; }

        public enum Agent
        {
            BenchmarkAgent1,
            BenchmarkAgent2,
            BenchmarkAgent3,
            BenchmarkAgent4,
            BenchmarkAgent5,
            BenchmarkAgent6,
            BenchmarkAgent7,
        }
    }
}
