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
/// Represents a wrapper for a method. Add more details here.
/// </summary>
public abstract class MethodWrapper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodWrapper));

    private bool initialized = false;
    private string? _typeName = null;
    private MethodInfo? method = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodWrapper"/> class.
    /// </summary>
    /// <param name="methodSignature"> the method signature </param>
    /// <param name="assemblyName"> the assembly name (optional) </param>
    protected MethodWrapper(string methodSignature, string? assemblyName = null)
    {
        int typeIndex = methodSignature.IndexOf("::");
        if (typeIndex >= 0)
        {
            this._typeName = methodSignature.Substring(0, typeIndex);
            methodSignature = methodSignature.Substring(typeIndex + 2);
        }

        int paramsIndex = methodSignature.IndexOf('(');
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
    ///    Gets the type name of the method
    /// </summary>
    /// <param name="obj"> the object </param>
    /// <returns> the method </returns>
    /// <exception cref="MissingMethodException"> Method not found </exception>
    protected MethodInfo ResolveMethod(object? obj = null)
    {
        if (!initialized)
        {
            if (_typeName == null && obj == null) { throw new ArgumentNullException("obj"); }
            var t = _typeName != null ? GetType(_typeName) : obj?.GetType();
            if (t == null && obj != null) { t = obj.GetType(); }
            SetMethod(t);
        }

        if (method == null)
        {
            Log.Warning("Method {0}::{1} not found on", _typeName, MethodSignature);
            throw new MissingMethodException(_typeName, MethodSignature);
        }

        return method;
    }

    private Type? GetType(string? typeName)
    {
        if (typeName == null) { return null; }

        if (AssemblyName == null)
        {
            var t = Type.GetType(typeName);
            if (t != null) { return t; }

            var assemblyName = typeName;
            var pos = assemblyName.LastIndexOf(".");
            while (pos >= 0)
            {
                assemblyName = assemblyName.Substring(0, pos);
                var res = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName != null && a.FullName.StartsWith(assemblyName)).Select(a => a.GetType(typeName)).FirstOrDefault(type => type != null);
                if (res != null) { return res; }
                pos = assemblyName.LastIndexOf(".");
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
            method = t.GetMethods().FirstOrDefault(m => IsMethod(m));
            if (method == null)
            {
                 var all = t.GetMethods(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                 method = all.FirstOrDefault(m => IsMethod(m));
            }
        }

        initialized = true;
    }

    private bool IsMethod(MethodInfo m)
    {
        if (m.Name != Name) { return false; }
        var parameters = m.GetParameters().Select(p => p.ParameterType.ToString()).ToArray();
        var signature = string.Format("({0})", string.Join(",", parameters));
        return signature == ParamsSignature;
    }
}
