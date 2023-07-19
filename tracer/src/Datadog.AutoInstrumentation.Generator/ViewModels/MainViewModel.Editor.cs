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
    private static readonly string CompilerGeneratedAttributeName = typeof(CompilerGeneratedAttribute).FullName!;
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();
    private string _sourceCode = string.Empty;

    public string SourceCode
    {
        get => _sourceCode;
        private set => this.RaiseAndSetIfChanged(ref _sourceCode, value);
    }

    private static string GetFullName(MethodDef methodDef)
    {
        return methodDef.FullName
            .Replace("<", "[")
            .Replace(">", "]");
    }

    private static string GetFileName(MethodDef methodDef)
    {
        var value = methodDef.DeclaringType.Name + "_" + methodDef.Name;
        value = ClearString(value);
        return new string(value.Where(c => !InvalidChars.Contains(c) && c != '.').ToArray());
    }

    private static string GetNamespace(MethodDef methodDef)
    {
        return methodDef.DeclaringType.DefinitionAssembly.Name.Replace(".", "_");
    }

    private static string GetIntegrationClassName(MethodDef methodDef)
    {
        var value = methodDef.DeclaringType.Name + "_" + methodDef.Name;
        value = value
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace("/", string.Empty)
            .Replace(".", string.Empty)
            .Replace("`", "Generic")
            .Replace("|", "_");
        return new string(value.Where(c => !InvalidChars.Contains(c)).ToArray());
    }

    private static string GetIntegrationName(MethodDef methodDef)
    {
        var value = methodDef.DeclaringType.Name.ToString();
        value = value.Replace("`", "Generic");
        return new string(value.Where(c => !InvalidChars.Contains(c)).ToArray());
    }

    private static string GetMinimumVersion(MethodDef methodDef)
    {
        var version = methodDef.DeclaringType.DefinitionAssembly.Version;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string GetMaximumVersion(MethodDef methodDef)
    {
        var version = methodDef.DeclaringType.DefinitionAssembly.Version;
        return $"{version.Major}.*.*";
    }

    private static string GetReturnType(MethodDef methodDef)
    {
        return CreateTypeName(methodDef.ReturnType);
    }

    private static string GetParameterTypeArray(MethodDef methodDef)
    {
        var parameters = methodDef.Parameters.Where(p => !p.IsHiddenThisParameter).ToArray();
        if (parameters.Length == 0)
        {
            return "new string[0]";
        }

        var sb = new StringBuilder();
        sb.Append("new[] { ")
            .Append(string.Join(", ", parameters.Select(p => CreateTypeName(p.Type))))
            .Append(" }");
        return sb.ToString();
    }

    private static string ClearString(string value)
    {
        return value
            .Replace("<", "[")
            .Replace(">", "]")
            .Replace("/", ".")
            .Replace("`", "Generic")
            .Replace("|", "_");
    }

    private static string CreateTypeName(TypeSig typeSig)
    {
        var typeFullName = string.Empty;
        if (typeSig is GenericMVar genMVar)
        {
            typeFullName = $"!!{genMVar.Number}";
        }
        else if (typeSig is GenericInstSig genType)
        {
            var genTypeFullName = genType.GenericType.FullName;
            var genArgs = genType.GenericArguments;

            var sb = new StringBuilder(typeSig.FullName.Length + (genArgs.Count * 2));
            sb.Append(genTypeFullName)
                .Append('[');
            for (var i = 0; i < genArgs.Count; i++)
            {
                if (genArgs[i] is GenericSig genericSig)
                {
                    var replacement = genericSig.IsTypeVar ? $"!{i}" : $"!!{i}";
                    sb.Append(replacement);
                }
                else
                {
                    sb.Append(genArgs[i].FullName);
                }

                if (i < genArgs.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(']');

            typeFullName = sb.ToString();
        }
        else
        {
            typeFullName = typeSig.FullName;
        }

        return CreateTypeName(typeFullName);
    }

    private static string CreateTypeName(string fullTypeName)
    {
        fullTypeName = fullTypeName
            .Replace("<", "[")
            .Replace(">", "]")
            .Replace("/", ".");
        return fullTypeName switch
        {
            ClrNames.Type => $"{nameof(ClrNames)}.{nameof(ClrNames.Type)}",
            ClrNames.Activity => $"{nameof(ClrNames)}.{nameof(ClrNames.Activity)}",
            ClrNames.Bool => $"{nameof(ClrNames)}.{nameof(ClrNames.Bool)}",
            ClrNames.Int16 => $"{nameof(ClrNames)}.{nameof(ClrNames.Int16)}",
            ClrNames.Int32 => $"{nameof(ClrNames)}.{nameof(ClrNames.Int32)}",
            ClrNames.Int64 => $"{nameof(ClrNames)}.{nameof(ClrNames.Int64)}",
            ClrNames.Byte => $"{nameof(ClrNames)}.{nameof(ClrNames.Byte)}",
            ClrNames.Object => $"{nameof(ClrNames)}.{nameof(ClrNames.Object)}",
            ClrNames.Process => $"{nameof(ClrNames)}.{nameof(ClrNames.Process)}",
            ClrNames.Stream => $"{nameof(ClrNames)}.{nameof(ClrNames.Stream)}",
            ClrNames.String => $"{nameof(ClrNames)}.{nameof(ClrNames.String)}",
            ClrNames.Task => $"{nameof(ClrNames)}.{nameof(ClrNames.Task)}",
            ClrNames.Void => $"{nameof(ClrNames)}.{nameof(ClrNames.Void)}",
            ClrNames.AsyncCallback => $"{nameof(ClrNames)}.{nameof(ClrNames.AsyncCallback)}",
            ClrNames.ByteArray => $"{nameof(ClrNames)}.{nameof(ClrNames.ByteArray)}",
            ClrNames.CancellationToken => $"{nameof(ClrNames)}.{nameof(ClrNames.CancellationToken)}",
            ClrNames.GenericTask => $"{nameof(ClrNames)}.{nameof(ClrNames.GenericTask)}",
            ClrNames.Int32Task => $"{nameof(ClrNames)}.{nameof(ClrNames.Int32Task)}",
            ClrNames.ObjectTask => $"{nameof(ClrNames)}.{nameof(ClrNames.ObjectTask)}",
            ClrNames.SByte => $"{nameof(ClrNames)}.{nameof(ClrNames.SByte)}",
            ClrNames.TimeSpan => $"{nameof(ClrNames)}.{nameof(ClrNames.TimeSpan)}",
            ClrNames.UInt16 => $"{nameof(ClrNames)}.{nameof(ClrNames.UInt16)}",
            ClrNames.UInt32 => $"{nameof(ClrNames)}.{nameof(ClrNames.UInt32)}",
            ClrNames.UInt64 => $"{nameof(ClrNames)}.{nameof(ClrNames.UInt64)}",
            ClrNames.GenericTaskWithGenericClassParameter => $"{nameof(ClrNames)}.{nameof(ClrNames.GenericTaskWithGenericClassParameter)}",
            ClrNames.GenericTaskWithGenericMethodParameter => $"{nameof(ClrNames)}.{nameof(ClrNames.GenericTaskWithGenericMethodParameter)}",
            ClrNames.HttpRequestMessage => $"{nameof(ClrNames)}.{nameof(ClrNames.HttpRequestMessage)}",
            ClrNames.HttpResponseMessage => $"{nameof(ClrNames)}.{nameof(ClrNames.HttpResponseMessage)}",
            ClrNames.IAsyncResult => $"{nameof(ClrNames)}.{nameof(ClrNames.IAsyncResult)}",
            ClrNames.HttpResponseMessageTask => $"{nameof(ClrNames)}.{nameof(ClrNames.HttpResponseMessageTask)}",
            _ => $"\"{fullTypeName}\""
        };
    }

    private static string? GetIfBasicTypeOrDefault(string fullTypeName)
    {
        if (fullTypeName[fullTypeName.Length - 1] == '&')
        {
            fullTypeName = fullTypeName.Substring(0, fullTypeName.Length - 1);
        }

        return fullTypeName switch
        {
            "System.Object" => "object",
            "System.Action" => "Action",
            "System.Boolean" => "bool",
            "System.String" => "string",
            "System.Text.StringBuilder" => "System.Text.StringBuilder",
            "System.SByte" => "sbyte",
            "System.Byte" => "byte",
            "System.Int16" => "short",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.IntPtr" => "IntPtr",
            "System.UInt16" => "ushort",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.UIntPtr" => "UIntPtr",
            "System.Decimal" => "decimal",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Char" => "char",
            "System.TimeSpan" => "TimeSpan",
            "System.DateTime" => "DateTime",
            "System.DateTimeOffset" => "DateTimeOffset",
            "System.IO.Stream" => "Stream",
            "System.Threading.Tasks.Task" => "Task",
            "System.Threading.CancellationToken" => "CancellationToken",
            "System.Diagnostics.Process" => "System.Diagnostics.Process",
            "System.IAsyncResult" => "IAsyncResult",
            "System.AsyncCallback" => "AsyncCallback",
            "System.Exception" => "Exception",
            "System.Type" => "Type",
            "System.Reflection.MethodInfo" => "System.Reflection.MethodInfo",
            "System.Delegate" => "Delegate",
            "System.Uri" => "Uri",
            "System.Nullable`1<System.Boolean>" => "bool?",
            "System.Nullable`1<System.SByte>" => "sbyte?",
            "System.Nullable`1<System.Byte>" => "byte?",
            "System.Nullable`1<System.Int16>" => "short?",
            "System.Nullable`1<System.Int32>" => "int?",
            "System.Nullable`1<System.Int64>" => "long?",
            "System.Nullable`1<System.IntPtr>" => "IntPtr?",
            "System.Nullable`1<System.UInt16>" => "ushort?",
            "System.Nullable`1<System.UInt32>" => "uint?",
            "System.Nullable`1<System.UInt64>" => "ulong?",
            "System.Nullable`1<System.UIntPtr>" => "UIntPtr?",
            "System.Nullable`1<System.Decimal>" => "decimal?",
            "System.Nullable`1<System.Single>" => "float?",
            "System.Nullable`1<System.Double>" => "double?",
            "System.Nullable`1<System.Char>" => "char?",
            "System.Nullable`1<System.TimeSpan>" => "TimeSpan?",
            "System.Nullable`1<System.DateTime>" => "DateTime?",
            "System.Nullable`1<System.DateTimeOffset>" => "DateTimeOffset?",
            "System.ArraySegment`1<System.Object>" => "ArraySegment<object>",
            "System.ArraySegment`1<System.Boolean>" => "ArraySegment<bool>",
            "System.ArraySegment`1<System.SByte>" => "ArraySegment<sbyte>",
            "System.ArraySegment`1<System.Byte>" => "ArraySegment<byte>",
            "System.ArraySegment`1<System.Int16>" => "ArraySegment<short>",
            "System.ArraySegment`1<System.Int32>" => "ArraySegment<int>",
            "System.ArraySegment`1<System.Int64>" => "ArraySegment<long>",
            "System.ArraySegment`1<System.IntPtr>" => "ArraySegment<IntPtr>",
            "System.ArraySegment`1<System.UInt16>" => "ArraySegment<ushort>",
            "System.ArraySegment`1<System.UInt32>" => "ArraySegment<uint>",
            "System.ArraySegment`1<System.UInt64>" => "ArraySegment<ulong>",
            "System.ArraySegment`1<System.UIntPtr>" => "ArraySegment<UIntPtr>",
            "System.ArraySegment`1<System.Decimal>" => "ArraySegment<decimal>",
            "System.ArraySegment`1<System.Single>" => "ArraySegment<float>",
            "System.ArraySegment`1<System.Double>" => "ArraySegment<double>",
            "System.ArraySegment`1<System.Char>" => "ArraySegment<char>",
            "System.ArraySegment`1<System.TimeSpan>" => "ArraySegment<TimeSpan>",
            "System.ArraySegment`1<System.DateTime>" => "ArraySegment<DateTime>",
            "System.ArraySegment`1<System.DateTimeOffset>" => "ArraySegment<DateTimeOffset>",
            "System.Object[]" => "object[]",
            "System.Action[]" => "Action[]",
            "System.Boolean[]" => "bool[]",
            "System.String[]" => "string[]",
            "System.Text.StringBuilder[]" => "System.Text.StringBuilder[]",
            "System.SByte[]" => "sbyte[]",
            "System.Byte[]" => "byte[]",
            "System.Int16[]" => "short[]",
            "System.Int32[]" => "int[]",
            "System.Int64[]" => "long[]",
            "System.IntPtr[]" => "IntPtr[]",
            "System.UInt16[]" => "ushort[]",
            "System.UInt32[]" => "uint[]",
            "System.UInt64[]" => "ulong[]",
            "System.UIntPtr[]" => "UIntPtr[]",
            "System.Decimal[]" => "decimal[]",
            "System.Single[]" => "float[]",
            "System.Double[]" => "double[]",
            "System.Char[]" => "char[]",
            "System.TimeSpan[]" => "TimeSpan[]",
            "System.DateTime[]" => "DateTime[]",
            "System.DateTimeOffset[]" => "DateTimeOffset[]",
            "System.IO.Stream[]" => "Stream[]",
            "System.Threading.Tasks.Task[]" => "Task[]",
            "System.Threading.CancellationToken[]" => "CancellationToken[]",
            "System.Diagnostics.Process[]" => "System.Diagnostics.Process[]",
            "System.IAsyncResult[]" => "IAsyncResult[]",
            "System.AsyncCallback[]" => "AsyncCallback[]",
            "System.Exception[]" => "Exception[]",
            "System.Type[]" => "Type[]",
            "System.Reflection.MethodInfo[]" => "System.Reflection.MethodInfo[]",
            "System.Delegate[]" => "Delegate[]",
            "System.Uri[]" => "Uri[]",
            "System.Nullable`1<System.Boolean>[]" => "bool?[]",
            "System.Nullable`1<System.SByte>[]" => "sbyte?[]",
            "System.Nullable`1<System.Byte>[]" => "byte?[]",
            "System.Nullable`1<System.Int16>[]" => "short?[]",
            "System.Nullable`1<System.Int32>[]" => "int?[]",
            "System.Nullable`1<System.Int64>[]" => "long?[]",
            "System.Nullable`1<System.IntPtr>[]" => "IntPtr?[]",
            "System.Nullable`1<System.UInt16>[]" => "ushort?[]",
            "System.Nullable`1<System.UInt32>[]" => "uint?[]",
            "System.Nullable`1<System.UInt64>[]" => "ulong?[]",
            "System.Nullable`1<System.UIntPtr>[]" => "UIntPtr?[]",
            "System.Nullable`1<System.Decimal>[]" => "decimal?[]",
            "System.Nullable`1<System.Single>[]" => "float?[]",
            "System.Nullable`1<System.Double>[]" => "double?[]",
            "System.Nullable`1<System.Char>[]" => "char?[]",
            "System.Nullable`1<System.TimeSpan>[]" => "TimeSpan?[]",
            "System.Nullable`1<System.DateTime>[]" => "DateTime?[]",
            "System.Nullable`1<System.DateTimeOffset>[]" => "DateTimeOffset?[]",
            _ => null
        };
    }

    private static DuckTypeProxyDefinition? GetDuckTypeProxies(TypeDef targetType, Dictionary<TypeDef, DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        if (targetType is null)
        {
            return null;
        }

        if (duckTypeProxyDefinitions.TryGetValue(targetType, out var response))
        {
            return response;
        }

        var proxyProperties = new List<(string ReturnValue, string PropertyName, string TargetName, string TargetFullName, bool Getter, bool Setter, bool IsField)>();

        foreach (var field in targetType.Fields)
        {
            if (field.HasCustomAttributes && field.CustomAttributes.Find(CompilerGeneratedAttributeName) is not null)
            {
                continue;
            }

            var initOnly = (field.Attributes & FieldAttributes.InitOnly) != 0;
            var returnType = GetIfBasicTypeOrDefault(field.FieldType.FullName);
            if (returnType is null)
            {
                if (field.FieldType.IsCorLibType)
                {
                    returnType = field.FieldType.FullName;
                }
                else if (field.ResolveFieldDef()?.FieldType is { } fieldType)
                {
                    if (fieldType.IsCorLibType)
                    {
                        returnType = fieldType.FullName;
                    }
                    else if (fieldType.TryGetTypeDef() is { } fieldTypeDef && fieldTypeDef != targetType)
                    {
                        var proxyDefinition = GetDuckTypeProxies(fieldTypeDef, duckTypeProxyDefinitions);
                        returnType = proxyDefinition?.ProxyName;
                    }
                }
            }

            returnType ??= "object";
            var fieldName = field.Name.String;
            if (fieldName[0] == '_')
            {
                fieldName = char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
            }

            proxyProperties.Add(new(returnType, $"{fieldName}Field", field.Name, field.FieldType.FullName, true, initOnly, true));
        }

        foreach (var property in targetType.Properties)
        {
            if (property.HasCustomAttributes && property.CustomAttributes.Find(CompilerGeneratedAttributeName) is not null)
            {
                continue;
            }

            if (property.Type is PropertySig propertySig)
            {
                var returnType = GetIfBasicTypeOrDefault(propertySig.RetType.ToString());
                if (returnType is null)
                {
                    if (propertySig.RetType.IsCorLibType)
                    {
                        returnType = propertySig.RetType.ToString();
                    }
                    else if (propertySig.RetType.TryGetTypeDef() is { } propertyTypeDef && propertyTypeDef != targetType)
                    {
                        var proxyDefinition = GetDuckTypeProxies(propertyTypeDef, duckTypeProxyDefinitions);
                        returnType = proxyDefinition?.ProxyName;
                    }
                }

                returnType ??= "object";
                proxyProperties.Add(new(returnType, $"{property.Name}", property.Name, propertySig.RetType.ToString(), property.GetMethod is not null, property.SetMethod is not null, false));
            }
        }

        foreach (var method in targetType.Methods)
        {
            if (method.HasCustomAttributes && method.CustomAttributes.Find(CompilerGeneratedAttributeName) is not null)
            {
                continue;
            }

            if ((method.Attributes & MethodAttributes.SpecialName) != 0)
            {
                continue;
            }

            Console.WriteLine(method.ToString());
        }

        if (proxyProperties.Count == 0)
        {
            return null;
        }

        var proxyName = $"I{targetType.Name}Proxy";
        var sb = new StringBuilder();
        sb.Append($@"
/// <summary>
/// DuckTyping interface for {targetType.FullName}
/// </summary>
internal interface {proxyName} : IDuckType
{{");

        foreach (var property in proxyProperties)
        {
            var getterSetter = string.Empty;
            var documentation = string.Empty;
            if (property is { Getter: true, Setter: true })
            {
                documentation = $"Gets or sets a value of {property.TargetFullName}";
                getterSetter = "get; set;";
            }
            else if (property.Getter)
            {
                documentation = $"Gets a value of {property.TargetFullName}";
                getterSetter = "get;";
            }
            else if (property.Setter)
            {
                documentation = $"Sets a value of {property.TargetFullName}";
                getterSetter = "set;";
            }

            sb.AppendLine(string.Empty);
            sb.AppendLine("\t///<summary>");
            sb.AppendLine($"\t/// {documentation.Replace("<", "[").Replace(">", "]").Replace("&", "&amp;")}");
            sb.AppendLine("\t///</summary>");
            if (property.PropertyName != property.TargetName)
            {
                if (property.IsField)
                {
                    sb.AppendLine($"\t[DuckField(Name = \"{property.TargetName}\")]");
                }
                else
                {
                    sb.AppendLine($"\t[Duck(Name = \"{property.TargetName}\")]");
                }
            }

            sb.AppendLine($"\t{property.ReturnValue} {property.PropertyName} {{ {getterSetter} }}");
        }

        if (proxyProperties.Count == 0)
        {
            sb.AppendLine();
        }

        sb.Append($@"}}
");

        var ret = new DuckTypeProxyDefinition(targetType, proxyName, sb.ToString());
        duckTypeProxyDefinitions[targetType] = ret;
        return ret;
    }

    private void InitEditor()
    {
        var subscribeAction = (bool value) => UpdateSourceCode();
        this.WhenAnyValue(o => o.AssemblyPath).Subscribe(_ => UpdateSourceCode());
        this.WhenAnyValue(o => o.SelectedMethod).Subscribe(_ => UpdateSourceCode());
        this.WhenAnyValue(o => o.CreateOnMethodBegin).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodBeginDucktypeInstance).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodBeginDucktypeArguments).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodEnd).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodEndDucktypeInstance).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodEndDucktypeReturnValue).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnAsyncMethodEnd).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnAsyncMethodEndDucktypeInstance).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnAsyncMethodEndDucktypeReturnValue).Subscribe(subscribeAction);
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
            integrationSourceBuilder.Replace("$(Filename)", GetFileName(methodDef));
            integrationSourceBuilder.Replace("$(Namespace)", GetNamespace(methodDef));
            integrationSourceBuilder.Replace("$(FullName)", GetFullName(methodDef));
            integrationSourceBuilder.Replace("$(AssemblyName)", methodDef.DeclaringType.DefinitionAssembly.Name);
            integrationSourceBuilder.Replace("$(TypeName)", methodDef.DeclaringType.Name);
            integrationSourceBuilder.Replace("$(MethodName)", methodDef.Name);
            integrationSourceBuilder.Replace("$(ReturnTypeName)", GetReturnType(methodDef));
            integrationSourceBuilder.Replace("$(ParameterTypeNames)", GetParameterTypeArray(methodDef));
            integrationSourceBuilder.Replace("$(MinimumVersion)", GetMinimumVersion(methodDef));
            integrationSourceBuilder.Replace("$(MaximumVersion)", GetMaximumVersion(methodDef));
            integrationSourceBuilder.Replace("$(IntegrationClassName)", GetIntegrationClassName(methodDef));
            integrationSourceBuilder.Replace("$(IntegrationName)", GetIntegrationName(methodDef));

            var isStatic = methodDef.IsStatic;
            var isVoid = !methodDef.HasReturnType;

            var ducktypeInstance = CreateOnMethodBeginDucktypeInstance || CreateOnMethodEndDucktypeInstance ||
                                   CreateOnAsyncMethodEndDucktypeInstance;

            var duckTypeProxyDefinitions = new Dictionary<TypeDef, DuckTypeProxyDefinition>();
            if (!isStatic && ducktypeInstance)
            {
                GetDuckTypeProxies(methodDef.DeclaringType, duckTypeProxyDefinitions);
            }

            // OnMethodBegin
            integrationSourceBuilder.Replace("$(OnMethodBegin)", CreateOnMethodBegin ? $"{Environment.NewLine}{Environment.NewLine}{GetOnMethodBeginSourceBuilder(isStatic, methodDef, duckTypeProxyDefinitions)}" : string.Empty);

            // OnMethodEnd
            integrationSourceBuilder.Replace("$(OnMethodEnd)", CreateOnMethodEnd ? $"{Environment.NewLine}{Environment.NewLine}{GetOnMethodEndSourceBuilder(isStatic, isVoid, methodDef, duckTypeProxyDefinitions)}" : string.Empty);

            // OnAsyncMethodEnd
            integrationSourceBuilder.Replace("$(OnAsyncMethodEnd)", CreateOnAsyncMethodEnd ? $"{Environment.NewLine}{Environment.NewLine}{GetOnAsyncMethodEndBuilder(isStatic, methodDef, duckTypeProxyDefinitions)}" : string.Empty);

            if (duckTypeProxyDefinitions.Count > 0)
            {
                integrationSourceBuilder.Replace("$(DuckTypeDefinitions)", string.Join(Environment.NewLine, duckTypeProxyDefinitions.Values.Select(v => v.ProxyDefinition)));
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

    private StringBuilder GetOnMethodBeginSourceBuilder(bool isStatic, MethodDef methodDef, Dictionary<TypeDef, DuckTypeProxyDefinition> duckTypeProxyDefinitions)
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

        if (CreateOnMethodBeginDucktypeInstance)
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

            var basicType = GetIfBasicTypeOrDefault(parameter.Type.FullName);
            if (basicType is not null)
            {
                argsParameters.Add($"ref {basicType} {parameterName}");
            }
            else
            {
                argsTypesTypeParamDocumentation.Add($"    /// <typeparam name=\"TArg{i}\">Type of the argument {i} ({parameterTypeCleaned})</typeparam>");
                argsTypes.Add($"TArg{i}");
                argsParameters.Add($"ref TArg{i} {parameterName}");
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

    private StringBuilder GetOnMethodEndSourceBuilder(bool isStatic, bool isVoid, MethodDef methodDef, Dictionary<TypeDef, DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onMethodEndSource = new StringBuilder(isVoid ?
            (isStatic ? ResourceLoader.LoadResource("OnMethodEndVoidStatic.cs") : ResourceLoader.LoadResource("OnMethodEndVoid.cs")) :
            (isStatic ? ResourceLoader.LoadResource("OnMethodEndStatic.cs") : ResourceLoader.LoadResource("OnMethodEnd.cs")));

        var argsConstraint = new List<string>();

        if (CreateOnMethodEndDucktypeInstance)
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

            var basicType = GetIfBasicTypeOrDefault(methodDef.ReturnType.FullName);
            if (basicType is not null)
            {
                returnTypeParameter = basicType;
            }
            else
            {
                returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Type of the return value ({returnTypeCleaned})</typeparam>";
                returnType = ", TReturn";
                returnTypeParameter = "TReturn";
            }

            onMethodEndSource.Replace("$(TReturnTypeParamDocumentation)", returnTypeParamDocumentation);
            onMethodEndSource.Replace("$(TReturnParamDocumentation)", returnParamDocumentation);
            onMethodEndSource.Replace("$(TReturnType)", returnType);
            onMethodEndSource.Replace("$(TReturnTypeParameter)", returnTypeParameter);
        }

        onMethodEndSource.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));

        return onMethodEndSource;
    }

    private StringBuilder GetOnAsyncMethodEndBuilder(bool isStatic, MethodDef methodDef, Dictionary<TypeDef, DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        var onAsyncMethodEndSource = new StringBuilder(isStatic ?
            ResourceLoader.LoadResource("OnAsyncMethodEndStatic.cs") :
            ResourceLoader.LoadResource("OnAsyncMethodEnd.cs"));

        var argsConstraint = new List<string>();

        if (CreateOnAsyncMethodEndDucktypeInstance)
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
        var returnConstraint = string.Empty;

        if (!methodDef.ReturnType.IsGenericInstanceType)
        {
            returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Always NULL: Async type doesn't have generic argument. (Task or ValueTask)</param>";
            returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Dummy object type due original return value doesn't have a generic argument.</typeparam>";
            returnType = ", TReturn";
            returnTypeParameter = "TReturn";
        }
        else if (methodDef.ReturnType.ToGenericInstSig().GenericArguments.Count > 1)
        {
            returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Instance of return type</param>";
            returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Type of the return value</typeparam>";
            returnType = ", TReturn";
            returnTypeParameter = "TReturn";
        }
        else
        {
            var genericReturnValue = methodDef.ReturnType.ToGenericInstSig().GenericArguments[0];
            var returnTypeCleaned = genericReturnValue.FullName.Replace("<", "[").Replace(">", "]").Replace("&", "&amp;");
            returnParamDocumentation = Environment.NewLine + $"    /// <param name=\"returnValue\">Instance of {returnTypeCleaned}</param>";
            var basicType = GetIfBasicTypeOrDefault(genericReturnValue.FullName);
            if (basicType is not null)
            {
                returnTypeParameter = basicType;
            }
            else
            {
                returnTypeParamDocumentation = Environment.NewLine + $"    /// <typeparam name=\"TReturn\">Type of the return value ({returnTypeCleaned})</typeparam>";
                returnType = ", TReturn";
                returnTypeParameter = "TReturn";
            }
        }

        onAsyncMethodEndSource.Replace("$(TReturnTypeParamDocumentation)", returnTypeParamDocumentation);
        onAsyncMethodEndSource.Replace("$(TReturnParamDocumentation)", returnParamDocumentation);
        onAsyncMethodEndSource.Replace("$(TReturnType)", returnType);
        onAsyncMethodEndSource.Replace("$(TReturnTypeParameter)", returnTypeParameter);
        onAsyncMethodEndSource.Replace("$(TArgsConstraint)", argsConstraint.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, argsConstraint));
        return onAsyncMethodEndSource;
    }

    private readonly struct DuckTypeProxyDefinition
    {
        public readonly TypeDef TargetType;
        public readonly string ProxyName;
        public readonly string ProxyDefinition;

        public DuckTypeProxyDefinition(TypeDef targetType, string proxyName, string proxyDefinition)
        {
            TargetType = targetType;
            ProxyName = proxyName;
            ProxyDefinition = proxyDefinition;
        }
    }

    private static class ClrNames
    {
        public const string Void = "System.Void";
        public const string Object = "System.Object";
        public const string Bool = "System.Boolean";
        public const string String = "System.String";

        public const string SByte = "System.SByte";
        public const string Byte = "System.Byte";

        public const string Int16 = "System.Int16";
        public const string Int32 = "System.Int32";
        public const string Int64 = "System.Int64";

        public const string UInt16 = "System.UInt16";
        public const string UInt32 = "System.UInt32";
        public const string UInt64 = "System.UInt64";

        public const string TimeSpan = "System.TimeSpan";

        public const string Stream = "System.IO.Stream";

        public const string Task = "System.Threading.Tasks.Task";
        public const string CancellationToken = "System.Threading.CancellationToken";
        public const string Process = "System.Diagnostics.Process";

        // ReSharper disable once InconsistentNaming
        public const string IAsyncResult = "System.IAsyncResult";
        public const string AsyncCallback = "System.AsyncCallback";

        public const string HttpRequestMessage = "System.Net.Http.HttpRequestMessage";
        public const string HttpResponseMessage = "System.Net.Http.HttpResponseMessage";
        public const string HttpResponseMessageTask = "System.Threading.Tasks.Task`1[System.Net.Http.HttpResponseMessage]";

        public const string GenericTask = "System.Threading.Tasks.Task`1";
        public const string GenericTaskWithGenericClassParameter = "System.Threading.Tasks.Task`1[!0]";
        public const string GenericTaskWithGenericMethodParameter = "System.Threading.Tasks.Task`1[!!0]";
        public const string ObjectTask = "System.Threading.Tasks.Task`1[System.Object]";
        public const string Int32Task = "System.Threading.Tasks.Task`1[System.Int32]";

        public const string Type = "System.Type";

        public const string Activity = "System.Diagnostics.Activity";
        public const string ByteArray = "System.Byte[]";
    }
}
