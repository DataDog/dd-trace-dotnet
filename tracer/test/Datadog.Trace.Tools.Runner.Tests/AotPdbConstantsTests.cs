// <copyright file="AotPdbConstantsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using Datadog.Trace.Tools.Runner.Aot.Native;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Pdb;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The PDB local constants of an enum type dnlib couldn't resolve (read as a class type with a value, which its writer
/// rejects) are written as enum constants when an instrumented assembly is saved.
/// </summary>
public class AotPdbConstantsTests
{
    [Fact]
    public void EnumConstantsOfUnresolvedTypesBecomeValueTypeConstants()
    {
        var module = new ModuleDefUser("Test.dll");
        new AssemblyDefUser("Test", new Version(1, 0, 0, 0)).Modules.Add(module);
        var type = new TypeDefUser("Test", "Type", module.CorLibTypes.Object.TypeDefOrRef);
        module.Types.Add(type);
        var method = new MethodDefUser("Method", MethodSig.CreateStatic(module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Static) { Body = new CilBody() };
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(method);

        var bindingFlags = new TypeRefUser(module, "System.Reflection", "BindingFlags", module.CorLibTypes.AssemblyRef);
        var scope = new PdbScope { Start = method.Body.Instructions[0] };
        scope.Constants.Add(new PdbConstant("all", new ClassSig(bindingFlags), 60));
        scope.Constants.Add(new PdbConstant("none", new ClassSig(module.CorLibTypes.Object.TypeDefOrRef), null!));
        method.Body.PdbMethod = new PdbMethod { Scope = scope };

        MethodBodies.NormalizeEnumConstants(module);

        scope.Constants[0].Type.Should().BeOfType<ValueTypeSig>().Which.TypeDefOrRef.Should().Be(bindingFlags);
        scope.Constants[0].Value.Should().Be(60);
        scope.Constants[1].Type.Should().BeOfType<ClassSig>("a null constant of a class type is valid");
    }
}
#endif
