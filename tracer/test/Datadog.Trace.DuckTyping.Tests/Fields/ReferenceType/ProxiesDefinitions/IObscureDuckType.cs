// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [DuckField(Name = "_publicStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string PublicStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string InternalStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string ProtectedStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string PrivateStaticReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticReferenceTypeField", FallbackToBaseTypes = true)]
        string PublicStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_internalStaticReferenceTypeField", FallbackToBaseTypes = true)]
        string InternalStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedStaticReferenceTypeField", FallbackToBaseTypes = true)]
        string ProtectedStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_privateStaticReferenceTypeField", FallbackToBaseTypes = true)]
        string PrivateStaticReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string PublicReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string InternalReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string ProtectedReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        string PrivateReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicReferenceTypeField", FallbackToBaseTypes = true)]
        string PublicReferenceTypeField { get; set; }

        [DuckField(Name = "_internalReferenceTypeField", FallbackToBaseTypes = true)]
        string InternalReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedReferenceTypeField", FallbackToBaseTypes = true)]
        string ProtectedReferenceTypeField { get; set; }

        [DuckField(Name = "_privateReferenceTypeField", FallbackToBaseTypes = true)]
        string PrivateReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReferenceTypeField", FallbackToBaseTypes = true)]
        ValueWithType<string> PublicReferenceTypeFieldWithType { get; set; }
    }
}
