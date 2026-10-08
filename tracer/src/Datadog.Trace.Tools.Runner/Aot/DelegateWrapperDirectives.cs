// <copyright file="DelegateWrapperDirectives.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// <c>DelegateInstrumentation</c> wraps the delegates of the libraries it instruments (Kafka handlers, the AWS Lambda
/// handler) in wrapper types it instantiates at runtime with <c>MakeGenericType</c>: the arguments of the delegate and the
/// callbacks struct. NativeAOT can only build those types from the canonical code of the wrapper for that callbacks struct,
/// which nothing else compiles. This writes a runtime directives file (rd.xml) with each wrapper instantiated over
/// <c>System.Object</c> (the canonical form of the reference type arguments) and each callbacks struct of Datadog.Trace its
/// constraints accept, with the members reflection uses (constructor, <c>Invoke</c>) and the generic methods of the
/// wrappers (async continuations) over <c>System.Object</c>.
/// </summary>
internal static class DelegateWrapperDirectives
{
    private const string DelegateInstrumentationType = "Datadog.Trace.Util.Delegates.DelegateInstrumentation";
    private const string CallbacksInterface = "Datadog.Trace.Util.Delegates.ICallbacks";
    private const string ObjectType = "System.Object, System.Private.CoreLib";

    /// <returns>The number of wrapper instantiations.</returns>
    public static int Write(ModuleDef datadogTrace, string runtimeDirectivesPath)
    {
        XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
        var assembly = new XElement(directives + "Assembly", new XAttribute("Name", datadogTrace.Assembly.Name.String));
        var instantiations = Collect(datadogTrace);
        foreach (var (wrapper, callbacks) in instantiations)
        {
            for (var type = wrapper; type is not null; type = type.BaseType?.ResolveTypeDef())
            {
                if (!type.HasGenericParameters || type.DeclaringType?.FullName != DelegateInstrumentationType)
                {
                    break;
                }

                // The wrapper with all its members (reflection creates it and its delegate), its bases with their generic
                // methods: by construction, the callbacks struct is the last generic parameter of all of them.
                var element = new XElement(directives + "Type", new XAttribute("Name", InstantiationName(type, callbacks)));
                if (type == wrapper)
                {
                    element.Add(new XAttribute("Dynamic", "Required All"));
                }

                foreach (var method in type.Methods.Where(m => m.GenericParameters.Count > 0))
                {
                    element.Add(new XElement(
                                    directives + "Method",
                                    new XAttribute("Name", method.Name.String),
                                    new XAttribute("Dynamic", "Required"),
                                    method.GenericParameters.Select(_ => new XElement(directives + "GenericArgument", new XAttribute("Name", ObjectType)))));
                }

                if (type == wrapper || element.HasElements)
                {
                    assembly.Add(element);
                }
            }
        }

        new XDocument(new XElement(directives + "Directives", new XElement(directives + "Application", assembly))).Save(runtimeDirectivesPath);
        return instantiations.Count;
    }

    /// <summary>
    /// Gets the wrapper types (the non-abstract generic types nested in DelegateInstrumentation) with each callbacks struct of
    /// Datadog.Trace that the constraints of their last generic parameter accept.
    /// </summary>
    internal static List<(TypeDef Wrapper, TypeDef Callbacks)> Collect(ModuleDef datadogTrace)
    {
        var instantiations = new List<(TypeDef Wrapper, TypeDef Callbacks)>();
        if (datadogTrace.Find(DelegateInstrumentationType, isReflectionName: false) is not { } owner)
        {
            return instantiations;
        }

        var callbacks = datadogTrace.GetTypes().Where(t => t.IsValueType && !t.HasGenericParameters && Implements(t, CallbacksInterface)).ToList();
        foreach (var wrapper in owner.NestedTypes.Where(t => !t.IsAbstract && t.HasGenericParameters))
        {
            // The interfaces of the callbacks parameter (`struct` is also a System.ValueType constraint).
            var constraints = wrapper.GenericParameters[wrapper.GenericParameters.Count - 1].GenericParamConstraints
                                     .Select(c => c.Constraint.FullName)
                                     .Where(c => c != "System.ValueType")
                                     .ToList();
            if (constraints.Count == 0)
            {
                continue;
            }

            instantiations.AddRange(callbacks.Where(c => constraints.All(i => Implements(c, i))).Select(c => (wrapper, c)));
        }

        return instantiations;
    }

    private static string InstantiationName(TypeDef type, TypeDef callbacks)
    {
        var arguments = Enumerable.Repeat($"[{ObjectType}]", type.GenericParameters.Count - 1)
                                  .Append($"[{callbacks.ReflectionFullName}, {callbacks.Module.Assembly.Name}]");
        return $"{type.ReflectionFullName}[{string.Join(",", arguments)}]";
    }

    private static bool Implements(TypeDef type, string interfaceName)
    {
        foreach (var implemented in type.Interfaces.Select(i => i.Interface))
        {
            if (implemented.FullName == interfaceName || (implemented.ResolveTypeDef() is { } definition && Implements(definition, interfaceName)))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
