#if !NETSTANDARD2_0

using System;
using System.Configuration;
using System.ServiceModel.Configuration;
using System.Xml;
using System.Xml.Schema;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <inheritdoc />
    public class WcfBehaviorExtensionElement : BehaviorExtensionElement
    {
        /// <inheritdoc />
        public override Type BehaviorType
            => typeof(WcfEndpointBehavior);

        /// <inheritdoc />
        protected override object CreateBehavior()
            => new WcfEndpointBehavior();
    }
}

#endif
