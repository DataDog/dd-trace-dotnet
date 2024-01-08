// <copyright file="EditorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using dnlib.DotNet;
// ReSharper disable InconsistentNaming

namespace Datadog.AutoInstrumentation.Generator;

internal static class EditorHelper
{
    private static readonly string CompilerGeneratedAttributeName = typeof(CompilerGeneratedAttribute).FullName!;
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public static string GetMethodFullNameForComments(MethodDef methodDef)
    {
        return methodDef.FullName
            .Replace("<", "[")
            .Replace(">", "]");
    }

    public static string GetFileName(MethodDef methodDef)
    {
        var value = methodDef.DeclaringType.Name + "_" + methodDef.Name;
        value = ClearString(value);
        return new string(value.Where(c => !InvalidChars.Contains(c) && c != '.').ToArray());
    }

    public static string GetNamespace(MethodDef methodDef)
    {
        return methodDef.DeclaringType.DefinitionAssembly.Name.Replace(".", "_");
    }

    public static string GetIntegrationClassName(MethodDef methodDef)
    {
        return CleanTypeName(methodDef.DeclaringType.Name + methodDef.Name);
    }

    public static string CleanTypeName(string typeName)
    {
        var value = typeName
                   .Replace("<", string.Empty)
                   .Replace(">", string.Empty)
                   .Replace("/", string.Empty)
                   .Replace(".", string.Empty)
                   .Replace(",", string.Empty)
                   .Replace("`", "Generic")
                   .Replace("|", "_");
        return new string(value.Where(c => !InvalidChars.Contains(c)).ToArray());
    }

    public static string GetIntegrationName(MethodDef methodDef)
    {
        var value = methodDef.DeclaringType.Name.ToString() ?? string.Empty;
        return CleanTypeName(value);
        // value = value.Replace("`", "Generic");
        // return new string(value.Where(c => !InvalidChars.Contains(c)).ToArray());
    }

    public static string GetMinimumVersion(MethodDef methodDef)
    {
        var version = methodDef.DeclaringType.DefinitionAssembly.Version;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public static string GetMaximumVersion(MethodDef methodDef)
    {
        var version = methodDef.DeclaringType.DefinitionAssembly.Version;
        return $"{version.Major}.*.*";
    }

    public static string GetReturnType(MethodDef methodDef)
    {
        return CreateTypeName(methodDef.ReturnType);
    }

    public static string GetParameterTypeArray(MethodDef methodDef)
    {
        var parameters = methodDef.Parameters.Where(p => !p.IsHiddenThisParameter).ToArray();
        if (parameters.Length == 0)
        {
            return "[]";
        }

        var sb = new StringBuilder();
        sb.Append('[')
            .Append(string.Join(", ", parameters.Select(p => CreateTypeName(p.Type))))
            .Append(']');
        return sb.ToString();
    }

    public static string ClearString(string value)
    {
        return value
            .Replace("<", "[")
            .Replace(">", "]")
            .Replace("/", ".")
            .Replace("`", "Generic")
            .Replace("|", "_");
    }

    public static string CreateTypeName(TypeSig typeSig)
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
                    sb.Append(",");
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

    public static string CreateTypeName(string fullTypeName)
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

    public static string? GetIfBasicTypeOrDefault(string fullTypeName)
    {
        if (fullTypeName[fullTypeName.Length - 1] == '&')
        {
            fullTypeName = fullTypeName.Substring(0, fullTypeName.Length - 1);
        }

        return fullTypeName switch
        {
            "System.Void" => "void",
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
            "System.Collections.Generic.Dictionary`2<System.Int32,System.String>" => "Dictionary<int, string>",
            "System.Collections.Generic.Dictionary`2<System.String,System.String>" => "Dictionary<string, string>",
            "System.Collections.Generic.Dictionary`2<System.String,System.String[]>" => "Dictionary<string, string[]>",
            "System.Collections.Generic.Dictionary`2<System.String,System.Object>" => "Dictionary<string, object>",
            "System.Collections.Generic.Dictionary`2<System.Object,System.Object>" => "Dictionary<object, object>",
            "System.Threading.Tasks.Task`1<System.Object>" => "Task<object>",
            "System.Threading.Tasks.Task`1<System.String>" => "Task<string>",
            "System.Threading.Tasks.Task`1<System.Boolean>" => "Task<bool>",
            "System.Threading.Tasks.Task`1<System.SByte>" => "Task<sbyte>",
            "System.Threading.Tasks.Task`1<System.Byte>" => "Task<byte>",
            "System.Threading.Tasks.Task`1<System.Int16>" => "Task<short>",
            "System.Threading.Tasks.Task`1<System.Int32>" => "Task<int>",
            "System.Threading.Tasks.Task`1<System.Int64>" => "Task<long>",
            "System.Threading.Tasks.Task`1<System.IntPtr>" => "Task<IntPtr>",
            "System.Threading.Tasks.Task`1<System.UInt16>" => "Task<ushort>",
            "System.Threading.Tasks.Task`1<System.UInt32>" => "Task<uint>",
            "System.Threading.Tasks.Task`1<System.UInt64>" => "Task<ulong>",
            "System.Threading.Tasks.Task`1<System.UIntPtr>" => "Task<UIntPtr>",
            "System.Threading.Tasks.Task`1<System.Decimal>" => "Task<decimal>",
            "System.Threading.Tasks.Task`1<System.Single>" => "Task<float>",
            "System.Threading.Tasks.Task`1<System.Double>" => "Task<double>",
            "System.Threading.Tasks.Task`1<System.Char>" => "Task<char>",
            "System.Threading.Tasks.Task`1<System.TimeSpan>" => "Task<TimeSpan>",
            "System.Threading.Tasks.Task`1<System.DateTime>" => "Task<DateTime>",
            "System.Threading.Tasks.Task`1<System.DateTimeOffset>" => "Task<DateTimeOffset>",
            "System.Threading.Tasks.Task`1<System.Action>" => "Task<Action>",
            "System.Threading.Tasks.Task`1<System.Object[]>" => "Task<object[]>",
            "System.Threading.Tasks.Task`1<System.String[]>" => "Task<string[]>",
            "System.Threading.Tasks.Task`1<System.Boolean[]>" => "Task<bool[]>",
            "System.Threading.Tasks.Task`1<System.SByte[]>" => "Task<sbyte[]>",
            "System.Threading.Tasks.Task`1<System.Byte[]>" => "Task<byte[]>",
            "System.Threading.Tasks.Task`1<System.Int16[]>" => "Task<short[]>",
            "System.Threading.Tasks.Task`1<System.Int32[]>" => "Task<int[]>",
            "System.Threading.Tasks.Task`1<System.Int64[]>" => "Task<long[]>",
            "System.Threading.Tasks.Task`1<System.IntPtr[]>" => "Task<IntPtr[]>",
            "System.Threading.Tasks.Task`1<System.UInt16[]>" => "Task<ushort[]>",
            "System.Threading.Tasks.Task`1<System.UInt32[]>" => "Task<uint[]>",
            "System.Threading.Tasks.Task`1<System.UInt64[]>" => "Task<ulong[]>",
            "System.Threading.Tasks.Task`1<System.UIntPtr[]>" => "Task<UIntPtr[]>",
            "System.Threading.Tasks.Task`1<System.Decimal[]>" => "Task<decimal[]>",
            "System.Threading.Tasks.Task`1<System.Single[]>" => "Task<float[]>",
            "System.Threading.Tasks.Task`1<System.Double[]>" => "Task<double[]>",
            "System.Threading.Tasks.Task`1<System.Char[]>" => "Task<char[]>",
            "System.Threading.Tasks.Task`1<System.TimeSpan[]>" => "Task<TimeSpan[]>",
            "System.Threading.Tasks.Task`1<System.DateTime[]>" => "Task<DateTime[]>",
            "System.Threading.Tasks.Task`1<System.DateTimeOffset[]>" => "Task<DateTimeOffset[]>",
            "System.Threading.Tasks.Task`1<System.Action[]>" => "Task<Action[]>",
            "System.Net.Http.HttpClient" => "System.Net.Http.HttpClient",
            _ => null
        };
    }

    public static Dictionary<TypeDef, DuckTypeProxyDefinition> GetDuckTypeProxies(TypeDef targetType, bool includeFields, bool includeProperties, bool includeMethods, bool includeDuckChaining, bool useDuckCopyStruct)
    {
        var duckTypeProxyDefinitions = new Dictionary<TypeDef, DuckTypeProxyDefinition>();
        GetDuckTypeProxies(targetType, includeFields, includeProperties, includeMethods, includeDuckChaining, useDuckCopyStruct, duckTypeProxyDefinitions);
        return duckTypeProxyDefinitions;
    }

    public static DuckTypeProxyDefinition? GetDuckTypeProxies(
        TypeDef targetType,
        bool includeFields,
        bool includeProperties,
        bool includeMethods,
        bool includeDuckChaining,
        bool useDuckCopyStruct,
        Dictionary<TypeDef, DuckTypeProxyDefinition> duckTypeProxyDefinitions,
        bool generateDuckCopyStruct = false)
    {
        if (targetType is null)
        {
            return null;
        }

        if (duckTypeProxyDefinitions.TryGetValue(targetType, out var response))
        {
            return response;
        }

        generateDuckCopyStruct = useDuckCopyStruct && generateDuckCopyStruct && !includeMethods;

        var proxyName = generateDuckCopyStruct ? $"DuckType{CleanTypeName(targetType.Name)}Proxy" : $"I{CleanTypeName(targetType.Name)}Proxy";
        duckTypeProxyDefinitions[targetType] = new DuckTypeProxyDefinition(targetType, proxyName, null);

        var proxyProperties = new List<(string ReturnValue, string PropertyName, string TargetName, string TargetFullName, bool Getter, bool Setter, bool IsField)>();
        var proxyMethods = new List<(string ReturnType, string MethodName, string MethodTargetName, string MethodTargetFullName, List<string> ArgumentNames, List<string> ArgumentTypeNames, List<string>? ArgumentTargetTypeNames)>();

        string GetTypeName(TypeSig typeSig)
        {
            var typeValue = typeSig.ToString() ?? string.Empty;
            var type = GetIfBasicTypeOrDefault(typeValue);
            if (type is null)
            {
                if (typeSig.IsCorLibType)
                {
                    type = typeValue;
                }
                else if (includeDuckChaining && typeSig.TryGetTypeDef() is { } typeDef && typeDef != targetType)
                {
                    var proxyDefinition = GetDuckTypeProxies(typeDef, includeFields, includeProperties, includeMethods, includeDuckChaining, useDuckCopyStruct, duckTypeProxyDefinitions, true);
                    type = proxyDefinition?.ProxyName;
                }
            }

            return type ?? "object";
        }

        if (includeFields)
        {
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
                        returnType = GetTypeName(fieldType);
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
        }

        if (includeProperties)
        {
            foreach (var property in targetType.Properties)
            {
                if (property.HasCustomAttributes && property.CustomAttributes.Find(CompilerGeneratedAttributeName) is not null)
                {
                    continue;
                }

                if (property.Type is PropertySig propertySig)
                {
                    var returnType = GetTypeName(propertySig.RetType);
                    var propertyName = property.Name.String;
                    if (propertyName.IndexOf('.') != -1)
                    {
                        propertyName = CleanTypeName(propertyName);
                    }

                    proxyProperties.Add(new(returnType, propertyName, property.Name, propertySig.RetType.FullName, property.GetMethod is not null, property.SetMethod is not null, false));
                }
            }
        }

        if (includeMethods)
        {
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

                var retTypeValue = GetTypeName(method.ReturnType);
                var methodName = method.Name.String;
                if (methodName.IndexOf('.') != -1)
                {
                    methodName = CleanTypeName(methodName);
                }

                var lstArgumentsNames = new List<string>();
                var lstArgumentsTypeNames = new List<string>();
                List<string>? lstArgumentsTargetTypeNames = null;
                var idx = 0;
                var numberOfSimilarMethods = targetType.Methods.Count(m => m != method && m.Name == method.Name && m.Parameters.Count == method.Parameters.Count);
                if (numberOfSimilarMethods > 0)
                {
                    lstArgumentsTargetTypeNames = new();
                }

                var allArgumentTypesAreFromBCL = true;
                foreach (var param in method.Parameters)
                {
                    if (param.IsHiddenThisParameter || param.IsReturnTypeParameter)
                    {
                        continue;
                    }

                    var paramTypeName = GetTypeName(param.Type);
                    lstArgumentsNames.Add(param.Name ?? $"arg{idx}");
                    lstArgumentsTypeNames.Add(paramTypeName);
                    lstArgumentsTargetTypeNames?.Add(param.Type.FullName);
                    allArgumentTypesAreFromBCL = allArgumentTypesAreFromBCL && (param.Type.IsCorLibType || param.Type.DefinitionAssembly?.Name == "System.Runtime");
                    idx++;
                }

                if (allArgumentTypesAreFromBCL)
                {
                    lstArgumentsTargetTypeNames?.Clear();
                }

                proxyMethods.Add(new(retTypeValue, methodName, method.Name, method.FullName, lstArgumentsNames, lstArgumentsTypeNames, lstArgumentsTargetTypeNames));
            }
        }

        if (proxyProperties.Count == 0 && proxyMethods.Count == 0)
        {
            duckTypeProxyDefinitions.Remove(targetType);
            return null;
        }

        // Create Proxy
        var sb = new StringBuilder();
        if (!generateDuckCopyStruct)
        {
            sb.Append(
                $@"
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

            foreach (var method in proxyMethods)
            {
                var documentation = $"Calls method: {method.MethodTargetFullName}";
                sb.AppendLine(string.Empty);
                sb.AppendLine("\t///<summary>");
                sb.AppendLine($"\t/// {documentation.Replace("<", "[").Replace(">", "]").Replace("&", "&amp;")}");
                sb.AppendLine("\t///</summary>");
                var targetParameters = method.ArgumentTargetTypeNames is not null ? string.Join(", ", method.ArgumentTargetTypeNames.Select(i => "\"" + i + "\"")) : null;
                if (method.MethodName != method.MethodTargetName)
                {
                    if (string.IsNullOrEmpty(targetParameters))
                    {
                        sb.AppendLine($"\t[Duck(Name = \"{method.MethodTargetName}\")]");
                    }
                    else
                    {
                        sb.AppendLine($"\t[Duck(Name = \"{method.MethodTargetName}\", ParameterTypeNames = new string[] {{ {targetParameters} }})]");
                    }
                }
                else if (!string.IsNullOrEmpty(targetParameters))
                {
                    sb.AppendLine($"\t[Duck(ParameterTypeNames = new string[] {{ {targetParameters} }})]");
                }

                var parameters = string.Join(", ", method.ArgumentTypeNames.Zip(method.ArgumentNames, (a, b) => $"{a} {b}"));
                sb.AppendLine($"\t{method.ReturnType} {method.MethodName}({parameters});");
            }

            if (proxyProperties.Count == 0 && proxyMethods.Count == 0)
            {
                sb.AppendLine();
            }

            sb.Append($@"}}");
        }
        else
        {
            sb.Append(
                $@"
/// <summary>
/// DuckTyping struct for {targetType.FullName}
/// </summary>
[DuckCopy]
internal struct {proxyName}
{{");
            foreach (var property in proxyProperties)
            {
                if (!property.Getter)
                {
                    continue;
                }

                var documentation = $"Gets a value of {property.TargetFullName}";
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

                sb.AppendLine($"\t{property.ReturnValue} {property.PropertyName};");
            }

            if (proxyProperties.Count == 0)
            {
                sb.AppendLine();
            }

            sb.Append($@"}}");
        }

        var ret = new DuckTypeProxyDefinition(targetType, proxyName, sb.ToString());
        duckTypeProxyDefinitions[targetType] = ret;
        return ret;
    }

    public readonly struct DuckTypeProxyDefinition
    {
        public readonly TypeDef TargetType;
        public readonly string ProxyName;
        public readonly string? ProxyDefinition;

        public DuckTypeProxyDefinition(TypeDef targetType, string proxyName, string? proxyDefinition)
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
