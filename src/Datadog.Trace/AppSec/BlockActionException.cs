using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.AppSec
{
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
