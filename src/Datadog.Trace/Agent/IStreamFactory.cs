// <copyright file="IStreamFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;

namespace Datadog.Trace.Agent
{
    internal interface IStreamFactory
    {
        string Info();

        Stream GetBidirectionalStream();
    }
}
