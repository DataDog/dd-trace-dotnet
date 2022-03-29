// <copyright file="WrongChainedReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
#pragma warning disable 649

    public struct WrongChainedReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticGetSelfTypeStruct
        {
            public WrongFieldStruct PublicStaticGetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSelfTypeStruct
        {
            public WrongFieldStruct InternalStaticGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSelfTypeStruct
        {
            public WrongFieldStruct ProtectedStaticGetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSelfTypeStruct
        {
            public WrongFieldStruct PrivateStaticGetSelfType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetSelfTypeStruct
        {
            public WrongFieldStruct PublicStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetSelfTypeStruct
        {
            public WrongFieldStruct InternalStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetSelfTypeStruct
        {
            public WrongFieldStruct ProtectedStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetSelfTypeStruct
        {
            public WrongFieldStruct PrivateStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSelfTypeStruct
        {
            public WrongFieldStruct PublicGetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSelfTypeStruct
        {
            public WrongFieldStruct InternalGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSelfTypeStruct
        {
            public WrongFieldStruct ProtectedGetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSelfTypeStruct
        {
            public WrongFieldStruct PrivateGetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSetSelfTypeStruct
        {
            public WrongFieldStruct PublicGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSetSelfTypeStruct
        {
            public WrongFieldStruct InternalGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSetSelfTypeStruct
        {
            public WrongFieldStruct ProtectedGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSetSelfTypeStruct
        {
            public WrongFieldStruct PrivateGetSetSelfType;
        }
    }
}
