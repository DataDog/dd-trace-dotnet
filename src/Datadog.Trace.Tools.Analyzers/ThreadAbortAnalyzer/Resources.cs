namespace Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer
{
    internal static class Resources
    {
        public const string Title = "Potential infinite loop on ThreadAbortException";
        public const string Description = "While blocks are vulnerable to infinite loop on ThreadAbortException due to a bug in the runtime. The catch block should rethrow a ThreadAbortException, or use a finally block";
        public const string MessageFormat = "Potential infinite loop - you should rethrow Exception in catch block";
        public const string CodeFixTitle = "Rethrow exception";
    }
}
