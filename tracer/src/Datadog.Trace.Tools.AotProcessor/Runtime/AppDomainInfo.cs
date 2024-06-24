using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tools.AotProcessor.Interfaces;

namespace Datadog.Trace.Tools.AotProcessor.Runtime;

internal class AppDomainInfo(int id, string name = "DefaultDomain")
{
    public AppDomainId Id { get; } = new AppDomainId(id);
    public string Name { get; } = name;
    public ProcessId ProcessId { get; } = new ProcessId(1);
}
