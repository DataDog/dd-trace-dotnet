// <copyright file="WrongReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public struct WrongReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public int PublicStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public int InternalStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public int ProtectedStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public int PrivateStaticReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public int PublicStaticSelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public int InternalStaticSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public int ProtectedStaticSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public int PrivateStaticSelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public int PublicReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public int InternalReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public int ProtectedReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public int PrivateReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicSelfTypeFieldStruct
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public int PublicSelfTypeField;
        }

        [DuckCopy]
        public struct InternalSelfTypeFieldStruct
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public int InternalSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedSelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public int ProtectedSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateSelfTypeFieldStruct
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public int PrivateSelfTypeField;
        }
    }
}
