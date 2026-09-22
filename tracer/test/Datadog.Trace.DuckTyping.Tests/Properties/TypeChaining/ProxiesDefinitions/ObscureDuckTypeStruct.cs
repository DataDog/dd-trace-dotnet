// <copyright file="ObscureDuckTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    public struct ObscureDuckTypeStruct
    {
        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PublicStaticGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct InternalStaticGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct ProtectedStaticGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PrivateStaticGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PublicStaticGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct InternalStaticGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct ProtectedStaticGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PrivateStaticGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PublicGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct InternalGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct ProtectedGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PrivateGetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PublicGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct InternalGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct ProtectedGetSetSelfType;

        [Duck(FallbackToBaseTypes = true)]
        public DummyFieldStruct PrivateGetSetSelfType;

        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetSelfType")]
        public ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType;
    }
}
