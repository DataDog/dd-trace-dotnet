#nullable enable

using System;

#pragma warning disable SA1649 // File name must match first type name
namespace Datadog.Trace.ServiceFabric
{
    internal interface IServiceRemotingResponseMessageHeader
    {
    }

    internal interface IServiceRemotingResponseMessage
    {
        IServiceRemotingResponseMessageHeader GetHeader();
    }

    internal interface IServiceRemotingRequestMessageHeader
    {
        int MethodId { get; set; }

        int InterfaceId { get; set; }

        string? InvocationId { get; set; }

        string? MethodName { get; set; }

        void AddHeader(string headerName, byte[] headerValue);

        bool TryGetHeaderValue(string headerName, out byte[]? headerValue);
    }

    internal interface IServiceRemotingRequestMessage
    {
        IServiceRemotingRequestMessageHeader GetHeader();
    }

    internal interface IServiceRemotingRequestEventArgs
    {
        public IServiceRemotingRequestMessage? Request { get; }

        public Uri? ServiceUri { get; }

        public string? MethodName { get; }
    }

    internal interface IServiceRemotingResponseEventArgs
    {
        public IServiceRemotingResponseMessage Response { get; }

        public IServiceRemotingRequestMessage Request { get; }
    }

    internal interface IServiceRemotingFailedResponseEventArgs
    {
        public Exception? Error { get; }

        public IServiceRemotingRequestMessage Request { get; }
    }
}
#pragma warning restore SA1649 // File name must match first type name
