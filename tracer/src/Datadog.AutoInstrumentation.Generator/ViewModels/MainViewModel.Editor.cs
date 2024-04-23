// <copyright file="MainViewModel.Editor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.AutoInstrumentation.Generator.Resources;
using dnlib.DotNet;
using ReactiveUI;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal partial class MainViewModel
{
    private string _sourceCode = string.Empty;

    public string SourceCode
    {
        get => _sourceCode;
        private set => this.RaiseAndSetIfChanged(ref _sourceCode, value);
    }

    private void InitEditor()
    {
        var subscribeAction = (bool value) => UpdateSourceCode();
        this.WhenAnyValue(o => o.AssemblyPath).Subscribe(_ => UpdateSourceCode());
        this.WhenAnyValue(o => o.SelectedMethod).Subscribe(_ => UpdateSourceCode());
        this.WhenAnyValue(o => o.CreateOnMethodBegin).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodEnd).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnAsyncMethodEnd).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.UseDuckCopyStruct).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeInstance).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceDuckChaining).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeArguments).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsDuckChaining).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeReturnValue).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueDuckChaining).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeAsyncReturnValue).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueDuckChaining).Subscribe(subscribeAction);
    }

    private void UpdateSourceCode()
    {
        if (SelectedMethod is { } methodDef)
        {
            if (methodDef.DeclaringType.IsValueType)
            {
                if (methodDef.IsStatic)
                {
                    SourceCode = "// Static method for ValueTypes are not supported by CallTarget.";
                    return;
                }

                if (methodDef.DeclaringType.HasGenericParameters)
                {
                    SourceCode = "// Generics ValueTypes are not supported by CallTarget.";
                    return;
                }
            }

            var integrationSourceBuilder = new StringBuilder(ResourceLoader.LoadResource("Integration.cs"));
            integrationSourceBuilder.Replace("$(Filename)", EditorHelper.GetFileName(methodDef));
            integrationSourceBuilder.Replace("$(Namespace)", EditorHelper.GetNamespace(methodDef));
            integrationSourceBuilder.Replace("$(FullName)", EditorHelper.GetMethodFullNameForComments(methodDef));
            integrationSourceBuilder.Replace("$(AssemblyName)", methodDef.DeclaringType.DefinitionAssembly.Name);
            if (methodDef.DeclaringType.IsNested)
            {
                integrationSourceBuilder.Replace(
                    "$(TypeName)",
                    methodDef.DeclaringType.DeclaringType.FullName + "+" + methodDef.DeclaringType.Name);
            }
            else
            {
                integrationSourceBuilder.Replace("$(TypeName)", methodDef.DeclaringType.FullName);
            }

            integrationSourceBuilder.Replace("$(MethodName)", methodDef.Name);
            integrationSourceBuilder.Replace("$(ReturnTypeName)", EditorHelper.GetReturnType(methodDef));
            integrationSourceBuilder.Replace("$(ParameterTypeNames)", EditorHelper.GetParameterTypeArray(methodDef));
            integrationSourceBuilder.Replace("$(MinimumVersion)", EditorHelper.GetMinimumVersion(methodDef));
            integrationSourceBuilder.Replace("$(MaximumVersion)", EditorHelper.GetMaximumVersion(methodDef));
            integrationSourceBuilder.Replace("$(IntegrationClassName)", EditorHelper.GetIntegrationClassName(methodDef));
            integrationSourceBuilder.Replace("$(IntegrationName)", EditorHelper.GetIntegrationName(methodDef));

            var isStatic = methodDef.IsStatic;
            var isVoid = !methodDef.HasReturnType;

            Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition>? duckTypeProxyDefinitions = null;
            if (!isStatic && (CreateDucktypeInstance && CreateDucktypeInstanceEnabled))
            {
                duckTypeProxyDefinitions = EditorHelper.GetDuckTypeProxies(methodDef.DeclaringType, DucktypeInstanceFields, DucktypeInstanceProperties, DucktypeInstanceMethods, DucktypeInstanceDuckChaining, UseDuckCopyStruct);
            }

            duckTypeProxyDefinitions ??= new Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition>();

            // OnMethodBegin
            integrationSourceBuilder.Replace("$(OnMethodBegin)", CreateOnMethodBegin ? $"{Environment.NewLine}{GetOnMethodBeginSourceBuilder(isStatic, methodDef, duckTypeProxyDefinitions)}" : string.Empty);

            // OnMethodEnd
            integrationSourceBuilder.Replace("$(OnMethodEnd)", CreateOnMethodEnd ? $"{Environment.NewLine}{GetOnMethodEndSourceBuilder(isStatic, isVoid, methodDef, duckTypeProxyDefinitions)}" : string.Empty);

            // OnAsyncMethodEnd
            integrationSourceBuilder.Replace("$(OnAsyncMethodEnd)", CreateOnAsyncMethodEnd ? $"{Environment.NewLine}{GetOnAsyncMethodEndBuilder(isStatic, methodDef, duckTypeProxyDefinitions)}" : string.Empty);

            var duckTypeProxyDefinitionsValues = duckTypeProxyDefinitions.Values.Where(v => !string.IsNullOrEmpty(v.ProxyDefinition)).Select(v => v.ProxyDefinition).ToList();
            if (duckTypeProxyDefinitionsValues.Count > 0)
            {
                integrationSourceBuilder.Replace("$(DuckTypeDefinitions)", string.Join(Environment.NewLine, duckTypeProxyDefinitionsValues) + Environment.NewLine);
            }
            else
            {
                integrationSourceBuilder.Replace("$(DuckTypeDefinitions)", string.Empty);
            }

            SourceCode = integrationSourceBuilder.ToString();
        }
        else
        {
            if (string.IsNullOrEmpty(AssemblyPath))
            {
                SourceCode = "// Open an assembly using the File icon button.";
            }
            else
            {
                SourceCode = "// Select a method to show the source code of the integration.";
            }
        }
    }

    private StringBuilder GetOnMethodBeginSourceBuilder(bool isStatic, MethodDef methodDef, Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onMethodBeginSourceBuilder = new StringBuilder(isStatic
            ? ResourceLoader.LoadResource("OnMethodBeginStatic.cs")
            : ResourceLoader.LoadResource("OnMethodBegin.cs"));

        var parameters = methodDef.Parameters.Where(p => !p.IsHiddenThisParameter).ToArray();

        var argsTypesTypeParamDocumentation = new List<string>();
        var argsTypesParamDocumentation = new List<string>();
        var argsTypes = new List<string>();
        var argsParameters = new List<string>();
        var argsConstraint = new List<string>();

        if (CreateDucktypeInstance)
        {
            if (duckTypeProxyDefinitions.TryGetValue(methodDef.DeclaringType, out var proxyDefinition))
            {
                argsConstraint.Add($"        where TTarget : {proxyDefinition.ProxyName}");
            }
        }

        var i = 0;
        foreach (var parameter in parameters)
        {
            i++;

            var parameterTypeCleaned = parameter.Type.FullName.Replace("<", "[").Replace(">", "]").Replace("&", "&amp;");
            var parameterName = parameter.Name;
            if (string.IsNullOrEmpty(parameterName))
            {
                parameterName = $"arg{i}";
            }

            argsTypesParamDocumentation.Add($"    /// <param name=\"{parameterName}\">Instance of {parameterTypeCleaned}</param>");

            var realPType = parameter.Type.IsByRef ? parameter.Type.Next : parameter.Type;
            var gentTypeName = "T" + char.ToUpperInvariant(parameterName[0]) + parameterName[1..];
            var basicType = EditorHelper.GetIfBasicTypeOrDefault(parameter.Type.FullName);
            if (basicType is not null)
            {
                argsParameters.Add(realPType.IsValueType ? $"ref {basicType} {parameterName}" : $"ref {basicType}? {parameterName}");
            }
            else
            {
                argsTypesTypeParamDocumentation.Add($"    /// <typeparam name=\"{gentTypeName}\">Type of the argument {parameterName} ({parameterTypeCleaned})</typeparam>");
                argsTypes.Add($"{gentTypeName}");
                var withConstraint = false;
                if (CreateDucktypeArguments && parameter.Type.TryGetTypeDef() is { } paramTypeDef)
                {
                    var proxyDefinition = EditorHelper.GetDuckTypeProxies(paramTypeDef, DucktypeArgumentsFields, DucktypeArgumentsProperties, DucktypeArgumentsMethods, DucktypeArgumentsDuckChaining, UseDuckCopyStruct, duckTypeProxyDefinitions);
                    if (proxyDefinition is not null)
                    {
                        argsConstraint.Add($"        where {gentTypeName} : {proxyDefinition.Value.ProxyName}");
                        withConstraint = true;
                    }
                }

                if (withConstraint)
                {
                    argsParameters.Add($"{gentTypeName} {parameterName}");
                }
                else
                {
                    argsParameters.Add(realPType.IsValueType
                        ? $"ref {gentTypeName} {parameterName}"
                        : $"ref {gentTypeName}? {parameterName}");
                }
            }
        }

        var strTArgsTypes = argsTypes.Count == 0 ? string.Empty : ", " + string.Join(", ", argsTypes);
        var strTArgsParameters = argsParameters.Count == 0 ? string.Empty : string.Join(", ", argsParameters);
        if (!string.IsNullOrEmpty(strTArgsParameters) && !isStatic)
        {
            strTArgsParameters = ", " + strTArgsParameters;
        }

        onMethodBeginSourceBuilder.Replace("$(TArgsTypesTypeParamDocumentation)", argsTypesTypeParamDocumentation.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsTypesTypeParamDocumentation));
        onMethodBeginSourceBuilder.Replace("$(TArgsTypesParamDocumentation)", argsTypesParamDocumentation.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsTypesParamDocumentation));
        onMethodBeginSourceBuilder.Replace("$(TArgsTypes)", strTArgsTypes);
        onMethodBeginSourceBuilder.Replace("$(TArgsParameters)", strTArgsParameters);
        onMethodBeginSourceBuilder.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));
        return onMethodBeginSourceBuilder;
    }

    private StringBuilder GetOnMethodEndSourceBuilder(bool isStatic, bool isVoid, MethodDef methodDef, Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onMethodEndSource = new StringBuilder(isVoid ?
            (isStatic ? ResourceLoader.LoadResource("OnMethodEndVoidStatic.cs") : ResourceLoader.LoadResource("OnMethodEndVoid.cs")) :
            (isStatic ? ResourceLoader.LoadResource("OnMethodEndStatic.cs") : ResourceLoader.LoadResource("OnMethodEnd.cs")));

        var argsConstraint = new List<string>();

        if (CreateDucktypeInstance)
        {
            if (duckTypeProxyDefinitions.TryGetValue(methodDef.DeclaringType, out var proxyDefinition))
            {
                argsConstraint.Add($"        where TTarget : {proxyDefinition.ProxyName}");
            }
        }

        if (!isVoid)
        {
            var returnTypeParamDocumentation = string.Empty;
            var returnType = string.Empty;
            var returnTypeParameter = string.Empty;

            var returnTypeCleaned = methodDef.ReturnType.FullName.Replace("<", "[").Replace(">", "]").Replace("&", "&amp;");
            var returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Instance of {returnTypeCleaned}</param>";

            var rType = methodDef.ReturnType.IsByRef ? methodDef.ReturnType.Next : methodDef.ReturnType;
            var basicType = EditorHelper.GetIfBasicTypeOrDefault(methodDef.ReturnType.FullName);
            if (basicType is not null)
            {
                returnTypeParameter = basicType;
                if (!rType.IsValueType)
                {
                    returnTypeParameter += "?";
                }
            }
            else
            {
                returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Type of the return value ({returnTypeCleaned})</typeparam>";
                returnType = ", TReturn";
                returnTypeParameter = "TReturn";
                if (CreateDucktypeReturnValue && methodDef.ReturnType.TryGetTypeDef() is { } returnTypeDef)
                {
                    var proxyDefinition = EditorHelper.GetDuckTypeProxies(returnTypeDef, DucktypeReturnValueFields, DucktypeReturnValueProperties, DucktypeReturnValueMethods, DucktypeReturnValueDuckChaining, UseDuckCopyStruct, duckTypeProxyDefinitions);
                    if (proxyDefinition is not null)
                    {
                        argsConstraint.Add($"        where TReturn : {proxyDefinition.Value.ProxyName}");
                    }
                }
                else if (!rType.IsValueType)
                {
                    returnTypeParameter = "TReturn?";
                }
            }

            onMethodEndSource.Replace("$(TReturnTypeParamDocumentation)", returnTypeParamDocumentation);
            onMethodEndSource.Replace("$(TReturnParamDocumentation)", returnParamDocumentation);
            onMethodEndSource.Replace("$(TReturnType)", returnType);
            onMethodEndSource.Replace("$(TReturnTypeParameter)", returnTypeParameter);
        }

        onMethodEndSource.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));

        return onMethodEndSource;
    }

    private StringBuilder GetOnAsyncMethodEndBuilder(bool isStatic, MethodDef methodDef, Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onAsyncMethodEndSource = new StringBuilder(isStatic ?
            ResourceLoader.LoadResource("OnAsyncMethodEndStatic.cs") :
            ResourceLoader.LoadResource("OnAsyncMethodEnd.cs"));

        var argsConstraint = new List<string>();

        if (CreateDucktypeInstance)
        {
            if (duckTypeProxyDefinitions.TryGetValue(methodDef.DeclaringType, out var proxyDefinition))
            {
                argsConstraint.Add($"        where TTarget : {proxyDefinition.ProxyName}");
            }
        }

        var returnParamDocumentation = string.Empty;
        var returnTypeParamDocumentation = string.Empty;
        var returnType = string.Empty;
        var returnTypeParameter = string.Empty;

        if (!methodDef.ReturnType.IsGenericInstanceType)
        {
            returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Always NULL: Async type doesn't have generic argument. (Task or ValueTask)</param>";
            returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Dummy object type due original return value doesn't have a generic argument.</typeparam>";
            returnType = ", TReturn";
            returnTypeParameter = "TReturn?";
        }
        else if (methodDef.ReturnType.ToGenericInstSig().GenericArguments.Count > 1)
        {
            returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Instance of return type</param>";
            returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Type of the return value</typeparam>";
            returnType = ", TReturn";
            returnTypeParameter = "TReturn?";
        }
        else
        {
            var genericReturnValue = methodDef.ReturnType.ToGenericInstSig().GenericArguments[0];
            var genericReturnType = genericReturnValue.IsByRef ? genericReturnValue.Next : genericReturnValue;
            var returnTypeCleaned = genericReturnValue.FullName.Replace("<", "[").Replace(">", "]").Replace("&", "&amp;");
            returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Instance of {returnTypeCleaned}</param>";
            var basicType = EditorHelper.GetIfBasicTypeOrDefault(genericReturnValue.FullName);
            if (basicType is not null)
            {
                returnTypeParameter = basicType;
                if (!genericReturnType.IsValueType)
                {
                    returnTypeParameter += "?";
                }
            }
            else
            {
                returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Type of the return value ({returnTypeCleaned})</typeparam>";
                returnType = ", TReturn";
                returnTypeParameter = "TReturn";
                if (CreateDucktypeAsyncReturnValue && genericReturnValue.TryGetTypeDef() is { } returnTypeDef)
                {
                    var proxyDefinition = EditorHelper.GetDuckTypeProxies(returnTypeDef, DucktypeAsyncReturnValueFields, DucktypeAsyncReturnValueProperties, DucktypeAsyncReturnValueMethods, DucktypeAsyncReturnValueDuckChaining, UseDuckCopyStruct, duckTypeProxyDefinitions);
                    if (proxyDefinition is not null)
                    {
                        argsConstraint.Add($"        where TReturn : {proxyDefinition.Value.ProxyName}");
                    }
                }
                else if (!genericReturnType.IsValueType)
                {
                    returnTypeParameter = "TReturn?";
                }
            }
        }

        onAsyncMethodEndSource.Replace("$(TReturnTypeParamDocumentation)", returnTypeParamDocumentation);
        onAsyncMethodEndSource.Replace("$(TReturnParamDocumentation)", returnParamDocumentation);
        onAsyncMethodEndSource.Replace("$(TReturnType)", returnType);
        onAsyncMethodEndSource.Replace("$(TReturnTypeParameter)", returnTypeParameter);
        onAsyncMethodEndSource.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));
        return onAsyncMethodEndSource;
    }
}
