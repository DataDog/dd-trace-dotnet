#nullable enable

using System;

#pragma warning disable SA1649 // File name must match first type name
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
namespace Datadog.Trace.ServiceFabric
{
    public interface IServiceRemotingResponseMessageHeader
    {
    }

    public interface IServiceRemotingResponseMessage
    {
        IServiceRemotingResponseMessageHeader GetHeader();
    }

    public interface IServiceRemotingRequestMessageHeader
    {
        int MethodId { get; set; }

        int InterfaceId { get; set; }

        string? InvocationId { get; set; }

        string? MethodName { get; set; }

        void AddHeader(string headerName, byte[] headerValue);

        bool TryGetHeaderValue(string headerName, out byte[]? headerValue);
    }

    public interface IServiceRemotingRequestMessage
    {
        IServiceRemotingRequestMessageHeader GetHeader();
    }

    public interface IServiceRemotingRequestEventArgs
    {
        public IServiceRemotingRequestMessage? Request { get; }

        public Uri? ServiceUri { get; }

        public string? MethodName { get; }
    }

    public interface IServiceRemotingResponseEventArgs
    {
        public IServiceRemotingResponseMessage Response { get; }

        public IServiceRemotingRequestMessage Request { get; }
    }

    public interface IServiceRemotingFailedResponseEventArgs
    {
        public Exception? Error { get; }

        public IServiceRemotingRequestMessage Request { get; }
    }
}
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore SA1649 // File name must match first type name
