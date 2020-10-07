namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    public struct DummyFieldStruct
    {
        [Duck(Kind = DuckKind.Field)]
        public int MagicNumber;
    }
}
