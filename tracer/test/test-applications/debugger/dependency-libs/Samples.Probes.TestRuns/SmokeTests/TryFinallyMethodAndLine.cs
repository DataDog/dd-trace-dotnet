namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 17, phase: 1)]
    [LogLineProbeTestData(lineNumber: 20, phase: 1)]
    [LogLineProbeTestData(lineNumber: 24, phase: 1)]
    internal class TryFinallyMethodAndLine : IRun
    {
        public void Run()
        {
            string message = "my name is slim shady";
            ReadRawMessageOriginal(message);
        }

        [LogMethodProbeTestData]
        public void ReadRawMessageOriginal(string message)
        {
            ParseContext.Initialize(this, out ParseContext ctx);
            try
            {
                ParsingPrimitivesMessages.ReadRawMessage(ref ctx, message);
            }
            finally
            {
                ctx.CopyStateTo(this);
            }
        }
    }

    internal class ParsingPrimitivesMessages
    {
        public static void ReadRawMessage(ref ParseContext parseContext, string message)
        {
            return;
        }
    }

    internal class ParseContext
    {
        public static void Initialize(TryFinallyMethodAndLine tryFinallyMethodAndLine, out ParseContext parseContext)
        {
            parseContext = new ParseContext();
            return;
        }

        public void CopyStateTo(TryFinallyMethodAndLine tryFinallyMethodAndLine)
        {
            return;
        }
    }
}
