// <copyright file="IWrongFieldName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongFieldName
{
    public interface IWrongFieldName
    {
        public interface IPublicStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "publicStaticReadonlyReferenceTypeField")]
            string PublicStaticReadonlyReferenceTypeField { get; }
        }

        public interface IInternalStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "internalStaticReadonlyReferenceTypeField")]
            string InternalStaticReadonlyReferenceTypeField { get; }
        }

        public interface IProtectedStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "protectedStaticReadonlyReferenceTypeField")]
            string ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public interface IPrivateStaticReadonlyReferenceTypeField
        {
            [DuckField(Name = "privateStaticReadonlyReferenceTypeField")]
            string PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public interface IPublicStaticReferenceTypeField
        {
            [DuckField(Name = "publicStaticReferenceTypeField")]
            string PublicStaticReferenceTypeField { get; set; }
        }

        public interface IInternalStaticReferenceTypeField
        {
            [DuckField(Name = "internalStaticReferenceTypeField")]
            string InternalStaticReferenceTypeField { get; set; }
        }

        public interface IProtectedStaticReferenceTypeField
        {
            [DuckField(Name = "protectedStaticReferenceTypeField")]
            string ProtectedStaticReferenceTypeField { get; set; }
        }

        public interface IPrivateStaticReferenceTypeField
        {
            [DuckField(Name = "privateStaticReferenceTypeField")]
            string PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlyReferenceTypeField
        {
            [DuckField(Name = "publicReadonlyReferenceTypeField")]
            string PublicReadonlyReferenceTypeField { get; }
        }

        public interface IInternalReadonlyReferenceTypeField
        {
            [DuckField(Name = "internalReadonlyReferenceTypeField")]
            string InternalReadonlyReferenceTypeField { get; }
        }

        public interface IProtectedReadonlyReferenceTypeField
        {
            [DuckField(Name = "protectedReadonlyReferenceTypeField")]
            string ProtectedReadonlyReferenceTypeField { get; }
        }

        public interface IPrivateReadonlyReferenceTypeField
        {
            [DuckField(Name = "privateReadonlyReferenceTypeField")]
            string PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public interface IPublicReferenceTypeField
        {
            [DuckField(Name = "publicReferenceTypeField")]
            string PublicReferenceTypeField { get; set; }
        }

        public interface IInternalReferenceTypeField
        {
            [DuckField(Name = "internalReferenceTypeField")]
            string InternalReferenceTypeField { get; set; }
        }

        public interface IProtectedReferenceTypeField
        {
            [DuckField(Name = "protectedReferenceTypeField")]
            string ProtectedReferenceTypeField { get; set; }
        }

        public interface IPrivateReferenceTypeField
        {
            [DuckField(Name = "privateReferenceTypeField")]
            string PrivateReferenceTypeField { get; set; }
        }
    }
}
