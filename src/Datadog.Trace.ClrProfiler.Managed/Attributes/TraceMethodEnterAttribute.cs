using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Attributes
{
    /// <summary>
    /// Trace method entry
    /// </summary>
    public class TraceMethodEnterAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceMethodEnterAttribute"/> class.
        /// </summary>
        /// <param name="signature">The method signature</param>
        public TraceMethodEnterAttribute(string signature)
        {
            Signature = signature;
        }

        /// <summary>
        /// Gets or sets the signature
        /// </summary>
        public string Signature { get; set; }
    }
}
