// <copyright file="MethodMatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Util;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

internal static class MethodMatcher
{
    private static readonly ConcurrentDictionary<MethodBase, MethodBase?> AsyncMethodCache = new();

    /// <summary>
    /// Attempts to match a method name from a stack trace with a MethodBase,
    /// handling special cases like async methods, iterators, and nested methods.
    /// </summary>
    internal static bool IsMethodMatch(string stackTraceMethodText, MethodBase methodBase)
    {
        if (methodBase == null || string.IsNullOrWhiteSpace(stackTraceMethodText))
        {
            return false;
        }

        // First check if this is a state machine
        var isStateMachine = methodBase.Name == "MoveNext" &&
                             methodBase.DeclaringType != null &&
                             (typeof(IAsyncStateMachine).IsAssignableFrom(methodBase.DeclaringType) ||
                              typeof(IEnumerator).IsAssignableFrom(methodBase.DeclaringType));

        // Normalize and parse both representations
        var normalizedStackTrace = NormalizeMethodText(stackTraceMethodText);
        var stackTraceParts = ParseMethodName(normalizedStackTrace);
        var methodBaseParts = ParseMethodName(methodBase.DeclaringType?.FullName + "." + methodBase.Name);

        if (stackTraceParts == null || methodBaseParts == null)
        {
            return normalizedStackTrace.Equals(methodBase.Name, StringComparison.Ordinal);
        }

        return DoMethodPartsMatch(stackTraceParts.Value, methodBaseParts.Value, isStateMachine);
    }

    private static string NormalizeMethodText(string methodText)
    {
        // Handle .NET Core state machine format: Method()+MoveNext()
        if (methodText.Contains("()+MoveNext()"))
        {
            var index = methodText.IndexOf("()+MoveNext()");
            if (index > 0)
            {
                return methodText.Substring(0, index);
            }
        }

        var parenthesesIndex = methodText.IndexOf('(');
        if (parenthesesIndex > 0)
        {
            methodText = methodText.Substring(0, parenthesesIndex);
        }

        var genericIndex = methodText.IndexOf('[');
        if (genericIndex <= 0)
        {
            return methodText;
        }

        var closeIndex = methodText.IndexOf(']', genericIndex);
        if (closeIndex <= genericIndex)
        {
            return methodText;
        }

        // Count type parameters without string splits
        var typeParams = 1;
        for (int i = genericIndex + 1; i < closeIndex; i++)
        {
            if (methodText[i] == ',')
            {
                typeParams++;
            }
        }

        var sb = StringBuilderCache.Acquire();
        sb.Append(methodText, 0, genericIndex);
        sb.Append('`');
        sb.Append(typeParams);

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    private static string NormalizeMethodName(string methodName)
    {
        var backtickIndex = methodName.IndexOf('`');
        return backtickIndex > 0 ? methodName.Substring(0, backtickIndex) : methodName;
    }

    private static bool IsAsyncStateMachine(MethodBase method)
    {
        if (method.Name != "MoveNext")
        {
            return false;
        }

        var declaringType = method.DeclaringType;
        if (declaringType == null)
        {
            return false;
        }

        var isAsyncStateMachine = declaringType.GetInterfaces()
                                             .Any(i => i.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine");
        if (!isAsyncStateMachine)
        {
            return false;
        }

        // For async lambdas, the pattern is different (ends with ">d")
        var typeName = declaringType.Name;
        var isAsyncLambda = typeName.Contains("b__") && typeName.EndsWith(">d");

        // Only return true for regular async methods (not lambdas)
        return !isAsyncLambda && typeName.Contains(">d__");
    }

    private static bool IsAsyncLambdaStateMachine(MethodBase method)
    {
        if (method.Name != "MoveNext")
        {
            return false;
        }

        var declaringType = method.DeclaringType;
        if (declaringType == null)
        {
            return false;
        }

        // Check for the async lambda pattern (double angle brackets and b__)
        var typeName = declaringType.Name;
        return typeName.Contains("<<") && typeName.Contains("b__") && typeName.EndsWith(">d");
    }

    private static MethodBase? ResolveSpecialMethod(MethodBase method)
    {
        // Check cache first
        if (AsyncMethodCache.TryGetValue(method, out var cachedMethod))
        {
            return cachedMethod;
        }

        MethodBase? resolvedMethod = null;

        // Important: Check for async lambda first
        if (IsAsyncLambdaStateMachine(method))
        {
            // For async lambdas, we don't resolve - we'll handle them in the name matching
            resolvedMethod = null;
        }
        else if (IsAsyncStateMachine(method))
        {
            resolvedMethod = ResolveAsyncMethod(method);
        }
        else if (IsIteratorStateMachine(method))
        {
            resolvedMethod = ResolveIteratorMethod(method);
        }
        else if (IsLocalFunction(method))
        {
            resolvedMethod = ResolveLocalFunction(method);
        }

        // Cache the result (null is a valid cache result)
        AsyncMethodCache.TryAdd(method, resolvedMethod);
        return resolvedMethod;
    }

    private static bool AreMethodNamesEquivalent(string stackTraceMethod, string methodBaseMethod)
    {
        // If they're exactly equal, we're done
        if (stackTraceMethod.Equals(methodBaseMethod, StringComparison.Ordinal))
        {
            return true;
        }

        // For state machine methods (iterators and async methods)
        if (stackTraceMethod == "MoveNext")
        {
            return true;
        }

        return false;
    }

    private static string ExtractLambdaIdentifier(string methodName)
    {
        // Look for the b__ pattern that identifies lambda methods
        var lambdaMarkerIndex = methodName.IndexOf("b__", StringComparison.Ordinal);
        if (lambdaMarkerIndex < 0)
        {
            return string.Empty;
        }

        // Extract everything between < and > or from b__ to the next separator
        var startIndex = methodName.LastIndexOf('<', lambdaMarkerIndex);
        startIndex = startIndex >= 0 ? startIndex + 1 : lambdaMarkerIndex;

        var endIndex = methodName.IndexOfAny(new[] { '>', '(', '.', 'd' }, lambdaMarkerIndex);
        if (endIndex < 0)
        {
            endIndex = methodName.Length;
        }

        var identifier = methodName.Substring(startIndex, endIndex - startIndex);

        // If we got the identifier with angle brackets, clean it up
        if (identifier.StartsWith("<") && identifier.EndsWith(">"))
        {
            identifier = identifier.Substring(1, identifier.Length - 2);
        }

        return identifier;
    }

    private static bool IsIteratorStateMachine(MethodBase method)
    {
        if (method.Name != "MoveNext")
        {
            return false;
        }

        var declaringType = method.DeclaringType;
        if (declaringType == null)
        {
            return false;
        }

        return declaringType.GetInterfaces()
                          .Any(i => i.FullName == "System.Collections.IEnumerator" ||
                                  (i.FullName?.StartsWith("System.Collections.Generic.IEnumerator`") ?? false));
    }

    private static bool IsLocalFunction(MethodBase method)
    {
        // Local functions have a generated name starting with "<"
        return method.Name.StartsWith("<") && method.Name.Contains(">");
    }

    private static MethodBase? ResolveAsyncMethod(MethodBase moveNextMethod)
    {
        var stateMachineType = moveNextMethod.DeclaringType;
        if (stateMachineType == null)
        {
            return null;
        }

        // Handle nested types and generic type parameters
        var containingType = ResolveContainingType(stateMachineType);
        if (containingType == null)
        {
            return null;
        }

        var originalMethodName = ExtractMethodNameFromStateMachine(stateMachineType.Name);
        if (originalMethodName == null)
        {
            return null;
        }

        // Find all methods with matching name
        var candidates = containingType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Static | BindingFlags.Instance |
                                                 BindingFlags.DeclaredOnly)
                                     .Where(m => m.Name == originalMethodName)
                                     .ToList();

        // If we have multiple candidates, try to match by async method builder attribute
        if (candidates.Count > 1)
        {
            var matchingMethod = candidates.FirstOrDefault(HasMatchingAsyncMethodBuilder);
            if (matchingMethod != null)
            {
                return matchingMethod;
            }
        }

        return candidates.FirstOrDefault();
    }

    private static Type? ResolveContainingType(Type stateMachineType)
    {
        var containingType = stateMachineType.DeclaringType;
        if (containingType == null)
        {
            return null;
        }

        // Handle nested generic types
        if (containingType.IsGenericType && !containingType.IsGenericTypeDefinition)
        {
            // Get the generic type definition
            containingType = containingType.GetGenericTypeDefinition();
        }

        return containingType;
    }

    private static string? ExtractMethodNameFromStateMachine(string typeName)
    {
        var methodNameStart = typeName.IndexOf('<');
        var methodNameEnd = typeName.IndexOf('>');

        if (methodNameStart < 0 || methodNameEnd <= methodNameStart)
        {
            return null;
        }

        return typeName.Substring(methodNameStart + 1, methodNameEnd - methodNameStart - 1);
    }

    private static bool HasMatchingAsyncMethodBuilder(MethodInfo method)
    {
        return method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.EndsWith("AsyncStateMachineAttribute"));
    }

    private static MethodBase? ResolveIteratorMethod(MethodBase moveNextMethod)
    {
        // Similar to async method resolution but looking for iterator patterns
        var stateMachineType = moveNextMethod.DeclaringType;
        if (stateMachineType == null)
        {
            return null;
        }

        var containingType = ResolveContainingType(stateMachineType);
        if (containingType == null)
        {
            return null;
        }

        var originalMethodName = ExtractMethodNameFromStateMachine(stateMachineType.Name);
        if (originalMethodName == null)
        {
            return null;
        }

        return containingType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Static | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == originalMethodName &&
                                               IsIteratorMethod(m));
    }

    private static bool IsIteratorMethod(MethodInfo method)
    {
        var returnType = method.ReturnType;
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(returnType) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static MethodBase? ResolveLocalFunction(MethodBase method)
    {
        // Extract the original method name from the local function name
        var localFunctionName = method.Name;
        var parentMethodStart = localFunctionName.IndexOf('<');
        var parentMethodEnd = localFunctionName.IndexOf('>', parentMethodStart);

        if (parentMethodStart < 0 || parentMethodEnd <= parentMethodStart)
        {
            return null;
        }

        var parentMethodName = localFunctionName.Substring(parentMethodStart + 1, parentMethodEnd - parentMethodStart - 1);

        var declaringType = method.DeclaringType?.DeclaringType;
        if (declaringType == null)
        {
            return null;
        }

        return declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                      BindingFlags.Static | BindingFlags.Instance)
                           .FirstOrDefault(m => m.Name == parentMethodName);
    }

    private static MethodNameParts? ParseMethodName(string fullName)
    {
        var normalizedName = fullName;
        if (fullName.Contains(">d__") && !fullName.Contains("+"))
        {
            var stateMachineIndex = fullName.IndexOf("<");
            if (stateMachineIndex > 0)
            {
                var lastDotBeforeStateMachine = fullName.LastIndexOf('.', stateMachineIndex);
                if (lastDotBeforeStateMachine > 0)
                {
                    normalizedName = fullName.Substring(0, lastDotBeforeStateMachine) +
                                     "+" +
                                     fullName.Substring(lastDotBeforeStateMachine + 1);
                }
            }
        }

        // For generic type parameters, temporarily replace dots inside square brackets
        // This prevents incorrect splitting of the assembly-qualified type name
        var sb = StringBuilderCache.Acquire();
        sb.Append(normalizedName);
        var insideBrackets = false;
        var bracketCount = 0;
        for (var i = 0; i < sb.Length; i++)
        {
            var c = sb[i];
            if (c == '[')
            {
                insideBrackets = true;
                bracketCount++;
            }
            else if (c == ']')
            {
                bracketCount--;
                insideBrackets = bracketCount > 0;
            }
            else if (insideBrackets && c == '.')
            {
                sb[i] = '\u0001'; // Use a temporary character that won't appear in normal type names
            }
        }

        normalizedName = StringBuilderCache.GetStringAndRelease(sb);

        var parts = normalizedName.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var lastPart = parts[parts.Length - 1];
        var parenIndex = lastPart.IndexOf('(');
        var methodName = parenIndex > 0 ? lastPart.Substring(0, parenIndex) : lastPart;
        methodName = NormalizeMethodName(methodName);

        // Everything except the method name is type parts
        var typeParts = parts.Take(parts.Length - 1)
                            .SelectMany(p => p.Split('+'))
                            .ToArray();

        // Normalize generic type parts by only keeping up to the first [ character
        typeParts = typeParts.Select(p =>
        {
            var bracketIndex = p.IndexOf('[');
            if (bracketIndex > 0)
            {
                // Keep type name and its arity (e.g., "NestedGeneric`1")
                var backtickIndex = p.IndexOf('`');
                if (backtickIndex > 0)
                {
                    return p.Substring(0, bracketIndex);
                }
            }

            return p;
        }).ToArray();

        return new MethodNameParts(typeParts, methodName);
    }

    private static bool DoMethodPartsMatch(MethodNameParts stackTrace, MethodNameParts methodBase, bool isStateMachine)
    {
        // If we have an exact match on both type parts and method name, return true first
        if (AreArraysEqual(stackTrace.TypeParts, methodBase.TypeParts) &&
            stackTrace.MethodName == methodBase.MethodName)
        {
            return true;
        }

        // For async lambdas, we need special handling
        var stackTraceLambdaId = ExtractLambdaIdentifier(stackTrace.MethodName);
        if (!string.IsNullOrEmpty(stackTraceLambdaId))
        {
            var lastTypePart = methodBase.TypeParts.LastOrDefault() ?? string.Empty;
            var typePartLambdaId = ExtractLambdaIdentifier(lastTypePart);

            if (!string.IsNullOrEmpty(typePartLambdaId))
            {
                return stackTraceLambdaId == typePartLambdaId;
            }
        }

        // Check if this is a local function (but not an async lambda)
        var isLocalFunction = methodBase.TypeParts.Length > 0 &&
                             methodBase.TypeParts.Last().StartsWith("<<") &&
                             methodBase.TypeParts.Last().EndsWith(">d") &&
                             !methodBase.TypeParts.Last().Contains("b__");

        if (isLocalFunction)
        {
            var baseTypeParts = methodBase.TypeParts.Take(methodBase.TypeParts.Length - 1).ToArray();
            if (!AreArraysEqual(stackTrace.TypeParts, baseTypeParts))
            {
                return false;
            }

            // For .NET Framework local functions, they appear as state machines with MoveNext
            if (isStateMachine && stackTrace.MethodName == "MoveNext")
            {
                return true;
            }

            // For .NET Core local functions, extract the original method name
            var lastPart = methodBase.TypeParts.Last();
            var functionName = lastPart.Substring(1, lastPart.Length - 3);
            return stackTrace.MethodName == functionName;
        }

        // Handle other state machines (async methods and iterators)
        if (isStateMachine)
        {
            var baseTypeParts = methodBase.TypeParts.Take(methodBase.TypeParts.Length - 1).ToArray();

            // For .NET Framework, the stack trace shows the state machine type directly
            var isDirectStateMachineMatch = AreArraysEqual(stackTrace.TypeParts, methodBase.TypeParts);

            // For .NET Core, the stack trace might not show the state machine type
            var isBaseTypeMatch = AreArraysEqual(stackTrace.TypeParts, baseTypeParts);

            if (!isDirectStateMachineMatch && !isBaseTypeMatch)
            {
                return false;
            }

            if (stackTrace.MethodName == "MoveNext")
            {
                return true;
            }

            var lastPart = methodBase.TypeParts.Last();
            var methodStart = lastPart.IndexOf('<') + 1;
            var methodEnd = lastPart.LastIndexOf('>');
            if (methodStart <= 0 || methodEnd <= methodStart)
            {
                return false;
            }

            var originalMethod = lastPart.Substring(methodStart, methodEnd - methodStart);
            return stackTrace.MethodName == originalMethod;
        }

        // Regular case
        return AreArraysEqual(stackTrace.TypeParts, methodBase.TypeParts) &&
               AreMethodNamesEquivalent(stackTrace.MethodName, methodBase.MethodName);
    }

    private static bool AreArraysEqual(string[] arr1, string[] arr2)
    {
        if (arr1.Length != arr2.Length)
        {
            return false;
        }

        for (var i = 0; i < arr1.Length; i++)
        {
            if (!arr1[i].Equals(arr2[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStateMachineType(Type type)
    {
        return type.Name.Contains("d__") &&
               (type.GetInterfaces().Any(i => i.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine") ||
                type.GetInterfaces().Any(i => i.FullName == "System.Collections.IEnumerator"));
    }

    /// <summary>
    /// Represents the parts of a method's full name
    /// </summary>
    private readonly struct MethodNameParts(string[] typeParts, string methodName)
    {
        public string[] TypeParts { get; } = typeParts;

        public string MethodName { get; } = methodName;
    }
}
