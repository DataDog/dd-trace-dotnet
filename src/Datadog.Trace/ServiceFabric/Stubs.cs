#nullable enable

using System;
using System.ComponentModel;

#pragma warning disable SA1649 // File name must match first type name
namespace Datadog.Trace.ServiceFabric
{
    /// <summary>
    /// For internal use only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingRequestMessageHeader
    {
        /// <summary>
        /// Gets the method id. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        int MethodId { get; }

        /// <summary>
        /// Gets the method identifier. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        int InterfaceId { get; }

        /// <summary>
        /// Gets the invocation identifier. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        string? InvocationId { get; }

        /// <summary>
        /// Gets the method name. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        string? MethodName { get; }

        /// <summary>
        /// For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void AddHeader(string headerName, byte[] headerValue);

        /// <summary>
        /// For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        bool TryGetHeaderValue(string headerName, out byte[]? headerValue);
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingRequestMessage
    {
        /// <summary>
        /// For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        IServiceRemotingRequestMessageHeader GetHeader();
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingRequestEventArgs
    {
        /// <summary>
        /// Gets the request message. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IServiceRemotingRequestMessage? Request { get; }

        /// <summary>
        /// Gets the service URI. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Uri? ServiceUri { get; }

        /// <summary>
        /// Gets the method name. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? MethodName { get; }
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceRemotingFailedResponseEventArgs
    {
        /// <summary>
        /// Gets the exception. For internal use only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Exception? Error { get; }
    }
}
#pragma warning restore SA1649 // File name must match first type name
