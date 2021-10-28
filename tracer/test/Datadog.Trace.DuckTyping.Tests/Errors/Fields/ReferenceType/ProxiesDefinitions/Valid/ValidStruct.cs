// <copyright file="ValidStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.Valid
{
    public struct ValidStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            public string PublicStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            public string InternalStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            public string ProtectedStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            public string PrivateStaticReadonlyReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            public string PublicStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            public string InternalStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            public string ProtectedStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            public string PrivateStaticReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            public string PublicReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            public string InternalReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            public string ProtectedReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            public string PrivateReadonlyReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            public string PublicReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            public string InternalReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            public string ProtectedReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            public string PrivateReferenceTypeField;
        }
    }
}
