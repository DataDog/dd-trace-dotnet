#if NETSTANDARD
using System;
using System.Collections.Generic;
using Datadog.Trace.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.DiagnosticListeners
{
    internal class AspNetCoreDiagnosticOptions
    {
        private List<Func<HttpContext, bool>> _ignorePatterns;

        /// <summary>
        /// Gets a list of delegates that define whether or not a given request should be ignored.
        /// </summary>
        public List<Func<HttpContext, bool>> IgnorePatterns
        {
            get
            {
                if (_ignorePatterns == null)
                {
                    _ignorePatterns = new List<Func<HttpContext, bool>>();
                }

                return _ignorePatterns;
            }
        }

        /// <summary>
        /// Gets or sets a delegate that allows
        /// the modification of the created span.
        /// </summary>
        public Action<ISpan, HttpContext> OnRequest { get; set; }

        /// <summary>
        /// Gets or sets a delegate that allows
        /// the modification of the created span when error occurs.
        /// </summary>
        public Action<ISpan, Exception, HttpContext> OnError { get; set; }
    }
}
#endif
