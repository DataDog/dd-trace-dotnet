// <copyright file="DuckTypeAotMetadataOnlyParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
#if NETCOREAPP2_1
using AssemblyLoadContext = Datadog.Trace.Tools.Runner.Tests.NetCore21AssemblyLoadContext;
#else
using System.Runtime.Loader;
#endif
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Xunit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using ParamAttributes = dnlib.DotNet.ParamAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Metadata-only mappings: contracts written with dnlib that declare their own Datadog.Trace.DuckTyping.DuckIgnoreAttribute (on
/// the types only), which the generator's dynamic duck typing can't read, so the generator binds them from metadata. Dynamic duck
/// typing ignores those attributes, so its outcome on the same types is the reference.
/// </summary>
[Collection(nameof(DuckTypeAotProcessorConsoleCollection))]
public class DuckTypeAotMetadataOnlyParityTests
{
    private const string ContractsNamespace = "MetadataOnlyContracts";

    // M01: interface proxy method with an `in` parameter (modreq InAttribute); the target has the same signature. Dynamic duck
    // typing defines its methods without custom modifiers, so it can't implement the interface method.
    [Fact]
    public void InterfaceInParameterShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM01",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var inAttribute = new TypeRefUser(module, "System.Runtime.InteropServices", "InAttribute", module.CorLibTypes.AssemblyRef);
                var inInt = new CModReqdSig(inAttribute, new ByRefSig(module.CorLibTypes.Int32));
                var proxy = AddInterface(module, "IInEcho", ignore);
                var echo = new MethodDefUser("Echo", MethodSig.CreateInstance(module.CorLibTypes.String, inInt), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot);
                echo.ParamDefs.Add(new ParamDefUser("value", 1, ParamAttributes.In));
                proxy.Methods.Add(echo);

                var target = AddClass(module, "InTarget", module.CorLibTypes.Object.TypeDefOrRef, out _);
                var targetEcho = new MethodDefUser("Echo", MethodSig.CreateInstance(module.CorLibTypes.String, inInt), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig) { Body = new CilBody() };
                targetEcho.ParamDefs.Add(new ParamDefUser("value", 1, ParamAttributes.In));
                targetEcho.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("echo"));
                targetEcho.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                target.Methods.Add(targetEcho);
            },
            [("IInEcho", "InTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Echo(5)"] = () =>
                {
                    var proxy = CreateProxy(c, "IInEcho", "InTarget");
                    return c.GetType($"{ContractsNamespace}.IInEcho")!.GetMethod("Echo")!.Invoke(proxy, [5]);
                },
            });

    // M02: interface proxy with an init-only setter (modreq IsExternalInit on the setter's return type).
    [Fact]
    public void InterfaceInitSetterShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM02",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var isExternalInit = new TypeRefUser(module, "System.Runtime.CompilerServices", "IsExternalInit", module.CorLibTypes.AssemblyRef);
                var proxy = AddInterface(module, "IInitProxy", ignore);
                var getter = new MethodDefUser("get_Name", MethodSig.CreateInstance(module.CorLibTypes.String), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                var setter = new MethodDefUser("set_Name", MethodSig.CreateInstance(new CModReqdSig(isExternalInit, module.CorLibTypes.Void), module.CorLibTypes.String), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                proxy.Methods.Add(getter);
                proxy.Methods.Add(setter);
                proxy.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(module.CorLibTypes.String)) { GetMethod = getter, SetMethod = setter });
                AddSettableTarget(module, "SettableTarget");
            },
            [("IInitProxy", "SettableTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["set Name=x then read proxy|target"] = () =>
                {
                    var target = Activator.CreateInstance(c.GetType($"{ContractsNamespace}.SettableTarget")!)!;
                    var proxyType = c.GetType($"{ContractsNamespace}.IInitProxy")!;
                    var proxy = DuckType.Create(proxyType, target);
                    proxyType.GetProperty("Name")!.SetValue(proxy, "x");
                    return proxyType.GetProperty("Name")!.GetValue(proxy) + "|" + target.GetType().GetProperty("Name")!.GetValue(target);
                },
            });

    // M03: class proxy whose base implements an init-only interface setter (C#: interface IInitName { string Name { init; } },
    // class Base : IInitName { public string Name { get; init; } }): the setter is virtual final with modreq IsExternalInit. The
    // predictor's final accessor rule says dynamic can't create the type; dynamic's set_Name has no modreq, doesn't match it.
    [Fact]
    public void FinalInitSetterInBaseShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM03",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var isExternalInit = new TypeRefUser(module, "System.Runtime.CompilerServices", "IsExternalInit", module.CorLibTypes.AssemblyRef);
                var initVoid = new CModReqdSig(isExternalInit, module.CorLibTypes.Void);
                var s = module.CorLibTypes.String;

                // interface IInitName { string Name { init; } }
                var initName = AddInterface(module, "IInitName", ignoreCtor: null);
                var abstractSetter = new MethodDefUser("set_Name", MethodSig.CreateInstance(initVoid, s), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                initName.Methods.Add(abstractSetter);
                initName.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(s)) { SetMethod = abstractSetter });

                // public class InitBase : IInitName { string _name; public string Name { get => _name ?? "base"; init => _name = value; } }
                var baseType = AddClass(module, "InitBase", module.CorLibTypes.Object.TypeDefOrRef, out var baseCtor);
                baseType.Interfaces.Add(new InterfaceImplUser(initName));
                var field = new FieldDefUser("_name", new FieldSig(s), FieldAttributes.Private);
                baseType.Fields.Add(field);
                var getter = new MethodDefUser("get_Name", MethodSig.CreateInstance(s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName) { Body = new CilBody() };
                var hasValue = OpCodes.Ret.ToInstruction();
                getter.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                getter.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));
                getter.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                getter.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValue));
                getter.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                getter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("base"));
                getter.Body.Instructions.Add(hasValue);
                baseType.Methods.Add(getter);
                var setter = new MethodDefUser("set_Name", MethodSig.CreateInstance(initVoid, s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName) { Body = new CilBody() };
                setter.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                setter.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                setter.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(field));
                setter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                baseType.Methods.Add(setter);
                baseType.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(s)) { GetMethod = getter, SetMethod = setter });

                // [DuckIgnore] public abstract class InitProxy : InitBase { public abstract string Value { get; } }
                var proxy = AddClass(module, "InitProxy", baseType, out _, isAbstract: true, baseCtor: baseCtor);
                proxy.CustomAttributes.Add(new CustomAttribute(ignore));
                AddAbstractProperty(proxy, "Value", s);

                AddSettableTarget(module, "SettableTarget");
            },
            [("InitProxy", "SettableTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Value|Base.Name|init Name=x via interface then Base.Name"] = () =>
                {
                    var target = Activator.CreateInstance(c.GetType($"{ContractsNamespace}.SettableTarget")!)!;
                    var proxy = DuckType.Create(c.GetType($"{ContractsNamespace}.InitProxy")!, target);
                    var value = c.GetType($"{ContractsNamespace}.InitProxy")!.GetProperty("Value")!.GetValue(proxy);
                    var baseName = c.GetType($"{ContractsNamespace}.InitBase")!.GetProperty("Name")!.GetValue(proxy);
                    c.GetType($"{ContractsNamespace}.IInitName")!.GetProperty("Name")!.SetValue(proxy, "x");
                    return value + "|" + baseName + "|" + c.GetType($"{ContractsNamespace}.InitBase")!.GetProperty("Name")!.GetValue(proxy);
                },
            });

    // M04: interface proxy whose property has a public DIM getter and a private DIM setter, over a read-only target: dynamic
    // implements the setter (reflection associates private accessors of the declared type) and fails to bind it.
    [Fact]
    public void InterfacePrivateDimSetterShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM04",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var s = module.CorLibTypes.String;
                var proxy = AddInterface(module, "IPrivSet", ignore);
                var getter = new MethodDefUser("get_Name", MethodSig.CreateInstance(s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName) { Body = new CilBody() };
                getter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("dim"));
                getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                var setter = new MethodDefUser("set_Name", MethodSig.CreateInstance(module.CorLibTypes.Void, s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName) { Body = new CilBody() };
                setter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                proxy.Methods.Add(getter);
                proxy.Methods.Add(setter);
                proxy.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(s)) { GetMethod = getter, SetMethod = setter });
                AddTarget(module);
            },
            [("IPrivSet", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Name"] = () => ReadProperty(c, "IPrivSet", "Target", "IPrivSet", "Name"),
            });

    // M05 (older, fourth review AC7/S06): interface proxy IAgg : IGen<int>, IGen<string> (T Value), target object Value.
    [Fact]
    public void GenericInterfaceTwiceShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM05",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var gen = AddInterface(module, "IGen`1", ignore);
                gen.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "T"));
                AddAbstractProperty(gen, "Value", new GenericVar(0, gen));
                var agg = AddInterface(module, "IGenTwice", ignore);
                agg.Interfaces.Add(new InterfaceImplUser(new TypeSpecUser(new GenericInstSig(new ClassSig(gen), module.CorLibTypes.Int32))));
                agg.Interfaces.Add(new InterfaceImplUser(new TypeSpecUser(new GenericInstSig(new ClassSig(gen), module.CorLibTypes.String))));
                var target = AddClass(module, "ObjTarget", module.CorLibTypes.Object.TypeDefOrRef, out _);
                var getValue = new MethodDefUser("get_Value", MethodSig.CreateInstance(module.CorLibTypes.Object), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName) { Body = new CilBody() };
                getValue.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("v"));
                getValue.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                target.Methods.Add(getValue);
                target.Properties.Add(new PropertyDefUser("Value", PropertySig.CreateInstance(module.CorLibTypes.Object)) { GetMethod = getValue });
            },
            [("IGenTwice", "ObjTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["create"] = () => CreateProxy(c, "IGenTwice", "ObjTarget") is null ? "null" : "created",
            });

    // M06: class proxy whose base implements an interface property with a virtual final getter, and whose own class declares a
    // virtual (not final) METHOD get_Name() (no property): dynamic's get_Name overrides the most derived virtual get_Name, so it
    // never tries to override the final one. The predictor's final accessor rule says it fails.
    [Fact]
    public void FinalAccessorHiddenByVirtualMethodShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM06",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var s = module.CorLibTypes.String;
                var named = AddInterface(module, "INamed", ignoreCtor: null);
                AddAbstractProperty(named, "Name", s);
                var baseType = AddClass(module, "FinalNameBase", module.CorLibTypes.Object.TypeDefOrRef, out var baseCtor);
                baseType.Interfaces.Add(new InterfaceImplUser(named));
                AddConstantProperty(module, baseType, "Name", "base", MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                var proxy = AddClass(module, "MidProxy", baseType, out _, isAbstract: true, baseCtor: baseCtor);
                proxy.CustomAttributes.Add(new CustomAttribute(ignore));
                var method = new MethodDefUser("get_Name", MethodSig.CreateInstance(s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot) { Body = new CilBody() };
                method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("mid"));
                method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                proxy.Methods.Add(method);
                AddAbstractProperty(proxy, "Value", s);
                AddTarget(module);
            },
            [("MidProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Value|Base.Name|get_Name()"] = () =>
                {
                    var proxy = CreateProxy(c, "MidProxy", "Target");
                    return c.GetType($"{ContractsNamespace}.MidProxy")!.GetProperty("Value")!.GetValue(proxy) + "|" +
                           c.GetType($"{ContractsNamespace}.FinalNameBase")!.GetProperty("Name")!.GetValue(proxy) + "|" +
                           c.GetType($"{ContractsNamespace}.MidProxy")!.GetMethod("get_Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.Invoke(proxy, null);
                },
            });

    // M07: interface proxy over a generic base interface: IProxy : IBase<string> { new string Value { get; } } (the base accessor
    // is implemented by name + effective signature).
    [Fact]
    public void GenericBaseInterfaceHiddenSameEffectiveSignatureShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM07",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var gen = AddInterface(module, "IValueBase`1", ignore);
                gen.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "T"));
                AddAbstractProperty(gen, "Value", new GenericVar(0, gen));
                var proxy = AddInterface(module, "IValueProxy", ignore);
                proxy.Interfaces.Add(new InterfaceImplUser(new TypeSpecUser(new GenericInstSig(new ClassSig(gen), module.CorLibTypes.String))));
                AddAbstractProperty(proxy, "Value", module.CorLibTypes.String);
                AddTarget(module);
            },
            [("IValueProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Value"] = () => ReadProperty(c, "IValueProxy", "Target", "IValueProxy", "Value"),
            });

    // M08: class proxy with an explicit interface implementation and a protected abstract property with the interface property's
    // name and signature: dynamic's public get_Name for the interface property overrides it.
    [Fact]
    public void InterfaceAccessorOverridesProtectedAbstractShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM08",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var s = module.CorLibTypes.String;
                var named = AddInterface(module, "IName", ignoreCtor: null);
                var interfaceGetter = AddAbstractProperty(named, "Name", s).GetMethod;
                var proxy = AddClass(module, "ProtectedNameProxy", module.CorLibTypes.Object.TypeDefOrRef, out _, isAbstract: true);
                proxy.CustomAttributes.Add(new CustomAttribute(ignore));
                proxy.Interfaces.Add(new InterfaceImplUser(named));
                var explicitGetter = new MethodDefUser("MetadataOnlyContracts.IName.get_Name", MethodSig.CreateInstance(s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName) { Body = new CilBody() };
                explicitGetter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("explicit"));
                explicitGetter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                explicitGetter.Overrides.Add(new MethodOverride(explicitGetter, interfaceGetter));
                proxy.Methods.Add(explicitGetter);
                proxy.Properties.Add(new PropertyDefUser("MetadataOnlyContracts.IName.Name", PropertySig.CreateInstance(s)) { GetMethod = explicitGetter });
                var protectedGetter = new MethodDefUser("get_Name", MethodSig.CreateInstance(s), MethodAttributes.Family | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                proxy.Methods.Add(protectedGetter);
                proxy.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(s)) { GetMethod = protectedGetter });
                AddAbstractProperty(proxy, "Value", s, newSlot: true);
                AddTarget(module);
            },
            [("ProtectedNameProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Value|protected Name|IName.Name"] = () =>
                {
                    var proxy = CreateProxy(c, "ProtectedNameProxy", "Target");
                    var type = c.GetType($"{ContractsNamespace}.ProtectedNameProxy")!;
                    return type.GetProperty("Value")!.GetValue(proxy) + "|" +
                           type.GetProperty("Name", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.GetValue(proxy) + "|" +
                           c.GetType($"{ContractsNamespace}.IName")!.GetProperty("Name")!.GetValue(proxy);
                },
            });

    // M09: struct target with a generic virtual... (not possible) -> instead: target with a public virtual string ToString<T>()
    // and an interface proxy with its own [DuckIgnore]: IDuckType.ToString through the stub (metadata-only).
    [Fact]
    public void GenericVirtualToStringShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM09",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var proxy = AddInterface(module, "INameOnly", ignore);
                AddAbstractProperty(proxy, "Name", module.CorLibTypes.String);
                var target = AddClass(module, "GenericToStringTarget", module.CorLibTypes.Object.TypeDefOrRef, out _);
                AddConstantProperty(module, target, "Name", "n", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName);
                var toString = new MethodDefUser("ToString", MethodSig.CreateInstanceGeneric(1, module.CorLibTypes.String), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot) { Body = new CilBody() };
                toString.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "T"));
                toString.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("generic"));
                toString.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                target.Methods.Add(toString);
            },
            [("INameOnly", "GenericToStringTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Name|IDuckType.ToString|object.ToString"] = () =>
                {
                    var proxy = CreateProxy(c, "INameOnly", "GenericToStringTarget")!;
                    string Describe(Func<string?> call)
                    {
                        try
                        {
                            var text = call();
                            return text is null ? "null" : text == proxy.GetType().ToString() ? "proxy-name" : text;
                        }
                        catch (Exception ex)
                        {
                            return "throws:" + ex.GetType().Name + ":" + ex.Message;
                        }
                    }

                    return c.GetType($"{ContractsNamespace}.INameOnly")!.GetProperty("Name")!.GetValue(proxy) + "|" + Describe(() => ((IDuckType)proxy).ToString()) + "|" + Describe(() => proxy.ToString());
                },
            });

    // M10: metadata-only binding failure: interface proxy property missing on the target (exception type and message).
    [Fact]
    public void MissingPropertyShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM10",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var proxy = AddInterface(module, "IMissingProxy", ignore);
                AddAbstractProperty(proxy, "Missing", module.CorLibTypes.String);
                AddTarget(module);
            },
            [("IMissingProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["create"] = () => CreateProxy(c, "IMissingProxy", "Target") is null ? "null" : "created",
            });

    // M11: metadata-only binding failure: interface proxy method missing on the target.
    [Fact]
    public void MissingMethodShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM11",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var proxy = AddInterface(module, "IMissingMethodProxy", ignore);
                proxy.Methods.Add(new MethodDefUser("Missing", MethodSig.CreateInstance(module.CorLibTypes.String), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot));
                AddTarget(module);
            },
            [("IMissingMethodProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["create"] = () => CreateProxy(c, "IMissingMethodProxy", "Target") is null ? "null" : "created",
            });

    // M12: metadata-only binding failure: interface proxy property with a setter over a read-only target property.
    [Fact]
    public void ReadOnlyTargetPropertyShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM12",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var proxy = AddInterface(module, "ISettableNameProxy", ignore);
                var s = module.CorLibTypes.String;
                var getter = new MethodDefUser("get_Name", MethodSig.CreateInstance(s), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                var setter = new MethodDefUser("set_Name", MethodSig.CreateInstance(module.CorLibTypes.Void, s), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                proxy.Methods.Add(getter);
                proxy.Methods.Add(setter);
                proxy.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(s)) { GetMethod = getter, SetMethod = setter });
                AddTarget(module);
            },
            [("ISettableNameProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["create"] = () => CreateProxy(c, "ISettableNameProxy", "Target") is null ? "null" : "created",
            });

    // M13: metadata-only binding failure: class proxy abstract property missing on the target.
    [Fact]
    public void ClassProxyMissingPropertyShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM13",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var proxy = AddClass(module, "MissingClassProxy", module.CorLibTypes.Object.TypeDefOrRef, out _, isAbstract: true);
                proxy.CustomAttributes.Add(new CustomAttribute(ignore));
                AddAbstractProperty(proxy, "Missing", module.CorLibTypes.String);
                AddTarget(module);
            },
            [("MissingClassProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["create"] = () => CreateProxy(c, "MissingClassProxy", "Target") is null ? "null" : "created",
            });

    // M14: class proxy implementing a GENERIC interface explicitly (IGenValue<string>), with a protected abstract property of the
    // interface property's name and signature: the metadata rules compare the closed signature, not the open one.
    [Fact]
    public void GenericInterfaceAccessorOverridesProtectedAbstractShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM14",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var s = module.CorLibTypes.String;
                var genericValue = AddInterface(module, "IGenValue`1", ignoreCtor: null);
                genericValue.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "T"));
                AddAbstractProperty(genericValue, "Value", new GenericVar(0, genericValue));
                var closedValue = new TypeSpecUser(new GenericInstSig(new ClassSig(genericValue), s));
                var proxy = AddClass(module, "GenericInterfaceClassProxy", module.CorLibTypes.Object.TypeDefOrRef, out _, isAbstract: true);
                proxy.CustomAttributes.Add(new CustomAttribute(ignore));
                proxy.Interfaces.Add(new InterfaceImplUser(closedValue));
                var protectedGetter = new MethodDefUser("get_Value", MethodSig.CreateInstance(s), MethodAttributes.Family | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                proxy.Methods.Add(protectedGetter);
                proxy.Properties.Add(new PropertyDefUser("Value", PropertySig.CreateInstance(s)) { GetMethod = protectedGetter });
                var explicitGetter = new MethodDefUser($"{ContractsNamespace}.IGenValue<System.String>.get_Value", MethodSig.CreateInstance(s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName) { Body = new CilBody() };
                explicitGetter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("explicit:"));
                explicitGetter.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                explicitGetter.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(protectedGetter));
                explicitGetter.Body.Instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(module, "Concat", MethodSig.CreateStatic(s, s, s), module.CorLibTypes.String.TypeDefOrRef)));
                explicitGetter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                explicitGetter.Overrides.Add(new MethodOverride(explicitGetter, new MemberRefUser(module, "get_Value", MethodSig.CreateInstance(new GenericVar(0)), closedValue)));
                proxy.Methods.Add(explicitGetter);
                proxy.Properties.Add(new PropertyDefUser($"{ContractsNamespace}.IGenValue<System.String>.Value", PropertySig.CreateInstance(s)) { GetMethod = explicitGetter });
                AddTarget(module);
            },
            [("GenericInterfaceClassProxy", "Target")],
            c => new Dictionary<string, Func<object?>>
            {
                ["IGenValue<string>.Value"] = () =>
                {
                    var proxy = CreateProxy(c, "GenericInterfaceClassProxy", "Target");
                    return c.GetType($"{ContractsNamespace}.IGenValue`1")!.MakeGenericType(typeof(string)).GetProperty("Value")!.GetValue(proxy);
                },
            });

    // ===================================================================================================================

    // M15: overloads the default binder of Type.GetMethod selects (dynamic duck typing asks it before its own candidate scan): the
    // exact parameter types, the parameter count without optional parameters, boxing, the most specific, a wider primitive (which
    // dynamic duck typing then fails to convert).
    [Theory]
    [InlineData("exact-string-over-object")]
    [InlineData("object-over-string")]
    [InlineData("exact-count-over-optional")]
    [InlineData("boxing-to-object")]
    [InlineData("base-object-over-derived-int")]
    [InlineData("wider-primitive")]
    public void OverloadsTheDefaultBinderSelectsShouldBehaveLikeDynamicMode(string scenario)
    {
        var (proxyName, parameterType, argument, targetName) = scenario switch
        {
            "exact-string-over-object" => ("IEchoString", "string", (object)"x", "OverloadTarget"),
            "object-over-string" => ("IEchoObject", "object", "x", "OverloadTarget"),
            "exact-count-over-optional" => ("IEchoInt", "int", 4, "OptionalTarget"),
            "boxing-to-object" => ("IEchoInt", "int", 4, "ObjectTarget"),
            "base-object-over-derived-int" => ("IEchoObject", "object", 4, "DerivedIntTarget"),
            _ => ("IEchoInt", "int", 4, "LongTarget"),
        };
        RunScenario(
            "MetadataOnlyM15",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var types = new Dictionary<string, TypeSig> { ["string"] = module.CorLibTypes.String, ["object"] = module.CorLibTypes.Object, ["int"] = module.CorLibTypes.Int32 };
                foreach (var proxy in new[] { ("IEchoString", "string"), ("IEchoObject", "object"), ("IEchoInt", "int") })
                {
                    AddInterface(module, proxy.Item1, ignore).Methods.Add(new MethodDefUser("Echo", MethodSig.CreateInstance(module.CorLibTypes.String, types[proxy.Item2]), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot));
                }

                var overloads = AddClass(module, "OverloadTarget", module.CorLibTypes.Object.TypeDefOrRef, out _);
                AddConstantMethod(module, overloads, "Echo", "object", module.CorLibTypes.Object);
                AddConstantMethod(module, overloads, "Echo", "string", module.CorLibTypes.String);

                var optional = AddClass(module, "OptionalTarget", module.CorLibTypes.Object.TypeDefOrRef, out _);
                AddConstantMethod(module, optional, "Echo", "exact", module.CorLibTypes.Int32);
                var withOptional = AddConstantMethod(module, optional, "Echo", "optional", module.CorLibTypes.Int32, module.CorLibTypes.Int32);
                withOptional.ParamDefs.Add(new ParamDefUser("extra", 2, ParamAttributes.Optional | ParamAttributes.HasDefault) { Constant = new ConstantUser(0) });

                AddConstantMethod(module, AddClass(module, "ObjectTarget", module.CorLibTypes.Object.TypeDefOrRef, out _), "Echo", "object", module.CorLibTypes.Object);
                AddConstantMethod(module, AddClass(module, "LongTarget", module.CorLibTypes.Object.TypeDefOrRef, out _), "Echo", "long", module.CorLibTypes.Int64);

                var baseTarget = AddClass(module, "BaseObjectTarget", module.CorLibTypes.Object.TypeDefOrRef, out var baseCtor);
                AddConstantMethod(module, baseTarget, "Echo", "base-object", module.CorLibTypes.Object);
                AddConstantMethod(module, AddClass(module, "DerivedIntTarget", baseTarget, out _, baseCtor: baseCtor), "Echo", "derived-int", module.CorLibTypes.Int32);
            },
            [(proxyName, targetName)],
            c => new Dictionary<string, Func<object?>>
            {
                [$"Echo({parameterType})"] = () => c.GetType($"{ContractsNamespace}.{proxyName}")!.GetMethod("Echo")!.Invoke(CreateProxy(c, proxyName, targetName), [argument]),
            });
    }

    // M16: a property whose value dynamic duck typing can't convert (it selects the property by name): its failure.
    [Theory]
    [InlineData("int-to-long")]
    [InlineData("int-to-nullable")]
    [InlineData("string-to-int")]
    public void PropertyConversionFailuresShouldBehaveLikeDynamicMode(string scenario)
        => RunScenario(
            "MetadataOnlyM16",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var nullableInt = new GenericInstSig(new ValueTypeSig(module.CorLibTypes.GetTypeRef("System", "Nullable`1")), module.CorLibTypes.Int32);
                AddAbstractProperty(AddInterface(module, "IValueProxy", ignore), "Value", scenario switch { "int-to-long" => module.CorLibTypes.Int64, "int-to-nullable" => nullableInt, _ => module.CorLibTypes.Int32 });
                var target = AddClass(module, "ValueTarget", module.CorLibTypes.Object.TypeDefOrRef, out _);
                var valueType = scenario == "string-to-int" ? module.CorLibTypes.String : module.CorLibTypes.Int32;
                var getter = new MethodDefUser("get_Value", MethodSig.CreateInstance(valueType), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName) { Body = new CilBody() };
                getter.Body.Instructions.Add(scenario == "string-to-int" ? OpCodes.Ldstr.ToInstruction("v") : OpCodes.Ldc_I4_3.ToInstruction());
                getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                target.Methods.Add(getter);
                target.Properties.Add(new PropertyDefUser("Value", PropertySig.CreateInstance(valueType)) { GetMethod = getter });
            },
            [("IValueProxy", "ValueTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Value"] = () => ReadProperty(c, "IValueProxy", "ValueTarget", "IValueProxy", "Value"),
            });

    // M17: interface proxy method whose parameter has a custom modifier inside it (an array of int modreq(IsVolatile)): the
    // methods dynamic duck typing defines have no custom modifiers at any depth.
    [Fact]
    public void InterfaceNestedCustomModifierShouldBehaveLikeDynamicMode()
        => RunScenario(
            "MetadataOnlyM17",
            module =>
            {
                var ignore = AddDuckIgnoreAttribute(module);
                var isVolatile = new TypeRefUser(module, "System.Runtime.CompilerServices", "IsVolatile", module.CorLibTypes.AssemblyRef);
                var proxy = AddInterface(module, "IVolatileEcho", ignore);
                proxy.Methods.Add(new MethodDefUser("Echo", MethodSig.CreateInstance(module.CorLibTypes.String, new SZArraySig(new CModReqdSig(isVolatile, module.CorLibTypes.Int32))), MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot));
                AddConstantMethod(module, AddClass(module, "ArrayTarget", module.CorLibTypes.Object.TypeDefOrRef, out _), "Echo", "array", new SZArraySig(module.CorLibTypes.Int32));
            },
            [("IVolatileEcho", "ArrayTarget")],
            c => new Dictionary<string, Func<object?>>
            {
                ["Echo([1])"] = () => c.GetType($"{ContractsNamespace}.IVolatileEcho")!.GetMethod("Echo")!.Invoke(CreateProxy(c, "IVolatileEcho", "ArrayTarget"), [new[] { 1 }]),
            });

    private static void RunScenario(
        string assemblyName,
        Action<ModuleDef> build,
        (string Proxy, string Target)[] mappings,
        Func<Assembly, Dictionary<string, Func<object?>>> exercises)
    {
        // A unique assembly name: dynamic duck typing loads the contracts in the default context, once per name.
        assemblyName += Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "ducktype-aot-metadata-only", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var loadContext = new AssemblyLoadContext("MetadataOnlyParity", isCollectible: true);
        try
        {
            var module = CreateModule(assemblyName);
            build(module);
            var contractsPath = Path.Combine(directory, assemblyName + ".dll");
            module.Write(contractsPath);

            // Dynamic duck typing doesn't read the contracts' own duck attributes: its outcome on the same types is the reference.
            DuckType.ResetRuntimeModeForTests();
            var expected = exercises(Assembly.LoadFrom(contractsPath)).ToDictionary(exercise => exercise.Key, exercise => Normalize(Capture(exercise.Value)));

            var mapPath = Path.Combine(directory, "map.json");
            File.WriteAllText(mapPath, JsonConvert.SerializeObject(new
            {
                mappings = mappings.Select(mapping => new
                {
                    mode = "forward",
                    proxyType = $"{ContractsNamespace}.{mapping.Proxy}",
                    proxyAssembly = assemblyName,
                    targetType = $"{ContractsNamespace}.{mapping.Target}",
                    targetAssembly = assemblyName
                })
            }));
            var outputPath = Path.Combine(directory, "Registry.dll");
            DuckTypeAotGenerateProcessor.Process(new DuckTypeAotGenerateOptions(
                proxyAssemblies: [contractsPath, typeof(DuckTypeAotMetadataOnlyParityTests).Assembly.Location],
                targetAssemblies: [contractsPath, typeof(object).Assembly.Location],
                targetFolders: [],
                targetFilters: ["*.dll"],
                mapFile: mapPath,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Registry",
                trimmerDescriptorPath: outputPath + ".linker.xml",
                propsPath: outputPath + ".props")).Should().Be(0);
            JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(outputPath + ".compat.json"))!
                       .Mappings.Should().OnlyContain(mapping => mapping.CheckedAgainstMetadataOnly, "the contracts declare their own duck attributes");

            var contracts = loadContext.LoadFromAssemblyPath(contractsPath);
#if NETCOREAPP2_1
            var registry = Assembly.Load(File.ReadAllBytes(outputPath));
#else
            using var registryStream = File.OpenRead(outputPath);
            var registry = loadContext.LoadFromStream(registryStream);
#endif
            DuckType.ResetRuntimeModeForTests();
            registry.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap")!
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);

            exercises(contracts).ToDictionary(exercise => exercise.Key, exercise => Normalize(Capture(exercise.Value)))
                                .Should().Equal(expected, "AOT duck typing should behave like dynamic duck typing");
        }
        finally
        {
            DuckType.ResetRuntimeModeForTests();
            loadContext.Unload();
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    // Compares outcomes without the inner exception detail dynamic duck typing adds (AOT replays the outer DuckTypeException).
    private static string Normalize(string outcome)
    {
        var inner = outcome.IndexOf(" [inner ", StringComparison.Ordinal);
        return inner >= 0 ? outcome.Substring(0, inner) : outcome;
    }

    private static object? CreateProxy(Assembly contracts, string proxyTypeName, string targetTypeName)
    {
        var target = Activator.CreateInstance(contracts.GetType($"{ContractsNamespace}.{targetTypeName}")!)!;
        return DuckType.Create(contracts.GetType($"{ContractsNamespace}.{proxyTypeName}")!, target);
    }

    private static object? ReadProperty(Assembly contracts, string proxyTypeName, string targetTypeName, string declaringTypeName, string propertyName)
    {
        var proxy = CreateProxy(contracts, proxyTypeName, targetTypeName);
        return contracts.GetType($"{ContractsNamespace}.{declaringTypeName}")!.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.GetValue(proxy);
    }

    private static string Capture(Func<object?> exercise)
    {
        try
        {
            return Convert.ToString(exercise(), CultureInfo.InvariantCulture) ?? "null";
        }
        catch (Exception ex)
        {
            var types = new List<string>();
            for (Exception? current = ex; current is not null; current = current.InnerException)
            {
                types.Add(current.GetType().Name);
                if (current is DuckTypeException)
                {
                    return "throws:" + string.Join(">", types) + ":" + current.Message + (current.InnerException is { } inner ? " [inner " + inner.GetType().Name + ": " + inner.Message + "]" : string.Empty);
                }
            }

            return "throws:" + string.Join(">", types) + ":" + ex.GetBaseException().Message;
        }
    }

    private static ModuleDefUser CreateModule(string assemblyName)
    {
        var module = new ModuleDefUser(assemblyName + ".dll", Guid.NewGuid(), new AssemblyRefUser(typeof(object).Assembly.GetName())) { Kind = ModuleKind.Dll };
        new AssemblyDefUser(assemblyName, new Version(1, 0, 0, 0)).Modules.Add(module);
        return module;
    }

    private static MethodDef AddDuckIgnoreAttribute(ModuleDef module)
    {
        var attributeBase = module.CorLibTypes.GetTypeRef("System", "Attribute");
        var attribute = new TypeDefUser("Datadog.Trace.DuckTyping", "DuckIgnoreAttribute", attributeBase)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit
        };
        module.Types.Add(attribute);
        return AddDefaultConstructor(module, attribute, new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), attributeBase));
    }

    private static TypeDef AddInterface(ModuleDef module, string name, MethodDef? ignoreCtor)
    {
        var type = new TypeDefUser(ContractsNamespace, name)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract
        };
        if (ignoreCtor is not null)
        {
            type.CustomAttributes.Add(new CustomAttribute(ignoreCtor));
        }

        module.Types.Add(type);
        return type;
    }

    private static TypeDef AddClass(ModuleDef module, string name, ITypeDefOrRef baseType, out MethodDef constructor, bool isAbstract = false, MethodDef? baseCtor = null)
    {
        var type = new TypeDefUser(ContractsNamespace, name, baseType)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit | (isAbstract ? TypeAttributes.Abstract : 0)
        };
        module.Types.Add(type);
        constructor = AddDefaultConstructor(module, type, baseCtor is not null ? baseCtor : new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), baseType));
        return type;
    }

    private static MethodDef AddDefaultConstructor(ModuleDef module, TypeDef type, IMethod baseConstructor)
    {
        var constructor = new MethodDefUser(
            ".ctor",
            MethodSig.CreateInstance(module.CorLibTypes.Void),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
        {
            Body = new CilBody()
        };
        constructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        constructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseConstructor));
        constructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(constructor);
        return constructor;
    }

    private static PropertyDef AddAbstractProperty(TypeDef type, string name, TypeSig propertyType, params TypeSig[] indexTypes)
        => AddAbstractProperty(type, name, propertyType, newSlot: true, indexTypes);

    private static PropertyDef AddAbstractProperty(TypeDef type, string name, TypeSig propertyType, bool newSlot, params TypeSig[] indexTypes)
    {
        var getter = new MethodDefUser(
            "get_" + name,
            MethodSig.CreateInstance(propertyType, indexTypes),
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | (newSlot ? MethodAttributes.NewSlot : 0) | MethodAttributes.SpecialName);
        type.Methods.Add(getter);
        var property = new PropertyDefUser(name, PropertySig.CreateInstance(propertyType, indexTypes)) { GetMethod = getter };
        type.Properties.Add(property);
        return property;
    }

    private static void AddConstantProperty(ModuleDef module, TypeDef type, string name, string value, MethodAttributes attributes)
    {
        var getter = new MethodDefUser(
            "get_" + name,
            MethodSig.CreateInstance(module.CorLibTypes.String),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            attributes)
        {
            Body = new CilBody()
        };
        getter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(value));
        getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(getter);
        type.Properties.Add(new PropertyDefUser(name, PropertySig.CreateInstance(module.CorLibTypes.String)) { GetMethod = getter });
    }

    private static MethodDef AddConstantMethod(ModuleDef module, TypeDef type, string name, string value, params TypeSig[] parameterTypes)
    {
        var method = new MethodDefUser(name, MethodSig.CreateInstance(module.CorLibTypes.String, parameterTypes), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig)
        {
            Body = new CilBody()
        };
        method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(value));
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(method);
        return method;
    }

    private static void AddTarget(ModuleDef module)
    {
        var target = AddClass(module, "Target", module.CorLibTypes.Object.TypeDefOrRef, out _);
        AddConstantProperty(module, target, "Name", "target-name", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName);
        AddConstantProperty(module, target, "Value", "v", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName);
    }

    // public class <name> { string _name; public string Name { get => _name ?? "target-initial"; set => _name = value; } public string Value => "v"; }
    private static void AddSettableTarget(ModuleDef module, string name)
    {
        var s = module.CorLibTypes.String;
        var target = AddClass(module, name, module.CorLibTypes.Object.TypeDefOrRef, out _);
        var field = new FieldDefUser("_name", new FieldSig(s), FieldAttributes.Private);
        target.Fields.Add(field);
        var getter = new MethodDefUser("get_Name", MethodSig.CreateInstance(s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName) { Body = new CilBody() };
        var hasValue = OpCodes.Ret.ToInstruction();
        getter.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        getter.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));
        getter.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
        getter.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValue));
        getter.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
        getter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("target-initial"));
        getter.Body.Instructions.Add(hasValue);
        target.Methods.Add(getter);
        var setter = new MethodDefUser("set_Name", MethodSig.CreateInstance(module.CorLibTypes.Void, s), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName) { Body = new CilBody() };
        setter.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        setter.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
        setter.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(field));
        setter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        target.Methods.Add(setter);
        target.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(s)) { GetMethod = getter, SetMethod = setter });
        AddConstantProperty(module, target, "Value", "v", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName);
    }
}
