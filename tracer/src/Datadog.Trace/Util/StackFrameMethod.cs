// <copyright file="StackFrameMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util;

/// <summary>
/// The method of a stack frame: from its <see cref="MethodBase"/>, or in NativeAOT, where the methods without reflection
/// metadata have none, from the stack trace data the compiler keeps (the names, like in the stack traces).
/// </summary>
internal readonly struct StackFrameMethod
{
#if NETCOREAPP3_0_OR_GREATER
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StackFrameMethod));

    // .NET 9+: DiagnosticMethodInfo.Create(StackFrame) has the names in NativeAOT too. Datadog.Trace targets net6.0: by reflection.
    private static readonly MethodInfo? DiagnosticMethodInfoCreate;
    private static readonly PropertyInfo? DiagnosticMethodInfoName;
    private static readonly PropertyInfo? DiagnosticMethodInfoDeclaringTypeName;
    private static readonly PropertyInfo? DiagnosticMethodInfoDeclaringAssemblyName;

    static StackFrameMethod()
    {
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled)
        {
            return;
        }

        try
        {
            var type = Type.GetType("System.Diagnostics.DiagnosticMethodInfo, System.Private.CoreLib", throwOnError: false);
            DiagnosticMethodInfoCreate = type?.GetMethod("Create", [typeof(StackFrame)]);
            DiagnosticMethodInfoName = type?.GetProperty("Name");
            DiagnosticMethodInfoDeclaringTypeName = type?.GetProperty("DeclaringTypeName");
            DiagnosticMethodInfoDeclaringAssemblyName = type?.GetProperty("DeclaringAssemblyName");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DiagnosticMethodInfo isn't available");
        }
    }
#endif

    private StackFrameMethod(MethodBase? method, string? assemblyName, string? @namespace, string? typeName, string? typeFullName, string? name)
    {
        Method = method;
        AssemblyName = assemblyName;
        Namespace = @namespace;
        TypeName = typeName;
        TypeFullName = typeFullName;
        Name = name;
    }

    /// <summary>Gets the reflection method, when the frame has one.</summary>
    public MethodBase? Method { get; }

    /// <summary>Gets the simple name of the assembly of the method, when it is known (not in .NET 8 NativeAOT).</summary>
    public string? AssemblyName { get; }

    /// <summary>Gets the namespace of the declaring type (<see cref="Type.Namespace"/>).</summary>
    public string? Namespace { get; }

    /// <summary>Gets the name of the declaring type (<see cref="MemberInfo.Name"/>).</summary>
    public string? TypeName { get; }

    /// <summary>Gets the full name of the declaring type (<see cref="Type.FullName"/>, without generic arguments without reflection).</summary>
    public string? TypeFullName { get; }

    /// <summary>Gets the name of the method.</summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the name to filter the frame by assembly: the assembly name, or when it isn't known, the full name of the type,
    /// whose namespace usually starts with it.
    /// </summary>
    public string? AssemblyNameForFilters => AssemblyName ?? TypeFullName;

    /// <summary>
    /// Gets the method of a frame. Only NativeAOT frames without a <see cref="MethodBase"/> use the stack trace data: under
    /// JIT, they are skipped as before.
    /// </summary>
    /// <returns>true when the frame has a method with a declaring type.</returns>
    public static bool TryGet(StackFrame? frame, out StackFrameMethod method)
    {
        method = default;
        if (frame is null)
        {
            return false;
        }

        if (frame.GetMethod() is { } methodBase)
        {
            var type = methodBase.DeclaringType;
            method = new StackFrameMethod(methodBase, type?.Assembly.GetName().Name, type?.Namespace, type?.Name, type?.FullName, methodBase.Name);
            return type is not null;
        }

#if NETCOREAPP3_0_OR_GREATER
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled)
        {
            return false;
        }

        try
        {
            if (DiagnosticMethodInfoCreate is not null)
            {
                return DiagnosticMethodInfoCreate.Invoke(null, [frame]) is { } info &&
                       DiagnosticMethodInfoDeclaringTypeName?.GetValue(info) is string declaringTypeName &&
                       TryCreate(declaringTypeName, DiagnosticMethodInfoName?.GetValue(info) as string, DiagnosticMethodInfoDeclaringAssemblyName?.GetValue(info) as string, out method);
            }

            return TryParse(frame.ToString(), out method);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "The method of a stack frame couldn't be read from the stack trace data");
            return false;
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// The method of a frame from its <c>DiagnosticMethodInfo</c> names: a declaring type name like
    /// <see cref="Type.FullName"/> (<c>Namespace.Outer+Nested`1</c>) and an assembly display name.
    /// </summary>
    internal static bool TryCreate(string declaringTypeName, string? name, string? assemblyName, out StackFrameMethod method)
    {
        method = default;
        if (StringUtil.IsNullOrEmpty(declaringTypeName))
        {
            return false;
        }

        // Nested types have the namespace of the outermost one.
        var outermost = declaringTypeName.IndexOf('+') is var plus and > 0 ? declaringTypeName.Substring(0, plus) : declaringTypeName;
        var namespaceEnd = outermost.LastIndexOf('.');
        var typeName = declaringTypeName.Substring(Math.Max(declaringTypeName.LastIndexOf('+'), declaringTypeName.LastIndexOf('.')) + 1);
        var comma = assemblyName?.IndexOf(',') ?? -1;
        var assembly = comma > 0 ? assemblyName!.Substring(0, comma) : assemblyName;
        method = new StackFrameMethod(null, assembly, namespaceEnd > 0 ? outermost.Substring(0, namespaceEnd) : null, typeName, declaringTypeName, name);
        return true;
    }

    /// <summary>
    /// The method of a .NET 8 NativeAOT frame from its text, <c>Namespace.Type.Method(Parameters) + 0x1b at offset 27 in ...</c>,
    /// which names nested types with dots (their outer types are taken as namespaces) and generic arguments in brackets
    /// (<c>List`1[System.Int32].Add</c>, <c>Start[TStateMachine]</c>). Frames without names (<c>Module!&lt;BaseAddress&gt;+0x...</c>)
    /// have none.
    /// </summary>
    internal static bool TryParse(string? text, out StackFrameMethod method)
    {
        method = default;
        var parameters = text?.IndexOf('(') ?? -1;
        if (parameters <= 0)
        {
            return false;
        }

        var qualifiedName = text!.Substring(0, parameters).Trim();
        var methodStart = LastDotOutsideBrackets(qualifiedName);
        if (methodStart <= 0)
        {
            return false;
        }

        var typeFullName = qualifiedName.Substring(0, methodStart);
        var typeStart = LastDotOutsideBrackets(typeFullName);
        method = new StackFrameMethod(
            null,
            assemblyName: null,
            typeStart > 0 ? typeFullName.Substring(0, typeStart) : null,
            WithoutGenericArguments(typeFullName.Substring(typeStart + 1)),
            typeFullName,
            WithoutGenericArguments(qualifiedName.Substring(methodStart + 1)));
        return true;
    }

    // Compiler-generated names (<Main>b__0_1) and generic arguments ([System.Int32]) have dots.
    private static int LastDotOutsideBrackets(string name)
    {
        var depth = 0;
        for (var i = name.Length - 1; i >= 0; i--)
        {
            switch (name[i])
            {
                case '>' or ']':
                    depth++;
                    break;
                case '<' or '[':
                    depth--;
                    break;
                case '.' when depth == 0:
                    return i;
            }
        }

        return -1;
    }

    // Like MemberInfo.Name: List`1, not List`1[System.Int32].
    private static string WithoutGenericArguments(string name)
        => name.EndsWith("]", StringComparison.Ordinal) && name.IndexOf('[') is var start and > 0 ? name.Substring(0, start) : name;
}
