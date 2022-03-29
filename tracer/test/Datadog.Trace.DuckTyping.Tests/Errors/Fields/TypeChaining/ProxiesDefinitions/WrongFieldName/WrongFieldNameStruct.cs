// <copyright file="WrongFieldNameStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongFieldName
{
    public struct WrongFieldNameStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "publicStaticReadonlySelfTypeField")]
            public IDummyFieldObject PublicStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "internalStaticReadonlySelfTypeField")]
            public IDummyFieldObject InternalStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "protectedStaticReadonlySelfTypeField")]
            public IDummyFieldObject ProtectedStaticReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "privateStaticReadonlySelfTypeField")]
            public IDummyFieldObject PrivateStaticReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "publicStaticSelfTypeField")]
            public IDummyFieldObject PublicStaticSelfTypeField;
        }

        [DuckCopy]
        public struct InternalStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "internalStaticSelfTypeField")]
            public IDummyFieldObject InternalStaticSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "protectedStaticSelfTypeField")]
            public IDummyFieldObject ProtectedStaticSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticSelfTypeFieldStruct
        {
            [DuckField(Name = "privateStaticSelfTypeField")]
            public IDummyFieldObject PrivateStaticSelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "publicReadonlySelfTypeField")]
            public IDummyFieldObject PublicReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "internalReadonlySelfTypeField")]
            public IDummyFieldObject InternalReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "protectedReadonlySelfTypeField")]
            public IDummyFieldObject ProtectedReadonlySelfTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlySelfTypeFieldStruct
        {
            [DuckField(Name = "privateReadonlySelfTypeField")]
            public IDummyFieldObject PrivateReadonlySelfTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicSelfTypeFieldStruct
        {
            [DuckField(Name = "publicSelfTypeField")]
            public IDummyFieldObject PublicSelfTypeField;
        }

        [DuckCopy]
        public struct InternalSelfTypeFieldStruct
        {
            [DuckField(Name = "internalSelfTypeField")]
            public IDummyFieldObject InternalSelfTypeField;
        }

        [DuckCopy]
        public struct ProtectedSelfTypeFieldStruct
        {
            [DuckField(Name = "protectedSelfTypeField")]
            public IDummyFieldObject ProtectedSelfTypeField;
        }

        [DuckCopy]
        public struct PrivateSelfTypeFieldStruct
        {
            [DuckField(Name = "privateSelfTypeField")]
            public IDummyFieldObject PrivateSelfTypeField;
        }
    }
}
