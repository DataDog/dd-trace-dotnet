using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor.Runtime;

internal class AssemblyInfo
{
    private ASSEMBLYMETADATA assemblyMetaData;
    public AssemblyInfo(Rewriter rewriter, AssemblyDefinition definition, int id, AppDomainInfo appDomain, string name, string path, Func<int> moduleId)
    {
        Definition = definition;
        this.Runtime = rewriter;
        this.Id = new AssemblyId(id);
        this.AppDomain = appDomain;
        this.Name = name;
        MainModule = new ModuleInfo(definition.MainModule, moduleId(), this, path);

        // TODO : complete metadata info if needed
        assemblyMetaData = new ASSEMBLYMETADATA(definition.Name.Version.Major, definition.Name.Version.Minor, definition.Name.Version.Build, definition.Name.Version.Revision);
    }

    public Rewriter Runtime { get; }

    public AssemblyDefinition Definition { get; }

    public AssemblyId Id { get; }

    public AppDomainInfo AppDomain { get; }

    public string Name { get; }

    public ModuleInfo MainModule { get; set; }

    public ASSEMBLYMETADATA AssemblyMetaData { get => assemblyMetaData; }
}
