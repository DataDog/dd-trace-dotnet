// <copyright file="ValidStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.Valid
{
    public struct ValidStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public IDummyFieldObject PublicStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public IDummyFieldObject InternalStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public IDummyFieldObject ProtectedStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public IDummyFieldObject PrivateStaticReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public IDummyFieldObject PublicStaticSelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public IDummyFieldObject InternalStaticSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public IDummyFieldObject ProtectedStaticSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public IDummyFieldObject PrivateStaticSelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public IDummyFieldObject PublicReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public IDummyFieldObject InternalReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public IDummyFieldObject ProtectedReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public IDummyFieldObject PrivateReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicSelfTypeFieldStruct
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public IDummyFieldObject PublicSelfTypeField;
        }

        [DuckCopy]
        public struct InternalSelfTypeFieldStruct
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public IDummyFieldObject InternalSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedSelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public IDummyFieldObject ProtectedSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateSelfTypeFieldStruct
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public IDummyFieldObject PrivateSelfTypeField;
        }
    }
}
