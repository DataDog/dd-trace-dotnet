// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
        public virtual string PublicStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
        public virtual string InternalStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
        public virtual string ProtectedStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
        public virtual string PrivateStaticReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticReferenceTypeField")]
        public virtual string PublicStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_internalStaticReferenceTypeField")]
        public virtual string InternalStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedStaticReferenceTypeField")]
        public virtual string ProtectedStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_privateStaticReferenceTypeField")]
        public virtual string PrivateStaticReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyReferenceTypeField")]
        public virtual string PublicReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalReadonlyReferenceTypeField")]
        public virtual string InternalReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
        public virtual string ProtectedReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateReadonlyReferenceTypeField")]
        public virtual string PrivateReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicReferenceTypeField")]
        public virtual string PublicReferenceTypeField { get; set; }

        [DuckField(Name = "_internalReferenceTypeField")]
        public virtual string InternalReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedReferenceTypeField")]
        public virtual string ProtectedReferenceTypeField { get; set; }

        [DuckField(Name = "_privateReferenceTypeField")]
        public virtual string PrivateReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReferenceTypeField")]
        public virtual ValueWithType<string> PublicReferenceTypeFieldWithType { get; set; }
    }
}
