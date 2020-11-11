using System.Security.Authentication.ExtendedProtection;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal abstract class TransportContext
    {
        public abstract ChannelBinding? GetChannelBinding(ChannelBindingKind kind);
    }
}
