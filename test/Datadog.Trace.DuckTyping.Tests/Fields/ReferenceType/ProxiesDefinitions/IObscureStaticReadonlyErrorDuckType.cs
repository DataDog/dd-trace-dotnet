namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public interface IObscureStaticReadonlyErrorDuckType
    {
        [Duck(Name = "_publicStaticReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        string PublicStaticReadonlyReferenceTypeField { get; set; }
    }
}
