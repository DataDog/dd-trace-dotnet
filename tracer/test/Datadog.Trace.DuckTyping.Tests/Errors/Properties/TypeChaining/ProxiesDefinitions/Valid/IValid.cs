// <copyright file="IValid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.Valid
{
    public interface IValid
    {
        public interface IPublicStaticGetSelfType
        {
            IDummyFieldObject PublicStaticGetSelfType { get; }
        }

        public interface IInternalStaticGetSelfType
        {
            IDummyFieldObject InternalStaticGetSelfType { get; }
        }

        public interface IProtectedStaticGetSelfType
        {
            IDummyFieldObject ProtectedStaticGetSelfType { get; }
        }

        public interface IPrivateStaticGetSelfType
        {
            IDummyFieldObject PrivateStaticGetSelfType { get; }
        }

        // *

        public interface IPublicStaticGetSetSelfType
        {
            IDummyFieldObject PublicStaticGetSetSelfType { get; set; }
        }

        public interface IInternalStaticGetSetSelfType
        {
            IDummyFieldObject InternalStaticGetSetSelfType { get; set; }
        }

        public interface IProtectedStaticGetSetSelfType
        {
            IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }
        }

        public interface IPrivateStaticGetSetSelfType
        {
            IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public interface IPublicGetSelfType
        {
            IDummyFieldObject PublicGetSelfType { get; }
        }

        public interface IInternalGetSelfType
        {
            IDummyFieldObject InternalGetSelfType { get; }
        }

        public interface IProtectedGetSelfType
        {
            IDummyFieldObject ProtectedGetSelfType { get; }
        }

        public interface IPrivateGetSelfType
        {
            IDummyFieldObject PrivateGetSelfType { get; }
        }

        // *

        public interface IPublicGetSetSelfType
        {
            IDummyFieldObject PublicGetSetSelfType { get; set; }
        }

        public interface IInternalGetSetSelfType
        {
            IDummyFieldObject InternalGetSetSelfType { get; set; }
        }

        public interface IProtectedGetSetSelfType
        {
            IDummyFieldObject ProtectedGetSetSelfType { get; set; }
        }

        public interface IPrivateGetSetSelfType
        {
            IDummyFieldObject PrivateGetSetSelfType { get; set; }
        }

        // *

        public interface IPrivateDummyGetSetSelfType
        {
            IDummyFieldObject PrivateDummyGetSetSelfType { get; set; }
        }
    }
}
