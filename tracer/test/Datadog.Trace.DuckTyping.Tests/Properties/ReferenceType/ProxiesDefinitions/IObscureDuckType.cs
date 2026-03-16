// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [Duck(FallbackToBaseTypes = true)]
        string PublicStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        string InternalStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        string ProtectedStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        string PrivateStaticGetReferenceType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        string PublicStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        string InternalStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        string ProtectedStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        string PrivateStaticGetSetReferenceType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        string PublicGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        string InternalGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        string ProtectedGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        string PrivateGetReferenceType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        string PublicGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        string InternalGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        string ProtectedGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        string PrivateGetSetReferenceType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        string PublicStaticOnlySet { set; }

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        ValueWithType<string> PublicStaticOnlySetWithType { set; }

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        string PublicStaticOnlyGet { get; }

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        ValueWithType<string> PublicStaticOnlyGetWithType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        string this[string index] { get; set; }
    }
}
