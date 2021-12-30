using CommandLine;

namespace LargePayload
{
    public class Options
    {
        [Option('t', "traces", Required = false, HelpText = "How many traces")]
        public int Traces { get; set; } = 100;

        [Option('s', "spans", Required = false, HelpText = "How many spans per trace")]
        public int SpansPerTrace { get; set; } = 1999;

        [Option('f', "filler", Required = false, HelpText = "How characters to add to filler tag")]
        public int SpanTagFillerLength { get; set; } = 13980;
    }
}
