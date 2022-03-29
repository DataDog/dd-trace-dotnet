// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IPublicStaticGetReferenceType
        {
            string[] PublicStaticGetReferenceType { get; }
        }

        public interface IInternalStaticGetReferenceType
        {
            string[] InternalStaticGetReferenceType { get; }
        }

        public interface IProtectedStaticGetReferenceType
        {
            string[] ProtectedStaticGetReferenceType { get; }
        }

        public interface IPrivateStaticGetReferenceType
        {
            string[] PrivateStaticGetReferenceType { get; }
        }

        // *

        public interface IPublicStaticGetSetReferenceType
        {
            string[] PublicStaticGetSetReferenceType { get; set; }
        }

        public interface IInternalStaticGetSetReferenceType
        {
            string[] InternalStaticGetSetReferenceType { get; set; }
        }

        public interface IProtectedStaticGetSetReferenceType
        {
            string[] ProtectedStaticGetSetReferenceType { get; set; }
        }

        public interface IPrivateStaticGetSetReferenceType
        {
            string[] PrivateStaticGetSetReferenceType { get; set; }
        }

        // *

        public interface IPublicGetReferenceType
        {
            string[] PublicGetReferenceType { get; }
        }

        public interface IInternalGetReferenceType
        {
            string[] InternalGetReferenceType { get; }
        }

        public interface IProtectedGetReferenceType
        {
            string[] ProtectedGetReferenceType { get; }
        }

        public interface IPrivateGetReferenceType
        {
            string[] PrivateGetReferenceType { get; }
        }

        // *

        public interface IPublicGetSetReferenceType
        {
            string[] PublicGetSetReferenceType { get; set; }
        }

        public interface IInternalGetSetReferenceType
        {
            string[] InternalGetSetReferenceType { get; set; }
        }

        public interface IProtectedGetSetReferenceType
        {
            string[] ProtectedGetSetReferenceType { get; set; }
        }

        public interface IPrivateGetSetReferenceType
        {
            string[] PrivateGetSetReferenceType { get; set; }
        }

        // *

        public interface IPublicStaticOnlySet
        {
            [Duck(Name = "PublicStaticGetSetReferenceType")]
            string[] PublicStaticOnlySet { set; }
        }

        // *

        public interface IIndex
        {
            string[] this[string index] { get; set; }
        }
    }
}
