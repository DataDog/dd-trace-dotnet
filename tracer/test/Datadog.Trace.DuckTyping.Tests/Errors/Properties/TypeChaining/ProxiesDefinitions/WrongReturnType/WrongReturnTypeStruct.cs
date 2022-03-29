// <copyright file="WrongReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongReturnType
{
#pragma warning disable 649

    public struct WrongReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticGetSelfTypeStruct
        {
            public int PublicStaticGetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSelfTypeStruct
        {
            public int InternalStaticGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSelfTypeStruct
        {
            public int ProtectedStaticGetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSelfTypeStruct
        {
            public int PrivateStaticGetSelfType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetSelfTypeStruct
        {
            public int PublicStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetSelfTypeStruct
        {
            public int InternalStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetSelfTypeStruct
        {
            public int ProtectedStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetSelfTypeStruct
        {
            public int PrivateStaticGetSetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSelfTypeStruct
        {
            public int PublicGetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSelfTypeStruct
        {
            public int InternalGetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSelfTypeStruct
        {
            public int ProtectedGetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSelfTypeStruct
        {
            public int PrivateGetSelfType;
        }

        [DuckCopy]
        public struct PublicGetSetSelfTypeStruct
        {
            public int PublicGetSetSelfType;
        }

        [DuckCopy]
        public struct InternalGetSetSelfTypeStruct
        {
            public int InternalGetSetSelfType;
        }

        [DuckCopy]
        public struct ProtectedGetSetSelfTypeStruct
        {
            public int ProtectedGetSetSelfType;
        }

        [DuckCopy]
        public struct PrivateGetSetSelfTypeStruct
        {
            public int PrivateGetSetSelfType;
        }
    }
}
