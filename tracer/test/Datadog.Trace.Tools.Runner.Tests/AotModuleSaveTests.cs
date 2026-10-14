// <copyright file="AotModuleSaveTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using Datadog.Trace.Tools.Runner.Aot.Native;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Pdb;
using dnlib.DotNet.Writer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// An instrumented assembly dnlib's writer reports errors for is invalid: ILC must not compile it. An error in its symbols
/// only leaves them out.
/// </summary>
public class AotModuleSaveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dd-aot-save-" + Guid.NewGuid().ToString("N"));

    public AotModuleSaveTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ErrorsOfTheModuleAreReturned()
    {
        var state = LoadModule(withPdb: false);
        var other = new ModuleDefUser("Other.dll");
        var otherType = new TypeDefUser("Other", "Type", other.CorLibTypes.Object.TypeDefOrRef);
        other.Types.Add(otherType);

        // A type of another module that wasn't imported.
        var body = state.Module.Find("Test.Type", isReflectionName: true).FindMethod("Method").Body;
        body.Instructions.Insert(0, OpCodes.Ldtoken.ToInstruction(otherType));
        body.Instructions.Insert(1, OpCodes.Pop.ToInstruction());

        var errors = MethodBodies.Save(state, Path.Combine(_directory, "out", "Test.dll"));

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ErrorsOfTheSymbolsOnlyLeaveThemOut()
    {
        var state = LoadModule(withPdb: true);
        var other = new ModuleDefUser("Other.dll");
        var otherType = new TypeDefUser("Other", "Type", other.CorLibTypes.Object.TypeDefOrRef);
        other.Types.Add(otherType);

        // A local constant of the PDB of a type of another module that wasn't imported.
        var method = state.Module.Find("Test.Type", isReflectionName: true).FindMethod("Method");
        var scope = new PdbScope { Start = method.Body.Instructions[0] };
        scope.Constants.Add(new PdbConstant("value", new ClassSig(otherType), null!));
        method.Body.PdbMethod = new PdbMethod { Scope = scope };

        var output = Path.Combine(_directory, "out", "Test.dll");
        var errors = MethodBodies.Save(state, output);

        errors.Should().BeEmpty();
        File.Exists(output).Should().BeTrue();
        File.Exists(Path.ChangeExtension(output, ".pdb")).Should().BeFalse();
    }

    [Fact]
    public void ModulesWithoutErrorsKeepTheirSymbols()
    {
        var state = LoadModule(withPdb: true);
        var output = Path.Combine(_directory, "out", "Test.dll");

        MethodBodies.Save(state, output).Should().BeEmpty();

        File.Exists(Path.ChangeExtension(output, ".pdb")).Should().BeTrue();
    }

    private ModuleState LoadModule(bool withPdb)
    {
        var module = new ModuleDefUser("Test.dll") { Kind = ModuleKind.Dll };
        new AssemblyDefUser("Test", new Version(1, 0, 0, 0)).Modules.Add(module);
        var type = new TypeDefUser("Test", "Type", module.CorLibTypes.Object.TypeDefOrRef);
        module.Types.Add(type);
        var method = new MethodDefUser("Method", MethodSig.CreateStatic(module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Static) { Body = new CilBody() };
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(method);

        var path = Path.Combine(_directory, "Test.dll");
        var options = new ModuleWriterOptions(module);
        if (withPdb)
        {
            module.SetPdbState(new PdbState(module, PdbFileKind.PortablePDB));
            options.WritePdb = true;
        }

        module.Write(path, options);
        Directory.CreateDirectory(Path.Combine(_directory, "out"));
        return new ModuleState(1, path, ModuleDefMD.Load(path), writable: true);
    }
}
#endif
