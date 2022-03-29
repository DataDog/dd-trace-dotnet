// <copyright file="ValidStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.Valid
{
#pragma warning disable 649

    public struct ValidStruct
    {
        [DuckCopy]
        public struct PublicStaticGetSelfTypeStruct
        {
            public DummyFieldStruct PublicStaticGetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSelfTypeStruct
        {
            public DummyFieldStruct InternalStaticGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSelfTypeStruct
        {
            public DummyFieldStruct ProtectedStaticGetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSelfTypeStruct
        {
            public DummyFieldStruct PrivateStaticGetSelfType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct PublicStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct InternalStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct ProtectedStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct PrivateStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSelfTypeStruct
        {
            public DummyFieldStruct PublicGetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSelfTypeStruct
        {
            public DummyFieldStruct InternalGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSelfTypeStruct
        {
            public DummyFieldStruct ProtectedGetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSelfTypeStruct
        {
            public DummyFieldStruct PrivateGetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSetSelfTypeStruct
        {
            public DummyFieldStruct PublicGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSetSelfTypeStruct
        {
            public DummyFieldStruct InternalGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSetSelfTypeStruct
        {
            public DummyFieldStruct ProtectedGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSetSelfTypeStruct
        {
            public DummyFieldStruct PrivateGetSetSelfType;
        }
    }
}
