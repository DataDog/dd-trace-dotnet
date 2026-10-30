// <copyright file="DuckTypeAotProcessorsTests.MethodBinding.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201, SA1202 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The member plans and method binding layer of the registry emitter.
/// </summary>
public partial class DuckTypeAotProcessorsTests
{
    [Fact]
    public void InterfaceTraversalShouldResolveBclInheritedInterfacesWhenDirectDnlibResolutionFails()
    {
        var moduleContext = ModuleDef.CreateModuleContext();
        var systemRuntimeAssemblyRef = new AssemblyRefUser("System.Runtime", new Version(0, 0, 0, 0), new PublicKeyToken("b03f5f7f11d50"));
        var module = new ModuleDefUser("DuckTypeAotIsolatedProxy.dll", Guid.NewGuid(), systemRuntimeAssemblyRef)
        {
            Context = moduleContext
        };
        var ownerType = new TypeDefUser("DuckTypeAotTests", "IsolatedProxy", module.CorLibTypes.Object.TypeDefOrRef);
        module.Types.Add(ownerType);
        var inheritedInterfaceReference = new TypeRefUser(
            module,
            "System.Runtime.CompilerServices",
            nameof(ICriticalNotifyCompletion),
            systemRuntimeAssemblyRef);
        ownerType.Interfaces.Add(new InterfaceImplUser(inheritedInterfaceReference));

        var resolveInterfaceMethod = GetEmitterStaticMethod(
            "ResolveInterfaceTypeDefForTraversal",
            typeof(TypeDef),
            typeof(ITypeDefOrRef));

        var resolvedInterface = (TypeDef?)resolveInterfaceMethod.Invoke(
            obj: null,
            parameters: new object[] { ownerType, inheritedInterfaceReference });

        resolvedInterface.Should().NotBeNull("BCL inherited interface traversal must not depend only on dnlib's direct assembly resolver");
        resolvedInterface!.FullName.Should().Be("System.Runtime.CompilerServices.ICriticalNotifyCompletion");
        resolvedInterface.FindMethod(nameof(ICriticalNotifyCompletion.UnsafeOnCompleted)).Should().NotBeNull();
    }
}
