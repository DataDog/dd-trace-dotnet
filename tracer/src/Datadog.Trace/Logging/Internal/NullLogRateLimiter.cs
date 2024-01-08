// <copyright file="NullLogRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Logging
{
    internal class NullLogRateLimiter : ILogRateLimiter
    {
        /// <inheritdoc/>
        public bool ShouldLog(string filePath, int lineNumber, out uint skipCount)
        {
            skipCount = 0;
            return true;
        }
    }
}
