using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns
{
    public class ExceptionReplayIntentionalException : Exception
    {
        private const string Prefix = "Intentional exception that was thrown from an integration test";

        public ExceptionReplayIntentionalException()
            : base(Prefix)
        {
        }

        public ExceptionReplayIntentionalException(string message)
            : base($"{Prefix}. {message}")
        {
        }

        public ExceptionReplayIntentionalException(string message, Exception innerException)
            : base($"{Prefix}. {message}", innerException)
        {
        }
    }
}
