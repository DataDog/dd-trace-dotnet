namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public interface IDuckTypeUnion :
        IDuckType,
        IPublicReferenceType,
        IInternalReferenceType,
        IProtectedReferenceType,
        IPrivateReferenceType
    {
    }

    public interface IPublicReferenceType
    {
        string PublicGetSetReferenceType { get; set; }
    }

    public interface IInternalReferenceType
    {
        string InternalGetSetReferenceType { get; set; }
    }

    public interface IProtectedReferenceType
    {
        string ProtectedGetSetReferenceType { get; set; }
    }

    public interface IPrivateReferenceType
    {
        string PrivateGetSetReferenceType { get; set; }
    }
}
