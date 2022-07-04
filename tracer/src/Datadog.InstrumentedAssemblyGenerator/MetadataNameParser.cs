using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
#pragma warning disable CS1570

namespace Datadog.InstrumentedAssemblyGenerator;

/// <summary>
/// Parse a metadata member name, from a runtime module or from instrumentation logs (*.moduleMembers or *.instrlog) to a MetadataMember object
/// Name can be in one of two forms:
/// 1. a regular metadata name as known from reflection, dnlib, cecil etc.
/// 2. a name given by the instrumentation generator native side under clr profiler
/// For the first form, we can use a simple name and then find the type in the loaded module
/// For the second form, we need a full signature representation to construct the type from scratch and add it to module metadata
/// </summary>
internal class MetadataNameParser
{
    internal static MetadataMember Parse(Token token, string name)
    {
        switch (token.Table)
        {
            case MetadataTable.MethodSpec:
            {
                var parsedName = ParseName(name);
                if (parsedName.HasValue == false)
                {
                    throw new TypeNameParserException(name);
                }

                var (method, methodGenericArgs) = ParseGenericTypeOrMethod(parsedName.Value.member);
                var (type, typeGenericArgs) = ParseGenericTypeOrMethod(parsedName.Value.type);
                if (methodGenericArgs.Length <= 0 && name.Contains("<"))
                {
                    Logger.Warn($"{name}: It seems that the method should be generic but failed to parse it");
                    throw new TypeNameParserException(name);
                }
                SigMemberType[] typeGenericArgsSig = Array.Empty<SigMemberType>();
                SigMemberType[] methodGenericArgsSig = Array.Empty<SigMemberType>();
                SigMemberType[] methodParametersSig = Array.Empty<SigMemberType>();
                SigMemberType returnTypeSig = null;

                if (typeGenericArgs.Length > 0)
                {
                    typeGenericArgsSig = typeGenericArgs.Select(arg => new SigMemberType(arg)).ToArray();
                }

                if (methodGenericArgs.Length > 0)
                {
                    methodGenericArgsSig = methodGenericArgs.Select(arg => new SigMemberType(arg)).ToArray();
                }

                if (!string.IsNullOrEmpty(parsedName.Value.returnType))
                {
                    returnTypeSig = new SigMemberType(parsedName.Value.returnType);
                }

                if (parsedName.Value.parameters.Length > 0)
                {
                    methodParametersSig = parsedName.Value.parameters.Select(p => new SigMemberType(p)).ToArray();
                }
                return new MetadataMember(type ?? parsedName.Value.type, method ?? parsedName.Value.member, name, methodParametersSig, null, typeGenericArgsSig, methodGenericArgsSig, returnTypeSig, 0);
            }

            case MetadataTable.TypeSpec:
            {
                var (type, typeGenericArgs) = ParseGenericTypeOrMethod(name);
                if (typeGenericArgs.Length <= 0 && name.Contains("<"))
                {
                    Logger.Warn($"{name}: It seems that the ype should be generic but failed to parse it");
                    throw new TypeNameParserException(name);
                }
                var typeGenericArgsSig = typeGenericArgs.Select(arg => new SigMemberType(arg)).ToArray();
                return new MetadataMember(type, fullName: name, Array.Empty<SigMemberType>(), typeGenericArgsSig);
            }

            case MetadataTable.TypeRef:
            case MetadataTable.TypeDef:
            case MetadataTable.NestedClass:
            case MetadataTable.GenericParam:
            case MetadataTable.ExportedType:
                return new MetadataMember(name, name);

            case MetadataTable.Field:
            case MetadataTable.Method:
            case MetadataTable.MemberRef:
            {
                var parsedName = ParseName(name);
                if (!parsedName.HasValue)
                {
                    throw new TypeNameParserException(name);
                }

                var (type, typeGenericArgs) = ParseGenericTypeOrMethod(parsedName.Value.type);
                var (method, methodGenericArgs) = ParseGenericTypeOrMethod(parsedName.Value.member);
                SigMemberType typeSig = null;
                SigMemberType returnTypeSig = null;
                SigMemberType[] typeGenericArgsSig = Array.Empty<SigMemberType>();
                SigMemberType[] methodGenericArgsSig = Array.Empty<SigMemberType>();
                SigMemberType[] methodParametersSig = Array.Empty<SigMemberType>();
                if (typeGenericArgs.Length > 0)
                {
                    typeGenericArgsSig = typeGenericArgs.Select(arg => new SigMemberType(arg)).ToArray();
                    // if type is signature from the instrumentation log
                    if (parsedName.Value.type.Contains("?"))
                    {
                        // Replace generic sign '<>' with '?'
                        // In this case is just one type so take the first (locals can have more than one)
                        string sanitized = SanitizeGenericSig(parsedName.Value.type).First();
                        typeSig = new SigMemberType(sanitized);
                    }
                }

                typeSig ??= new SigMemberType(parsedName.Value.type);

                if (methodGenericArgs.Length > 0)
                {
                    methodGenericArgsSig = methodGenericArgs.Select(arg => new SigMemberType(arg)).ToArray();
                }

                int methodOrFieldAttr = 0;
                if (!string.IsNullOrEmpty(parsedName.Value.returnType))
                {
                    returnTypeSig = new SigMemberType(parsedName.Value.returnType);
                }
                else if (token.Table == MetadataTable.Field)
                {
                    string field = method ?? parsedName.Value.member;
                    if (field.Contains("?"))
                    {
                        string[] parts = field.Split('?');
                        method = parts[0];
                        returnTypeSig = new SigMemberType(parts[1]);
                        methodOrFieldAttr = Convert.ToInt32(parts[2], 16);
                    }
                }

                if (parsedName.Value.parameters.Length > 0)
                {
                    methodParametersSig = parsedName.Value.parameters.Select(p => new SigMemberType(p)).ToArray();
                }

                return new MetadataMember(type ?? parsedName.Value.type, method ?? parsedName.Value.member, name, methodParametersSig, typeSig, typeGenericArgsSig, methodGenericArgsSig, returnTypeSig, methodOrFieldAttr);
            }

            case MetadataTable.AssemblyRef:
            case MetadataTable.ModuleRef:
                return new MetadataMember("", name);
            case MetadataTable.UserString:
                return new MetadataMember("System.String", name);
            default:
                throw new NotImplementedException($"Token {token}, Type or method {name}: will not map");
        }
    }

    /// <summary>
    /// Take a long name of member (type, method, field) parse it, and return the relevant parts (i.e return type is just for method)
    /// </summary>
    /// <param name="name">The full name that came form native log or from module metadata</param>
    private static (string returnType, string type, string member, string[] parameters)? ParseName(string name)
    {
        // types can have both separator chars, depend on the type system they use, so search for both of them
        char[] separators = { ':', '.' };
        string fullMethodName = name;

        foreach (char separator in separators)
        {
            int parametersIndex = GetStartOfParametersIndex(name);
            if (parametersIndex > -1)
            {
                fullMethodName = name.Substring(0, parametersIndex);
            }

            int lastSeparator = GetStartOfMethodNameIndex(fullMethodName, separator);

            if (lastSeparator < 0)
            {
                continue;
            }

            string member = fullMethodName.Substring(lastSeparator + 1);
            string returnAndType = fullMethodName.Substring(0, lastSeparator);
            string type;
            string returnType = null;
            int returnTypeIndex = returnAndType.IndexOf(' ');
            if (returnTypeIndex >= 0)
            {
                returnType = returnAndType.Substring(0, returnTypeIndex);
                type = returnAndType.Substring(returnTypeIndex + 1);
            }
            else
            {
                returnTypeIndex = returnAndType.IndexOf('?');
                int startOfGenericIndex = returnAndType.IndexOf('<');
                while (returnTypeIndex >= 0 && (returnTypeIndex < startOfGenericIndex || startOfGenericIndex < 0))
                {
                    returnType += returnAndType.Substring(0, returnTypeIndex);
                    returnAndType = returnAndType.Substring(returnTypeIndex + 1);
                    returnTypeIndex = returnAndType.IndexOf('?');
                    startOfGenericIndex = returnAndType.IndexOf('<');
                }

                type = returnAndType;
            }

            int trailing = type.IndexOf(':');
            if (trailing >= 0)
            {
                type = type.Substring(0, trailing);
            }

            string[] parameters = parametersIndex < 0 || name[parametersIndex + 1] == ')' ?
                                      Array.Empty<string>() :
                                      SplitArgumentsOrLocals(name.Substring(parametersIndex + 1, name.Length - parametersIndex - 2));
            return (returnType, type, member, parameters);
        }

        return null;
    }

    private static int GetStartOfMethodNameIndex(string fullMethodName, char separator)
    {
        int lastSeparator = -1;
        bool isGenericMethod = fullMethodName.EndsWith(">");
        if (!isGenericMethod)
        {
            lastSeparator = fullMethodName.LastIndexOf(separator);
        }
        else
        {
            // if it's generic we don't want to consider al generic characters when searching for last separator
            int numberOfOpens = 1;
            for (int i = fullMethodName.Length - 2; i >= fullMethodName.IndexOf('<'); i--)
            {
                if (fullMethodName[i] == '>')
                {
                    numberOfOpens++;
                    continue;
                }

                if (fullMethodName[i] == '<')
                {
                    numberOfOpens--;
                    if (numberOfOpens == 0)
                    {
                        int genericMethodOpenIndex = i;
                        lastSeparator = fullMethodName.Substring(0, genericMethodOpenIndex).LastIndexOf(separator);
                        break;
                    }
                }
            }
        }

        return lastSeparator;
    }

    private static int GetStartOfParametersIndex(string name)
    {
        int parametersIndex = -1;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c != '(')
            {
                continue;
            }

            parametersIndex = i;
            string methodName = name.Substring(0, parametersIndex);
            if (methodName.EndsWith("modreq") || methodName.EndsWith("modopt"))
            {
                continue;
            }

            break;
        }

        return parametersIndex;
    }

    private static (string, string[]) ParseGenericTypeOrMethod(string name)
    {
        int startOfGenericParams = GetStartOfGenericIndex(name);
        if (startOfGenericParams < 0)
        {
            return (name, Array.Empty<string>());
        }

        int closedGenericIndex = name.Substring(startOfGenericParams).LastIndexOf('>') + startOfGenericParams;
        string[] genericParamsArray = GetGenericParamsFromMethodOrTypeName(name, startOfGenericParams, closedGenericIndex);
        string typeOrMethod = name.Substring(0, startOfGenericParams);
        int endSigIndex = typeOrMethod.LastIndexOf("?", StringComparison.InvariantCulture);
        if (endSigIndex > 0)
        {
            typeOrMethod = typeOrMethod.Substring(endSigIndex + 1);
        }
        return (typeOrMethod, genericParamsArray);
    }

    /// <summary>
    /// It's a method or type name, so the last '>' sign is the absolute end of generics,
    /// so count from there to the beginning and match the count
    /// so then we will not confuse with other '>' or '<' that are not part of the generics signature
    /// </summary>
    /// <param name="name">method or type name</param>
    /// <returns>index of the open generic sign of the method</returns>
    internal static int GetStartOfGenericIndex(string name)
    {
        int startOfGeneric = -1;
        int numberOfGenerics = 0;
        int lastIndexOfClosedGenerics = name.LastIndexOf('>');
        if (lastIndexOfClosedGenerics < 0)
        {
            return lastIndexOfClosedGenerics;
        }

        numberOfGenerics++;
        for (int i = lastIndexOfClosedGenerics - 1; i > 0; i--)
        {
            if (name[i] == '<' && name[i + 1] != '>' &&
                name[i - 1] != '/' && name[i - 1] != '\\')
            {
                startOfGeneric = i;
                numberOfGenerics--;
                if (numberOfGenerics == 0)
                {
                    break;
                }
            }
            else if (name[i] == '>' && name[i - 1] != '<')
            {
                numberOfGenerics++;
            }
        }

        return startOfGeneric;
    }

    internal static string[] GetGenericParamsFromMethodOrTypeName(string name, int startOfGenericIndex, int closedGenericIndex)
    {
        char c = name[closedGenericIndex];
        while (c == '>')
        {
            closedGenericIndex--;
            c = name[closedGenericIndex];
        }

        string genericParams = name.Substring(startOfGenericIndex + 1, closedGenericIndex - startOfGenericIndex);
        return SplitArgumentsOrLocals(genericParams);
    }

    internal static string[] SplitArgumentsOrLocals(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            return Array.Empty<string>();
        }

        var parameterList = new List<string>();
        int insideGenerics = 0;
        int newParameterIndex = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            char c = parameters[i];
            if (c == '<')
            {
                insideGenerics++;
            }
            else if (c == '>')
            {
                insideGenerics--;
            }

            else if (c == ',' && insideGenerics == 0)
            {
                parameterList.Add(parameters.Substring(newParameterIndex, i - newParameterIndex));
                newParameterIndex = i + 1;
            }
        }

        parameterList.Add(parameters.Substring(newParameterIndex, parameters.Length - newParameterIndex));
        return parameterList.ToArray();
    }

    internal static string[] SanitizeGenericSig(string sig)
    {
        // We write generic locals as regular with '<>' but we want them here with '?'
        string[] sigToReturn = MetadataNameParser.SplitArgumentsOrLocals(sig);
        for (int index = 0; index < sigToReturn.Length; index++)
        {
            sigToReturn[index] = sigToReturn[index].Replace('<', '?');
            sigToReturn[index] = sigToReturn[index].Replace('>', '?');
        }

        return sigToReturn;
    }
}
