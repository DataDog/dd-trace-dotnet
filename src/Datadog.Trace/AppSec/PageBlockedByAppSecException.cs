// <copyright file="PageBlockedByAppSecException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.Serialization;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// This exception should only be used to signal to the end user that
    /// there page has been blocked
    /// </summary>
    internal class PageBlockedByAppSecException : Exception
    {
        public PageBlockedByAppSecException()
        {
        }

        public PageBlockedByAppSecException(string message)
            : base(message)
        {
        }

        public PageBlockedByAppSecException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PageBlockedByAppSecException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
