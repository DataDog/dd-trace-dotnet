// <copyright file="InstrumentationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.AutoInstrumentation.Generator.Core.Resources;
using dnlib.DotNet;

namespace Datadog.AutoInstrumentation.Generator.Core;

/// <summary>
/// Stateless code generation engine. Produces CallTarget integration source code
/// from a method definition and configuration.
/// </summary>
public class InstrumentationGenerator
{
    /// <summary>
    /// Generates CallTarget integration source code for the given method.
    /// </summary>
    public GenerationResult Generate(MethodDef methodDef, GenerationConfiguration config)
    {
        if (methodDef.DeclaringType.IsValueType)
        {
            if (methodDef.IsStatic)
            {
                return GenerationResult.Error("Static method for ValueTypes are not supported by CallTarget.");
            }

            if (methodDef.DeclaringType.HasGenericParameters)
            {
                return GenerationResult.Error("Generics ValueTypes are not supported by CallTarget.");
            }
        }

        if (methodDef.DeclaringType.IsInterface && methodDef.DeclaringType.HasGenericParameters)
        {
            return GenerationResult.Error("Generics Interfaces are not supported by CallTarget.");
        }

        var assemblyName = methodDef.DeclaringType.DefinitionAssembly.Name;
        var typeName = methodDef.DeclaringType.IsNested
            ? methodDef.DeclaringType.DeclaringType.FullName + "+" + methodDef.DeclaringType.Name
            : methodDef.DeclaringType.FullName;
        var methodName = methodDef.Name;
        var returnTypeName = EditorHelper.GetReturnType(methodDef);
        var parameterTypeNames = EditorHelper.GetParameterTypeArray(methodDef);
        var minimumVersion = EditorHelper.GetMinimumVersion(methodDef);
        var maximumVersion = EditorHelper.GetMaximumVersion(methodDef);
        var integrationClassName = EditorHelper.GetIntegrationClassName(methodDef);
        var integrationValue = EditorHelper.GetIntegrationValue(methodDef);
        var fileName = EditorHelper.GetFileName(methodDef);
        var ns = EditorHelper.GetNamespace(methodDef);

        var integrationSourceBuilder = new StringBuilder(ResourceLoader.LoadResource("Integration.cs"));
        integrationSourceBuilder.Replace("$(Filename)", fileName);
        integrationSourceBuilder.Replace("$(Namespace)", ns);
        integrationSourceBuilder.Replace("$(FullName)", EditorHelper.GetMethodFullNameForComments(methodDef));
        integrationSourceBuilder.Replace("$(AssemblyName)", assemblyName);
        integrationSourceBuilder.Replace("$(TypeName)", typeName);
        integrationSourceBuilder.Replace("$(MethodName)", methodName);
        integrationSourceBuilder.Replace("$(ReturnTypeName)", returnTypeName);
        integrationSourceBuilder.Replace("$(ParameterTypeNames)", parameterTypeNames);
        integrationSourceBuilder.Replace("$(MinimumVersion)", minimumVersion);
        integrationSourceBuilder.Replace("$(MaximumVersion)", maximumVersion);
        integrationSourceBuilder.Replace("$(IntegrationClassName)", integrationClassName);
        integrationSourceBuilder.Replace("$(IntegrationValue)", integrationValue);

        if (methodDef.DeclaringType.IsInterface)
        {
            integrationSourceBuilder.Replace("$(IntegrationKind)", $",{Environment.NewLine}    CallTargetIntegrationKind = CallTargetKind.Interface");
        }
        else
        {
            integrationSourceBuilder.Replace("$(IntegrationKind)", string.Empty);
        }

        var isStatic = methodDef.IsStatic;
        var isVoid = !methodDef.HasReturnType;

        var createDucktypeInstanceEnabled = config.CreateOnMethodBegin || config.CreateOnMethodEnd || config.CreateOnAsyncMethodEnd;

        Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition>? duckTypeProxyDefinitions = null;
        if (!isStatic && (config.CreateDucktypeInstance && createDucktypeInstanceEnabled))
        {
            duckTypeProxyDefinitions = EditorHelper.GetDuckTypeProxies(methodDef.DeclaringType, config.DucktypeInstanceFields, config.DucktypeInstanceProperties, config.DucktypeInstanceMethods, config.DucktypeInstanceDuckChaining, config.UseDuckCopyStruct);
        }

        duckTypeProxyDefinitions ??= new Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition>();

        // OnMethodBegin
        integrationSourceBuilder.Replace("$(OnMethodBegin)", config.CreateOnMethodBegin ? $"{Environment.NewLine}{GetOnMethodBeginSourceBuilder(isStatic, methodDef, config, duckTypeProxyDefinitions)}" : string.Empty);

        // OnMethodEnd
        integrationSourceBuilder.Replace("$(OnMethodEnd)", config.CreateOnMethodEnd ? $"{Environment.NewLine}{GetOnMethodEndSourceBuilder(isStatic, isVoid, methodDef, config, duckTypeProxyDefinitions)}" : string.Empty);

        // OnAsyncMethodEnd
        integrationSourceBuilder.Replace("$(OnAsyncMethodEnd)", config.CreateOnAsyncMethodEnd ? $"{Environment.NewLine}{GetOnAsyncMethodEndBuilder(isStatic, methodDef, config, duckTypeProxyDefinitions)}" : string.Empty);

        var duckTypeProxyDefinitionsValues = duckTypeProxyDefinitions.Values.Where(v => !string.IsNullOrEmpty(v.ProxyDefinition)).Select(v => v.ProxyDefinition).ToList();
        if (duckTypeProxyDefinitionsValues.Count > 0)
        {
            integrationSourceBuilder.Replace("$(DuckTypeDefinitions)", string.Join(Environment.NewLine, duckTypeProxyDefinitionsValues) + Environment.NewLine);
        }
        else
        {
            integrationSourceBuilder.Replace("$(DuckTypeDefinitions)", string.Empty);
        }

        return new GenerationResult
        {
            Success = true,
            SourceCode = integrationSourceBuilder.ToString(),
            FileName = fileName,
            Namespace = ns,
            Metadata = new InstrumentMethodMetadata
            {
                AssemblyName = assemblyName,
                TypeName = typeName,
                MethodName = methodName,
                ReturnTypeName = returnTypeName,
                ParameterTypeNames = parameterTypeNames,
                MinimumVersion = minimumVersion,
                MaximumVersion = maximumVersion,
                IntegrationName = integrationValue,
                IntegrationClassName = integrationClassName,
                IsInterface = methodDef.DeclaringType.IsInterface,
            },
        };
    }

    private static StringBuilder GetOnMethodBeginSourceBuilder(bool isStatic, MethodDef methodDef, GenerationConfiguration config, Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition> duckTypeProxyDefinitions)
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

        if (config.CreateDucktypeInstance)
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
                if (config.CreateDucktypeArguments && parameter.Type.TryGetTypeDef() is { } paramTypeDef)
                {
                    var proxyDefinition = EditorHelper.GetDuckTypeProxies(paramTypeDef, config.DucktypeArgumentsFields, config.DucktypeArgumentsProperties, config.DucktypeArgumentsMethods, config.DucktypeArgumentsDuckChaining, config.UseDuckCopyStruct, duckTypeProxyDefinitions);
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
        onMethodBeginSourceBuilder.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 || methodDef.DeclaringType.HasGenericParameters ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));
        return onMethodBeginSourceBuilder;
    }

    private static StringBuilder GetOnMethodEndSourceBuilder(bool isStatic, bool isVoid, MethodDef methodDef, GenerationConfiguration config, Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onMethodEndSource = new StringBuilder(isVoid ?
            (isStatic ? ResourceLoader.LoadResource("OnMethodEndVoidStatic.cs") : ResourceLoader.LoadResource("OnMethodEndVoid.cs")) :
            (isStatic ? ResourceLoader.LoadResource("OnMethodEndStatic.cs") : ResourceLoader.LoadResource("OnMethodEnd.cs")));

        var argsConstraint = new List<string>();

        if (config.CreateDucktypeInstance)
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
                if (config.CreateDucktypeReturnValue && methodDef.ReturnType.TryGetTypeDef() is { } returnTypeDef)
                {
                    var proxyDefinition = EditorHelper.GetDuckTypeProxies(returnTypeDef, config.DucktypeReturnValueFields, config.DucktypeReturnValueProperties, config.DucktypeReturnValueMethods, config.DucktypeReturnValueDuckChaining, config.UseDuckCopyStruct, duckTypeProxyDefinitions);
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

        onMethodEndSource.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 || methodDef.DeclaringType.HasGenericParameters ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));

        return onMethodEndSource;
    }

    private static StringBuilder GetOnAsyncMethodEndBuilder(bool isStatic, MethodDef methodDef, GenerationConfiguration config, Dictionary<TypeDef, EditorHelper.DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onAsyncMethodEndSource = new StringBuilder(isStatic ?
            ResourceLoader.LoadResource("OnAsyncMethodEndStatic.cs") :
            ResourceLoader.LoadResource("OnAsyncMethodEnd.cs"));

        var argsConstraint = new List<string>();

        if (config.CreateDucktypeInstance)
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
                if (config.CreateDucktypeAsyncReturnValue && genericReturnValue.TryGetTypeDef() is { } returnTypeDef)
                {
                    var proxyDefinition = EditorHelper.GetDuckTypeProxies(returnTypeDef, config.DucktypeAsyncReturnValueFields, config.DucktypeAsyncReturnValueProperties, config.DucktypeAsyncReturnValueMethods, config.DucktypeAsyncReturnValueDuckChaining, config.UseDuckCopyStruct, duckTypeProxyDefinitions);
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
        onAsyncMethodEndSource.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 || methodDef.DeclaringType.HasGenericParameters ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));
        return onAsyncMethodEndSource;
    }
}
