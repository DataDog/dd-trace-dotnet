namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public interface IObscureReadonlyErrorDuckType
    {
        [Duck(Name = "_publicReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        string PublicReadonlyReferenceTypeField { get; set; }
    }
}
