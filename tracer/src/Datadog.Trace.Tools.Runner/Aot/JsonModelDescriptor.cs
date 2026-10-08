// <copyright file="JsonModelDescriptor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Datadog.Trace (de)serializes its models (remote configuration, sampling rules, IAST vulnerabilities, telemetry…) with
/// its vendored Newtonsoft.Json, which finds their properties by reflection: ILC only keeps that metadata for what it's
/// told about. This writes an ILLink descriptor that preserves the models: the types Newtonsoft attributes decorate, the
/// type arguments of the Newtonsoft calls, the static types of the values Datadog.Trace serializes, and the types of
/// their fields and properties. Only what Newtonsoft uses is preserved (fields, properties, constructors, serialization
/// callbacks and ShouldSerialize methods), not the rest of their code. The framework collections Newtonsoft creates for
/// them by reflection (List, Dictionary, HashSet and arrays of the models) go to a runtime directives file (rd.xml):
/// a descriptor can't name closed generic types.
/// </summary>
internal static class JsonModelDescriptor
{
    private const string NewtonsoftNamespace = "Datadog.Trace.Vendors.Newtonsoft.Json";
    private const string JsonHelperType = "Datadog.Trace.Util.Json.JsonHelper";

    private static readonly HashSet<string> SerializeMethods = new(StringComparer.Ordinal) { "SerializeObject", "Serialize", "FromObject" };

    /// <returns>The number of types preserved.</returns>
    public static int Write(ModuleDef datadogTrace, string descriptorPath, string runtimeDirectivesPath)
    {
        var (types, instantiations) = Collect(datadogTrace);
        var assembly = new XElement("assembly", new XAttribute("fullname", datadogTrace.Assembly.Name.String));
        foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var element = new XElement("type", new XAttribute("fullname", type.FullName));
            foreach (var field in type.Fields)
            {
                element.Add(new XElement("field", new XAttribute("name", field.Name.String)));
            }

            foreach (var property in type.Properties)
            {
                element.Add(new XElement("property", new XAttribute("name", property.Name.String)));
            }

            foreach (var name in type.Methods.Where(IsUsedByNewtonsoft).Select(m => m.Name.String).Distinct(StringComparer.Ordinal))
            {
                element.Add(new XElement("method", new XAttribute("name", name)));
            }

            assembly.Add(element);
        }

        new XDocument(new XElement("linker", assembly)).Save(descriptorPath);

        XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
        var directivesAssembly = new XElement(directives + "Assembly", new XAttribute("Name", datadogTrace.Assembly.Name.String));
        foreach (var instantiation in instantiations)
        {
            // Newtonsoft creates the collections with their default constructor (then uses their non-generic interfaces),
            // and the arrays from their type.
            directivesAssembly.Add(
                instantiation.EndsWith("[]", StringComparison.Ordinal) || instantiation.Contains("[], ", StringComparison.Ordinal)
                    ? new XElement(directives + "Type", new XAttribute("Name", instantiation), new XAttribute("Dynamic", "Required All"))
                    : new XElement(directives + "Type", new XAttribute("Name", instantiation), new XElement(directives + "Method", new XAttribute("Name", ".ctor"), new XAttribute("Dynamic", "Required"))));
        }

        new XDocument(new XElement(directives + "Directives", new XElement(directives + "Application", directivesAssembly))).Save(runtimeDirectivesPath);
        return types.Count;
    }

    internal static (HashSet<TypeDef> Types, SortedSet<string> Instantiations) Collect(ModuleDef datadogTrace)
    {
        var types = new HashSet<TypeDef>();
        var instantiations = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Queue<TypeDef>();

        // Custom attribute type names (resolved in Datadog.Trace, then CoreLib), for the types they can name safely: the
        // name, and the assembly when it isn't CoreLib.
        (string Name, string? Assembly)? NameOf(TypeSig type)
            => type switch
            {
                CorLibTypeSig corLib => (corLib.ReflectionFullName, null),
                TypeDefOrRefSig { TypeDefOrRef: { } reference } when reference.ResolveTypeDef() is { } definition && definition.Module == datadogTrace
                    => (definition.ReflectionFullName, datadogTrace.Assembly.Name.String),
                GenericInstSig instance when CollectionOf(instance) is { } collection => (collection, null),
                _ => null,
            };

        static string Qualified((string Name, string? Assembly) type, string suffix = "")
            => type.Assembly is null ? type.Name + suffix : $"{type.Name}{suffix}, {type.Assembly}";

        // The collection Newtonsoft creates for a collection type, if it can be named.
        string? CollectionOf(GenericInstSig instance)
        {
            var definition = instance.GenericType.TypeDefOrRef.FullName switch
            {
                "System.Collections.Generic.List`1" or "System.Collections.Generic.IEnumerable`1" or "System.Collections.Generic.IList`1"
                    or "System.Collections.Generic.ICollection`1" or "System.Collections.Generic.IReadOnlyList`1"
                    or "System.Collections.Generic.IReadOnlyCollection`1" => "System.Collections.Generic.List`1",
                "System.Collections.Generic.Dictionary`2" or "System.Collections.Generic.IDictionary`2"
                    or "System.Collections.Generic.IReadOnlyDictionary`2" => "System.Collections.Generic.Dictionary`2",
                "System.Collections.Generic.HashSet`1" or "System.Collections.Generic.ISet`1" => "System.Collections.Generic.HashSet`1",
                _ => null,
            };
            var arguments = instance.GenericArguments.Select(NameOf).ToList();
            return definition is null || arguments.Any(a => a is null) ? null : $"{definition}[{string.Join(",", arguments.Select(a => $"[{Qualified(a!.Value)}]"))}]";
        }

        void Add(TypeSig? signature)
        {
            while (signature is not null)
            {
                switch (signature)
                {
                    case SZArraySig array:
                        if (NameOf(array.Next) is { } element)
                        {
                            // Newtonsoft fills a List<T> and copies it to a T[].
                            instantiations.Add($"System.Collections.Generic.List`1[[{Qualified(element)}]]");
                            instantiations.Add(Qualified(element, "[]"));
                        }

                        signature = array.Next;
                        break;
                    case GenericInstSig instance:
                        if (CollectionOf(instance) is { } collection)
                        {
                            instantiations.Add(collection);
                        }

                        Add(instance.GenericType);
                        foreach (var argument in instance.GenericArguments)
                        {
                            Add(argument);
                        }

                        return;
                    case TypeDefOrRefSig { TypeDefOrRef: { } reference }:
                        if (reference.ResolveTypeDef() is { } definition && definition.Module == datadogTrace && !IsVendored(definition) && types.Add(definition))
                        {
                            pending.Enqueue(definition);
                        }

                        return;
                    default:
                        signature = signature.Next;
                        break;
                }
            }
        }

        foreach (var type in datadogTrace.GetTypes().Where(t => !IsVendored(t)))
        {
            if (IsJsonDecorated(type.CustomAttributes) || type.Fields.Any(f => IsJsonDecorated(f.CustomAttributes)) || type.Properties.Any(p => IsJsonDecorated(p.CustomAttributes)))
            {
                Add(type.ToTypeSig());
            }

            foreach (var converter in ConverterTypes(type))
            {
                Add(converter);
            }

            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                var instructions = method.Body.Instructions;
                for (var i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].Operand is not IMethod called)
                    {
                        continue;
                    }

                    // Newtonsoft, and the generic JSON helpers of Datadog.Trace (PostAsJsonAsync<T>, WriteAsJson<T>…).
                    var isJsonApi = IsJsonApi(called);
                    if (called is MethodSpec specification && (isJsonApi || IsJsonHelper(specification, datadogTrace)))
                    {
                        foreach (var argument in specification.GenericInstMethodSig.GenericArguments)
                        {
                            Add(argument);
                        }
                    }

                    // The value serialized is the object parameter (JsonConvert.SerializeObject(value…), JsonSerializer.Serialize(writer, value)…).
                    if (isJsonApi && SerializeMethods.Contains(called.Name) && called.MethodSig is { } signature
                     && signature.Params.Select((type, index) => (type, index)).FirstOrDefault(p => p.type.ElementType == ElementType.Object) is { type: not null } value)
                    {
                        var hasThis = signature.HasThis ? 1 : 0;
                        Add(StaticTypeOfArgument(method, instructions, i, value.index + hasThis, signature.Params.Count + hasThis));
                    }
                }
            }
        }

        // Newtonsoft serializes the runtime type of a value: the implementations of interfaces and abstract types too.
        var implementations = datadogTrace.GetTypes()
                                          .Where(t => !IsVendored(t) && !t.IsAbstract)
                                          .SelectMany(t => t.Interfaces.Select(i => i.Interface).Append(t.BaseType).Where(b => b is not null).Select(b => (Base: b!.ResolveTypeDef(), Type: t)))
                                          .Where(p => p.Base is not null)
                                          .ToLookup(p => p.Base!, p => p.Type);

        // The members Newtonsoft (de)serializes, and the base types it reads them from.
        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (type.IsInterface || type.IsAbstract)
            {
                foreach (var implementation in implementations[type])
                {
                    Add(implementation.ToTypeSig());
                }
            }

            Add(type.BaseType?.ToTypeSig());
            foreach (var field in type.Fields.Where(f => !f.IsStatic))
            {
                Add(field.FieldType);
            }

            foreach (var property in type.Properties)
            {
                Add(property.PropertySig?.RetType);
            }

            foreach (var converter in ConverterTypes(type))
            {
                Add(converter);
            }
        }

        return (types, instantiations);
    }

    // Constructors, serialization callbacks ([OnDeserialized]…) and the ShouldSerialize{Member} convention.
    private static bool IsUsedByNewtonsoft(MethodDef method)
        => method.IsConstructor
        || method.Name.StartsWith("ShouldSerialize", StringComparison.Ordinal)
        || method.CustomAttributes.Any(a => a.TypeFullName.StartsWith("System.Runtime.Serialization.On", StringComparison.Ordinal));

    // Newtonsoft and the other vendored libraries use their own types statically.
    private static bool IsVendored(TypeDef type)
    {
        while (type.DeclaringType is { } declaringType)
        {
            type = declaringType;
        }

        return type.Namespace.StartsWith("Datadog.Trace.Vendors", StringComparison.Ordinal);
    }

    private static bool IsJsonApi(IMethod method)
        => method.DeclaringType?.FullName is { } name
        && (name.StartsWith(NewtonsoftNamespace, StringComparison.Ordinal) || name == JsonHelperType);

    private static bool IsJsonHelper(MethodSpec method, ModuleDef datadogTrace)
        => method.Name.Contains("Json") && method.Method.DeclaringType?.DefinitionAssembly?.Name == datadogTrace.Assembly.Name;

    private static bool IsJsonDecorated(CustomAttributeCollection attributes)
        => attributes.Any(a => a.TypeFullName.StartsWith(NewtonsoftNamespace, StringComparison.Ordinal));

    /// <summary>
    /// The converters Newtonsoft attributes name (JsonConverter, JsonProperty.ItemConverterType…): Newtonsoft creates them by
    /// reflection.
    /// </summary>
    private static IEnumerable<TypeSig> ConverterTypes(TypeDef type)
    {
        var attributes = type.CustomAttributes
                             .Concat(type.Fields.SelectMany(f => f.CustomAttributes))
                             .Concat(type.Properties.SelectMany(p => p.CustomAttributes))
                             .Where(a => a.TypeFullName.StartsWith(NewtonsoftNamespace, StringComparison.Ordinal));
        foreach (var attribute in attributes)
        {
            foreach (var value in attribute.ConstructorArguments.Select(a => a.Value).Concat(attribute.NamedArguments.Select(a => a.Argument.Value)))
            {
                if (value is TypeSig typeArgument)
                {
                    yield return typeArgument;
                }
            }
        }
    }

    /// <summary>
    /// The static type of an argument of the call at <paramref name="callIndex"/>, from the instruction that pushes it
    /// (straight-line code before the call; null when it can't be told).
    /// </summary>
    private static TypeSig? StaticTypeOfArgument(MethodDef method, IList<Instruction> instructions, int callIndex, int argument, int argumentCount)
    {
        // Depth of the argument from the top of the stack at the call.
        var depth = argumentCount - 1 - argument;
        for (var i = callIndex - 1; i >= 0; i--)
        {
            var instruction = instructions[i];
            if (instruction.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return or FlowControl.Throw)
            {
                return null;
            }

            instruction.CalculateStackUsage(method.HasReturnType, out var pushes, out var pops);
            if (depth < pushes)
            {
                return pushes == 1 ? PushedType(method, instruction) : null;
            }

            depth = depth - pushes + pops;
        }

        return null;
    }

    private static TypeSig? PushedType(MethodDef method, Instruction instruction)
    {
        switch (instruction.OpCode.Code)
        {
            case Code.Ldarg:
            case Code.Ldarg_S:
            case Code.Ldarg_0:
            case Code.Ldarg_1:
            case Code.Ldarg_2:
            case Code.Ldarg_3:
                return instruction.GetParameter(method.Parameters)?.Type;
            case Code.Ldloc:
            case Code.Ldloc_S:
            case Code.Ldloc_0:
            case Code.Ldloc_1:
            case Code.Ldloc_2:
            case Code.Ldloc_3:
                return instruction.GetLocal(method.Body.Variables)?.Type;
            case Code.Ldfld:
            case Code.Ldsfld:
                return (instruction.Operand as IField)?.FieldSig?.Type;
            case Code.Call:
            case Code.Callvirt:
                return (instruction.Operand as IMethod)?.MethodSig?.RetType;
            case Code.Newobj:
                return (instruction.Operand as IMethod)?.DeclaringType?.ToTypeSig();
            case Code.Box:
            case Code.Castclass:
            case Code.Isinst:
                return (instruction.Operand as ITypeDefOrRef)?.ToTypeSig();
            default:
                return null;
        }
    }
}
#endif
