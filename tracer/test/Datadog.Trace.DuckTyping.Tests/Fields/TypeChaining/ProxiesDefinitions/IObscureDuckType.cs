// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.TypeChaining.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [DuckField(Name = "_publicStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PublicStaticSelfTypeField { get; set; }

        [DuckField(Name = "_internalStaticSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject InternalStaticSelfTypeField { get; set; }

        [DuckField(Name = "_protectedStaticSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }

        [DuckField(Name = "_privateStaticSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateStaticSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PublicReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject InternalReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateReadonlySelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PublicSelfTypeField { get; set; }

        [DuckField(Name = "_internalSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject InternalSelfTypeField { get; set; }

        [DuckField(Name = "_protectedSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedSelfTypeField { get; set; }

        [DuckField(Name = "_privateSelfTypeField", FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicSelfTypeField", FallbackToBaseTypes = true)]
        ValueWithType<IDummyFieldObject> PublicSelfTypeFieldWithType { get; set; }
    }
}
