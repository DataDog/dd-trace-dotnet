// <copyright file="IWrongPropertyName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongPropertyName
{
    public interface IWrongPropertyName
    {
        public interface IPublicStaticGetSelfType
        {
            IDummyFieldObject NotPublicStaticGetSelfType { get; }
        }

        public interface IInternalStaticGetSelfType
        {
            IDummyFieldObject NotInternalStaticGetSelfType { get; }
        }

        public interface IProtectedStaticGetSelfType
        {
            IDummyFieldObject NotProtectedStaticGetSelfType { get; }
        }

        public interface IPrivateStaticGetSelfType
        {
            IDummyFieldObject NotPrivateStaticGetSelfType { get; }
        }

        // *

        public interface IPublicStaticGetSetSelfType
        {
            IDummyFieldObject NotPublicStaticGetSetSelfType { get; set; }
        }

        public interface IInternalStaticGetSetSelfType
        {
            IDummyFieldObject NotInternalStaticGetSetSelfType { get; set; }
        }

        public interface IProtectedStaticGetSetSelfType
        {
            IDummyFieldObject NotProtectedStaticGetSetSelfType { get; set; }
        }

        public interface IPrivateStaticGetSetSelfType
        {
            IDummyFieldObject NotPrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public interface IPublicGetSelfType
        {
            IDummyFieldObject NotPublicGetSelfType { get; }
        }

        public interface IInternalGetSelfType
        {
            IDummyFieldObject NotInternalGetSelfType { get; }
        }

        public interface IProtectedGetSelfType
        {
            IDummyFieldObject NotProtectedGetSelfType { get; }
        }

        public interface IPrivateGetSelfType
        {
            IDummyFieldObject NotPrivateGetSelfType { get; }
        }

        // *

        public interface IPublicGetSetSelfType
        {
            IDummyFieldObject NotPublicGetSetSelfType { get; set; }
        }

        public interface IInternalGetSetSelfType
        {
            IDummyFieldObject NotInternalGetSetSelfType { get; set; }
        }

        public interface IProtectedGetSetSelfType
        {
            IDummyFieldObject NotProtectedGetSetSelfType { get; set; }
        }

        public interface IPrivateGetSetSelfType
        {
            IDummyFieldObject NotPrivateGetSetSelfType { get; set; }
        }

        // *

        public interface IPrivateDummyGetSetSelfType
        {
            IDummyFieldObject NotPrivateDummyGetSetSelfType { get; set; }
        }
    }
}
