// <copyright file="IValid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.Valid
{
    public interface IValid
    {
        public interface IPublicStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            string PublicStaticReadonlyReferenceTypeField { get; }
        }

        public interface IInternalStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            string InternalStaticReadonlyReferenceTypeField { get; }
        }

        public interface IProtectedStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            string ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public interface IPrivateStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            string PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public interface IPublicStaticReferenceTypeField
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            string PublicStaticReferenceTypeField { get; set; }
        }

        public interface IInternalStaticReferenceTypeField
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            string InternalStaticReferenceTypeField { get; set; }
        }

        public interface IProtectedStaticReferenceTypeField
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            string ProtectedStaticReferenceTypeField { get; set; }
        }

        public interface IPrivateStaticReferenceTypeField
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            string PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlyReferenceTypeField
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            string PublicReadonlyReferenceTypeField { get; }
        }

        public interface IInternalReadonlyReferenceTypeField
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            string InternalReadonlyReferenceTypeField { get; }
        }

        public interface IProtectedReadonlyReferenceTypeField
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            string ProtectedReadonlyReferenceTypeField { get; }
        }

        public interface IPrivateReadonlyReferenceTypeField
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            string PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public interface IPublicReferenceTypeField
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            string PublicReferenceTypeField { get; set; }
        }

        public interface IInternalReferenceTypeField
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            string InternalReferenceTypeField { get; set; }
        }

        public interface IProtectedReferenceTypeField
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            string ProtectedReferenceTypeField { get; set; }
        }

        public interface IPrivateReferenceTypeField
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            string PrivateReferenceTypeField { get; set; }
        }
    }
}
