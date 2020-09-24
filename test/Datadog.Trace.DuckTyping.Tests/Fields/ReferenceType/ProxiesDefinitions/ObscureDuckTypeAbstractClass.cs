namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [Duck(Name = "_publicStaticReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PublicStaticReadonlyReferenceTypeField { get; }

        [Duck(Name = "_internalStaticReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string InternalStaticReadonlyReferenceTypeField { get; }

        [Duck(Name = "_protectedStaticReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string ProtectedStaticReadonlyReferenceTypeField { get; }

        [Duck(Name = "_privateStaticReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PrivateStaticReadonlyReferenceTypeField { get; }

        // *

        [Duck(Name = "_publicStaticReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PublicStaticReferenceTypeField { get; set; }

        [Duck(Name = "_internalStaticReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string InternalStaticReferenceTypeField { get; set; }

        [Duck(Name = "_protectedStaticReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string ProtectedStaticReferenceTypeField { get; set; }

        [Duck(Name = "_privateStaticReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PrivateStaticReferenceTypeField { get; set; }

        // *

        [Duck(Name = "_publicReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PublicReadonlyReferenceTypeField { get; }

        [Duck(Name = "_internalReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string InternalReadonlyReferenceTypeField { get; }

        [Duck(Name = "_protectedReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string ProtectedReadonlyReferenceTypeField { get; }

        [Duck(Name = "_privateReadonlyReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PrivateReadonlyReferenceTypeField { get; }

        // *

        [Duck(Name = "_publicReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PublicReferenceTypeField { get; set; }

        [Duck(Name = "_internalReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string InternalReferenceTypeField { get; set; }

        [Duck(Name = "_protectedReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string ProtectedReferenceTypeField { get; set; }

        [Duck(Name = "_privateReferenceTypeField", Kind = DuckKind.Field)]
        public abstract string PrivateReferenceTypeField { get; set; }
    }
}
