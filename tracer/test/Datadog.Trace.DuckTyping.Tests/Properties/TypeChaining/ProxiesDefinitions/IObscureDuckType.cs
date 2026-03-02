// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PublicStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject InternalStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PublicGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject InternalGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateGetSelfType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PublicGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject InternalGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateGetSetSelfType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        IDummyFieldObject PrivateDummyGetSetSelfType { get; set; }

        // *
        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetSelfType")]
        ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType { get; set; }
    }
}
