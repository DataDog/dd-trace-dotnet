#nullable enable

using System;
using System.Text;

namespace Datadog.Trace.ServiceFabric
{
    internal static class ServiceRemotingRequestMessageHeaderExtensions
    {
        public static bool TryAddHeader(this IServiceRemotingRequestMessageHeader headers, string headerName, string headerValue)
        {
            if (!headers.TryGetHeaderValue(headerName, out _))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(headerValue);
                headers.AddHeader(headerName, bytes);
                return true;
            }

            return false;
        }

        public static string? TryGetHeaderValueString(this IServiceRemotingRequestMessageHeader headers, string headerName)
        {
            if (headers.TryGetHeaderValue(headerName, out var bytes) && bytes?.Length > 0)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            return null;
        }
    }
}
