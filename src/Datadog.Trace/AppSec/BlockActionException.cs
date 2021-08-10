// <copyright file="BlockActionException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// This exception should only be used to signal that we want to attempt write 403
    /// and the blocking page to the response streazm
    /// </summary>
    internal class BlockActionException : Exception
    {
        public BlockActionException()
        {
        }

        public BlockActionException(string message)
            : base(message)
        {
        }

        public BlockActionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected BlockActionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
