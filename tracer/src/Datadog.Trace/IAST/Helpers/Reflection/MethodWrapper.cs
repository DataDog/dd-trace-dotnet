// <copyright file="MethodWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Helpers.Reflection;

/// <summary>
///     Method wrapper class
/// </summary>
public abstract class MethodWrapper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodWrapper));

    private bool _initialized;
    private string? _typeName;
    private MethodInfo? _method;
    private ConstructorInfo? _ctor;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MethodWrapper" /> class.
    /// </summary>
    /// <param name="methodSignature"> the method signature </param>
    /// <param name="assemblyName"> the assembly name (optional) </param>
    protected MethodWrapper(string methodSignature, string? assemblyName = null)
    {
        var typeIndex = methodSignature.IndexOf("::", StringComparison.Ordinal);
        if (typeIndex >= 0)
        {
            _typeName = methodSignature.Substring(0, typeIndex);
            methodSignature = methodSignature.Substring(typeIndex + 2);
        }

        var paramsIndex = methodSignature.IndexOf('(');
        Name = methodSignature.Substring(0, paramsIndex);
        ParamsSignature = methodSignature.Substring(paramsIndex);
        AssemblyName = assemblyName;
    }

    /// <summary>
    ///     Gets the name of the method
    /// </summary>
    public string? Name { get; }

    /// <summary>
    ///     Gets the params signature of the method
    /// </summary>
    public string? ParamsSignature { get; }

    /// <summary>
    ///     Gets the signature of the method
    /// </summary>
    public string MethodSignature => Name + ParamsSignature;

    /// <summary>
    ///     Gets the assembly name of the method
    /// </summary>
    public string? AssemblyName { get; }

    /// <summary>
    ///     Gets a value indicating whether the method is a constructor
    /// </summary>
    protected bool IsCtor => Name == ".ctor";

    /// <summary>
    ///     Gets the type name of the method
    /// </summary>
    /// <param name="obj"> the object </param>
    /// <returns> the method </returns>
    /// <exception cref="ArgumentNullException"> Argument null </exception>
    /// <exception cref="MissingMethodException"> Method not found </exception>
    protected MethodInfo ResolveMethod(object? obj = null)
    {
        if (!_initialized)
        {
            if (_typeName == null && obj == null) { throw new ArgumentNullException(nameof(obj)); }

            var t = _typeName != null ? GetType(_typeName) : obj?.GetType();
            if (t == null && obj != null) { t = obj.GetType(); }

            SetMethod(t);
        }

        if (_method == null)
        {
            Log.Warning("Method {0}::{1} not found on", _typeName, MethodSignature);
            throw new MissingMethodException(_typeName, MethodSignature);
        }

        return _method;
    }

    /// <summary>
    ///     Gets the constructor of the method
    /// </summary>
    /// <returns> the constructor </returns>
    /// <exception cref="ArgumentNullException"> Argument null </exception>
    /// <exception cref="MissingMethodException"> Method not found </exception>
    protected ConstructorInfo ResolveCtor()
    {
        if (!_initialized)
        {
            if (_typeName == null) { throw new ArgumentNullException(); }

            var t = GetType(_typeName);
            SetCtor(t);
        }

        if (_ctor == null)
        {
            Log.Warning("Method {0}::{1} not found on", _typeName, MethodSignature);
            throw new MissingMethodException(_typeName, MethodSignature);
        }

        return _ctor;
    }

    private Type? GetType(string? typeName)
    {
        if (typeName == null) { return null; }

        if (AssemblyName == null)
        {
            var t = Type.GetType(typeName);
            if (t != null) { return t; }

            var assemblyName = typeName;
            var pos = assemblyName.LastIndexOf('.');
            while (pos >= 0)
            {
                assemblyName = assemblyName.Substring(0, pos);
                var res = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName != null && a.FullName.StartsWith(assemblyName)).Select(a => a.GetType(typeName)).FirstOrDefault(type => type != null);
                if (res != null) { return res; }

                pos = assemblyName.LastIndexOf('.');
            }
        }
        else
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName != null && a.FullName.StartsWith(AssemblyName)).Select(a => a.GetType(typeName)).FirstOrDefault(type => type != null);
        }

        return null;
    }

    private void SetMethod(Type? t)
    {
        if (t != null)
        {
            if (_typeName == null) { _typeName = t.ToString(); }

            _method = t.GetMethods().FirstOrDefault(IsMethod);
            if (_method == null)
            {
                var all = t.GetMethods(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                _method = all.FirstOrDefault(IsMethod);
            }
        }

        _initialized = true;
    }

    private void SetCtor(Type? t)
    {
        if (t != null)
        {
            _typeName ??= t.ToString();

            _ctor = t.GetConstructors().FirstOrDefault(IsMethod);
            if (_ctor == null)
            {
                _ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(IsMethod);
            }
        }

        _initialized = true;
    }

    private bool IsMethod(MethodBase m)
    {
        if (m.Name != Name) { return false; }

        var parameters = m.GetParameters().Select(p => p.ParameterType.ToString()).ToArray();
        var signature = $"({string.Join(",", parameters)})";
        return signature == ParamsSignature;
    }
}
