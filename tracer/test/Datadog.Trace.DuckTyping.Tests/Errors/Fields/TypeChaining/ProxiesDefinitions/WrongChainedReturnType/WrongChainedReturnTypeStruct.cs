// <copyright file="WrongChainedReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public struct WrongChainedReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public IInvalidDummyFieldObject PublicStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public IInvalidDummyFieldObject InternalStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public IInvalidDummyFieldObject ProtectedStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public IInvalidDummyFieldObject PrivateStaticReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public IInvalidDummyFieldObject PublicStaticSelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public IInvalidDummyFieldObject InternalStaticSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public IInvalidDummyFieldObject ProtectedStaticSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public IInvalidDummyFieldObject PrivateStaticSelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public IInvalidDummyFieldObject PublicReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public IInvalidDummyFieldObject InternalReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public IInvalidDummyFieldObject ProtectedReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public IInvalidDummyFieldObject PrivateReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicSelfTypeFieldStruct
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public IInvalidDummyFieldObject PublicSelfTypeField;
        }

        [DuckCopy]
        public struct InternalSelfTypeFieldStruct
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public IInvalidDummyFieldObject InternalSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedSelfTypeFieldStruct
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public IInvalidDummyFieldObject ProtectedSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateSelfTypeFieldStruct
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public IInvalidDummyFieldObject PrivateSelfTypeField;
        }
    }
}
