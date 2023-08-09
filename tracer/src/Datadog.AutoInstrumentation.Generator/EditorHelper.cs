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
        return CleanTypeName(methodDef.DeclaringType.Name + "_" + methodDef.Name);
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
            return "new string[0]";
        }

        var sb = new StringBuilder();
        sb.Append("new[] { ")
            .Append(string.Join(", ", parameters.Select(p => CreateTypeName(p.Type))))
            .Append(" }");
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

    public static Dictionary<TypeDef, DuckTypeProxyDefinition> GetDuckTypeProxies(TypeDef targetType, bool includeFields, bool includeProperties, bool includeMethods, bool includeDuckChaining)
    {
        var duckTypeProxyDefinitions = new Dictionary<TypeDef, DuckTypeProxyDefinition>();
        GetDuckTypeProxies(targetType, includeFields, includeProperties, includeMethods, includeDuckChaining, duckTypeProxyDefinitions);
        return duckTypeProxyDefinitions;
    }

    public static DuckTypeProxyDefinition? GetDuckTypeProxies(TypeDef targetType, bool includeFields, bool includeProperties, bool includeMethods, bool includeDuckChaining, Dictionary<TypeDef, DuckTypeProxyDefinition> duckTypeProxyDefinitions)
    {
        if (targetType is null)
        {
            return null;
        }

        if (duckTypeProxyDefinitions.TryGetValue(targetType, out var response))
        {
            return response;
        }

        var proxyName = $"I{CleanTypeName(targetType.Name)}Proxy";
        duckTypeProxyDefinitions[targetType] = new DuckTypeProxyDefinition(targetType, proxyName, null);

        var proxyMembers = new List<(string ReturnValue, string PropertyName, string TargetName, string TargetFullName, bool Getter, bool Setter, bool IsField)>();

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
                        if (fieldType.IsCorLibType)
                        {
                            returnType = fieldType.FullName;
                        }
                        else if (includeDuckChaining && fieldType.TryGetTypeDef() is { } fieldTypeDef && fieldTypeDef != targetType)
                        {
                            var proxyDefinition = GetDuckTypeProxies(fieldTypeDef, includeFields, includeProperties, includeMethods, includeDuckChaining, duckTypeProxyDefinitions);
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

                proxyMembers.Add(new(returnType, $"{fieldName}Field", field.Name, field.FieldType.FullName, true, initOnly, true));
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
                    var retTypeValue = propertySig.RetType.ToString() ?? string.Empty;
                    var returnType = GetIfBasicTypeOrDefault(retTypeValue);
                    if (returnType is null)
                    {
                        if (propertySig.RetType.IsCorLibType)
                        {
                            returnType = retTypeValue;
                        }
                        else if (includeDuckChaining && propertySig.RetType.TryGetTypeDef() is { } propertyTypeDef && propertyTypeDef != targetType)
                        {
                            var proxyDefinition = GetDuckTypeProxies(propertyTypeDef, includeFields, includeProperties, includeMethods, includeDuckChaining, duckTypeProxyDefinitions);
                            returnType = proxyDefinition?.ProxyName;
                        }
                    }

                    returnType ??= "object";
                    var propertyName = property.Name.String;
                    if (propertyName.IndexOf('.') != -1)
                    {
                        propertyName = CleanTypeName(propertyName);
                    }

                    proxyMembers.Add(new(returnType, propertyName, property.Name, retTypeValue, property.GetMethod is not null, property.SetMethod is not null, false));
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

                Console.WriteLine(method.ToString());
            }
        }

        if (proxyMembers.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.Append($@"
/// <summary>
/// DuckTyping interface for {targetType.FullName}
/// </summary>
internal interface {proxyName} : IDuckType
{{");

        foreach (var property in proxyMembers)
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

        if (proxyMembers.Count == 0)
        {
            sb.AppendLine();
        }

        sb.Append($@"}}");

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
