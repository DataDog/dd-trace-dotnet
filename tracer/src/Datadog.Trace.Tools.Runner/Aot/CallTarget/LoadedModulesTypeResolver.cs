// <copyright file="LoadedModulesTypeResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Resolves type references across the modules the instrumenter loaded (target assemblies, Datadog.Trace and the
/// reference closure), following type forwarders.
/// </summary>
internal sealed class LoadedModulesTypeResolver
{
    private readonly Resolver _resolver;

    public LoadedModulesTypeResolver(IEnumerable<ModuleDef> modules)
    {
        var assemblies = new AssemblyResolver { UseGAC = false, FindExactMatch = false, EnableTypeDefCache = true, EnableFrameworkRedirect = false };
        _resolver = new Resolver(assemblies);
        var context = new ModuleContext(assemblies, _resolver);
        assemblies.DefaultModuleContext = context;
        foreach (var module in modules)
        {
            // Type forwarders resolve through the context of the module that exports them.
            module.Context = context;
            if (module.Assembly is not null)
            {
                assemblies.AddToCache(module);
            }
        }
    }

    public TypeDef? Resolve(ITypeDefOrRef type)
        => type switch
        {
            TypeDef definition => definition,
            TypeRef reference => _resolver.Resolve(reference, reference.Module),
            TypeSpec { TypeSig: GenericInstSig instance } => Resolve(instance.GenericType.TypeDefOrRef),
            _ => null,
        };
}
#endif
