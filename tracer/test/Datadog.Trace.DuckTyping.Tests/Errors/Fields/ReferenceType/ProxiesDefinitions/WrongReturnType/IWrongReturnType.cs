// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IPublicStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            int PublicStaticReadonlyReferenceTypeField { get; }
        }

        public interface IInternalStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            int InternalStaticReadonlyReferenceTypeField { get; }
        }

        public interface IProtectedStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            int ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public interface IPrivateStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            int PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public interface IPublicStaticReferenceTypeField
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            int PublicStaticReferenceTypeField { get; set; }
        }

        public interface IInternalStaticReferenceTypeField
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            int InternalStaticReferenceTypeField { get; set; }
        }

        public interface IProtectedStaticReferenceTypeField
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            int ProtectedStaticReferenceTypeField { get; set; }
        }

        public interface IPrivateStaticReferenceTypeField
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            int PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlyReferenceTypeField
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            int PublicReadonlyReferenceTypeField { get; }
        }

        public interface IInternalReadonlyReferenceTypeField
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            int InternalReadonlyReferenceTypeField { get; }
        }

        public interface IProtectedReadonlyReferenceTypeField
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            int ProtectedReadonlyReferenceTypeField { get; }
        }

        public interface IPrivateReadonlyReferenceTypeField
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            int PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public interface IPublicReferenceTypeField
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            int PublicReferenceTypeField { get; set; }
        }

        public interface IInternalReferenceTypeField
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            int InternalReferenceTypeField { get; set; }
        }

        public interface IProtectedReferenceTypeField
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            int ProtectedReferenceTypeField { get; set; }
        }

        public interface IPrivateReferenceTypeField
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            int PrivateReferenceTypeField { get; set; }
        }
    }
}
