namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class ExecutionResult
    {
        public int ReturnCode { get; set; }

        public JobResults JobResults { get; set; } = new();
    }
}
