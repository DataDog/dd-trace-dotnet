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
        public DummyFieldStruct PublicStaticGetSelfType;
        public DummyFieldStruct InternalStaticGetSelfType;
        public DummyFieldStruct ProtectedStaticGetSelfType;
        public DummyFieldStruct PrivateStaticGetSelfType;

        public DummyFieldStruct PublicStaticGetSetSelfType;
        public DummyFieldStruct InternalStaticGetSetSelfType;
        public DummyFieldStruct ProtectedStaticGetSetSelfType;
        public DummyFieldStruct PrivateStaticGetSetSelfType;

        public DummyFieldStruct PublicGetSelfType;
        public DummyFieldStruct InternalGetSelfType;
        public DummyFieldStruct ProtectedGetSelfType;
        public DummyFieldStruct PrivateGetSelfType;

        public DummyFieldStruct PublicGetSetSelfType;
        public DummyFieldStruct InternalGetSetSelfType;
        public DummyFieldStruct ProtectedGetSetSelfType;
        public DummyFieldStruct PrivateGetSetSelfType;

        [Duck(Name = "PublicGetSetSelfType")]
        public ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType;
    }
}
