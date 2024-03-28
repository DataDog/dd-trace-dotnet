#nullable enable

namespace Samples.GrpcDotNet.Services;

public enum ErrorType
{
    Throw,
    NotFound,
    Cancelled,
    DataLoss,
}
