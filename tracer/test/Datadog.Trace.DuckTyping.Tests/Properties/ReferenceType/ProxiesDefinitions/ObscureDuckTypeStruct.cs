// <copyright file="ObscureDuckTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    internal struct ObscureDuckTypeStruct
    {
        public readonly string ReadonlyFieldIgnored;

        [Duck(FallbackToBaseTypes = true)]
        public string PublicStaticGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string InternalStaticGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string ProtectedStaticGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PrivateStaticGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PublicStaticGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string InternalStaticGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string ProtectedStaticGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PrivateStaticGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PublicGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string InternalGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string ProtectedGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PrivateGetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PublicGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string InternalGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string ProtectedGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true)]
        public string PrivateGetSetReferenceType;

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        public ValueWithType<string> PublicStaticOnlyGetWithType;
    }
}
