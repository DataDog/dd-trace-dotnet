// <copyright file="Stubs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;

#pragma warning disable SA1649 // File name must match first type name
namespace Datadog.Trace.ServiceFabric
{
    /// <summary>
    /// For internal use only.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingRequestMessageHeader
    {
        /// <summary>
        /// Gets the method id. For internal use only.
        /// </summary>
        int MethodId { get; }

        /// <summary>
        /// Gets the method identifier. For internal use only.
        /// </summary>
        int InterfaceId { get; }

        /// <summary>
        /// Gets the invocation identifier. For internal use only.
        /// </summary>
        string? InvocationId { get; }

        /// <summary>
        /// Gets the method name. For internal use only.
        /// </summary>
        string? MethodName { get; }

        /// <summary>
        /// For internal use only.
        /// </summary>
        void AddHeader(string headerName, byte[] headerValue);

        /// <summary>
        /// For internal use only.
        /// </summary>
        bool TryGetHeaderValue(string headerName, out byte[]? headerValue);
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingRequestMessage
    {
        /// <summary>
        /// For internal use only.
        /// </summary>
        IServiceRemotingRequestMessageHeader GetHeader();
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingRequestEventArgs
    {
        /// <summary>
        /// Gets the request message. For internal use only.
        /// </summary>
        public IServiceRemotingRequestMessage? Request { get; }

        /// <summary>
        /// Gets the service URI. For internal use only.
        /// </summary>
        public Uri? ServiceUri { get; }

        /// <summary>
        /// Gets the method name. For internal use only.
        /// </summary>
        public string? MethodName { get; }
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingFailedResponseEventArgs
    {
        /// <summary>
        /// Gets the exception. For internal use only.
        /// </summary>
        public Exception? Error { get; }
    }
}
#pragma warning restore SA1649 // File name must match first type name
