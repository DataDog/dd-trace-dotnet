// <copyright file="IStreamFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IStreamFactory
    {
        string Info();

        Stream GetBidirectionalStream();

#if NET5_0_OR_GREATER
        Task<Stream> GetBidirectionalStreamAsync(CancellationToken token);
#endif
    }
}
