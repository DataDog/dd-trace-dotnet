// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        string PublicStaticGetReferenceType { get; }

        string InternalStaticGetReferenceType { get; }

        string ProtectedStaticGetReferenceType { get; }

        string PrivateStaticGetReferenceType { get; }

        // *

        string PublicStaticGetSetReferenceType { get; set; }

        string InternalStaticGetSetReferenceType { get; set; }

        string ProtectedStaticGetSetReferenceType { get; set; }

        string PrivateStaticGetSetReferenceType { get; set; }

        // *

        string PublicGetReferenceType { get; }

        string InternalGetReferenceType { get; }

        string ProtectedGetReferenceType { get; }

        string PrivateGetReferenceType { get; }

        // *

        string PublicGetSetReferenceType { get; set; }

        string InternalGetSetReferenceType { get; set; }

        string ProtectedGetSetReferenceType { get; set; }

        string PrivateGetSetReferenceType { get; set; }

        // *

        [Duck(Name = "PublicStaticGetSetReferenceType")]
        string PublicStaticOnlySet { set; }

        [Duck(Name = "PublicStaticGetSetReferenceType")]
        ValueWithType<string> PublicStaticOnlySetWithType { set; }

        [Duck(Name = "PublicStaticGetSetReferenceType")]
        string PublicStaticOnlyGet { get; }

        [Duck(Name = "PublicStaticGetSetReferenceType")]
        ValueWithType<string> PublicStaticOnlyGetWithType { get; }

        // *

        string this[string index] { get; set; }
    }
}
