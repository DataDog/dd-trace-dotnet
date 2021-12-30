using CommandLine;

namespace TransportsTester
{
    public class Options
    {
        [Option('q', "quiet", Required = false, HelpText = $"Don't ask for user input.")]
        public bool QuietMode { get; set; }

        [Option('l', "ttl", Required = false, HelpText = "How many seconds to run for.")]
        public int SecondsToLive { get; set; } = 10;

        [Option('t', "traces", Required = false, HelpText = "How many traces to send.")]
        public int TraceCount { get; set; } = 0;
    }
}
