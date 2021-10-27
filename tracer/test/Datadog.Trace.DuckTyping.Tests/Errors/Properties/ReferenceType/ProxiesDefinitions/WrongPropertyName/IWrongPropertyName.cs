// <copyright file="IWrongPropertyName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongPropertyName
{
    public interface IWrongPropertyName
    {
        public interface IPublicStaticGetReferenceType
        {
            string NotPublicStaticGetReferenceType { get; }
        }

        public interface IInternalStaticGetReferenceType
        {
            string NotInternalStaticGetReferenceType { get; }
        }

        public interface IProtectedStaticGetReferenceType
        {
            string NotProtectedStaticGetReferenceType { get; }
        }

        public interface IPrivateStaticGetReferenceType
        {
            string NotPrivateStaticGetReferenceType { get; }
        }

        // *

        public interface IPublicStaticGetSetReferenceType
        {
            string NotPublicStaticGetSetReferenceType { get; set; }
        }

        public interface IInternalStaticGetSetReferenceType
        {
            string NotInternalStaticGetSetReferenceType { get; set; }
        }

        public interface IProtectedStaticGetSetReferenceType
        {
            string NotProtectedStaticGetSetReferenceType { get; set; }
        }

        public interface IPrivateStaticGetSetReferenceType
        {
            string NotPrivateStaticGetSetReferenceType { get; set; }
        }

        // *

        public interface IPublicGetReferenceType
        {
            string NotPublicGetReferenceType { get; }
        }

        public interface IInternalGetReferenceType
        {
            string NotInternalGetReferenceType { get; }
        }

        public interface IProtectedGetReferenceType
        {
            string NotProtectedGetReferenceType { get; }
        }

        public interface IPrivateGetReferenceType
        {
            string NotPrivateGetReferenceType { get; }
        }

        // *

        public interface IPublicGetSetReferenceType
        {
            string NotPublicGetSetReferenceType { get; set; }
        }

        public interface IInternalGetSetReferenceType
        {
            string NotInternalGetSetReferenceType { get; set; }
        }

        public interface IProtectedGetSetReferenceType
        {
            string NotProtectedGetSetReferenceType { get; set; }
        }

        public interface IPrivateGetSetReferenceType
        {
            string NotPrivateGetSetReferenceType { get; set; }
        }

        // *

        public interface IPublicStaticOnlySet
        {
            [Duck(Name = "NotPublicStaticGetSetReferenceType")]
            string PublicStaticOnlySet { set; }
        }
    }
}
