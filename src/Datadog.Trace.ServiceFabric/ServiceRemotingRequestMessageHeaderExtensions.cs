using System;
using System.Text;
using Microsoft.ServiceFabric.Services.Remoting.V2;

namespace Datadog.Trace.ServiceFabric
{
    internal static class ServiceRemotingRequestMessageHeaderExtensions
    {
        public static bool TryAddHeader(this IServiceRemotingRequestMessageHeader headers, string headerName, PropagationContext context, Func<PropagationContext, byte[]> headerValue)
        {
            if (!headers.TryGetHeaderValue(headerName, out _))
            {
                byte[] bytes = headerValue(context);
                headers.AddHeader(headerName, bytes);
                return true;
            }

            return false;
        }

        public static int? TryGetHeaderValueInt32(this IServiceRemotingRequestMessageHeader headers, string headerName)
        {
            if (headers.TryGetHeaderValue(headerName, out byte[] bytes) && bytes?.Length == sizeof(int))
            {
                return BitConverter.ToInt32(bytes, 0);
            }

            return null;
        }

        public static ulong? TryGetHeaderValueUInt64(this IServiceRemotingRequestMessageHeader headers, string headerName)
        {
            if (headers.TryGetHeaderValue(headerName, out byte[] bytes) && bytes?.Length == sizeof(ulong))
            {
                return BitConverter.ToUInt64(bytes, 0);
            }

            return null;
        }

        public static string? TryGetHeaderValueString(this IServiceRemotingRequestMessageHeader headers, string headerName)
        {
            if (headers.TryGetHeaderValue(headerName, out byte[] bytes) && bytes?.Length > 0)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            return null;
        }
    }
}
