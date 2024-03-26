// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        IDummyFieldObject PublicStaticGetSelfType { get; }

        IDummyFieldObject InternalStaticGetSelfType { get; }

        IDummyFieldObject ProtectedStaticGetSelfType { get; }

        IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        IDummyFieldObject PublicGetSelfType { get; }

        IDummyFieldObject InternalGetSelfType { get; }

        IDummyFieldObject ProtectedGetSelfType { get; }

        IDummyFieldObject PrivateGetSelfType { get; }

        // *

        IDummyFieldObject PublicGetSetSelfType { get; set; }

        IDummyFieldObject InternalGetSetSelfType { get; set; }

        IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        IDummyFieldObject PrivateGetSetSelfType { get; set; }

        // *

        IDummyFieldObject PrivateDummyGetSetSelfType { get; set; }

        // *
        [Duck(Name = "PublicGetSetSelfType")]
        ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType { get; set; }
    }
}
