namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public interface IObscureReadonlyErrorDuckType
    {
        [Duck(Name = "_publicReadonlyValueTypeField", Kind = DuckKind.Field)]
        int PublicReadonlyValueTypeField { get; set; }
    }
}
