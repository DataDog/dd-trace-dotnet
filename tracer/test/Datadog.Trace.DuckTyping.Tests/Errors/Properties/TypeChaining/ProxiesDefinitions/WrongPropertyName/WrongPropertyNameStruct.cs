// <copyright file="WrongPropertyNameStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongPropertyName
{
#pragma warning disable 649

    public struct WrongPropertyNameStruct
    {
        [DuckCopy]
        public struct PublicStaticGetSelfTypeStruct
        {
            public DummyFieldStruct NotPublicStaticGetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSelfTypeStruct
        {
            public DummyFieldStruct NotInternalStaticGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSelfTypeStruct
        {
            public DummyFieldStruct NotProtectedStaticGetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSelfTypeStruct
        {
            public DummyFieldStruct NotPrivateStaticGetSelfType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotPublicStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotInternalStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotProtectedStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotPrivateStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSelfTypeStruct
        {
            public DummyFieldStruct NotPublicGetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSelfTypeStruct
        {
            public DummyFieldStruct NotInternalGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSelfTypeStruct
        {
            public DummyFieldStruct NotProtectedGetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSelfTypeStruct
        {
            public DummyFieldStruct NotPrivateGetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotPublicGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotInternalGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotProtectedGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSetSelfTypeStruct
        {
            public DummyFieldStruct NotPrivateGetSetSelfType;
        }
    }
}
