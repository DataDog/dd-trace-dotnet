// <copyright file="IDuckTypeUnion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public interface IDuckTypeUnion :
        IDuckType,
        IPublicReferenceType,
        IInternalReferenceType,
        IProtectedReferenceType,
        IPrivateReferenceType
    {
    }

    public interface IPublicReferenceType
    {
        string PublicGetSetReferenceType { get; set; }
    }

    public interface IInternalReferenceType
    {
        string InternalGetSetReferenceType { get; set; }
    }

    public interface IProtectedReferenceType
    {
        string ProtectedGetSetReferenceType { get; set; }
    }

    public interface IPrivateReferenceType
    {
        string PrivateGetSetReferenceType { get; set; }
    }
}
