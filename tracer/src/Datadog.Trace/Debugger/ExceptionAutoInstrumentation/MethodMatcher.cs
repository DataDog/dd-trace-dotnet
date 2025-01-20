// <copyright file="MethodMatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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
        // First try to resolve special method types (async, iterator, etc.)
        var resolvedMethod = ResolveSpecialMethod(methodBase);

        // Use resolved method if available, otherwise use original
        methodBase = resolvedMethod ?? methodBase;

        // Normalize and parse both representations
        var normalizedStackTrace = NormalizeMethodText(stackTraceMethodText);
        var methodBaseName = BuildMethodSignature(methodBase);

        var stackTraceParts = ParseMethodName(normalizedStackTrace);
        var methodBaseParts = ParseMethodName(methodBaseName);

        if (stackTraceParts == null || methodBaseParts == null)
        {
            return normalizedStackTrace.Equals(methodBaseName, StringComparison.Ordinal);
        }

        return DoMethodPartsMatch(stackTraceParts, methodBaseParts);
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

        // For async state machine methods
        if (methodBaseMethod == "MoveNext")
        {
            // Either it's an async method or we've already handled the lambda case
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
        var parts = fullName.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        // The last part is always the method name
        var methodName = NormalizeMethodName(parts[parts.Length - 1]);

        // Find where the type name starts (looking for capital letter after namespace)
        var typeStartIndex = -1;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Length > 0 && char.IsUpper(parts[i][0]))
            {
                typeStartIndex = i;
                break;
            }
        }

        if (typeStartIndex == -1)
        {
            return null;
        }

        var namespaceParts = new string[typeStartIndex];
        Array.Copy(parts, 0, namespaceParts, 0, typeStartIndex);

        var typeParts = new string[parts.Length - typeStartIndex - 1];
        Array.Copy(parts, typeStartIndex, typeParts, 0, parts.Length - typeStartIndex - 1);

        return new MethodNameParts(namespaceParts, typeParts, methodName);
    }

    private static bool DoMethodPartsMatch(MethodNameParts stackTrace, MethodNameParts methodBase)
    {
        // Namespace must match exactly
        if (!AreArraysEqual(stackTrace.NamespaceParts, methodBase.NamespaceParts))
        {
            return false;
        }

        // For async lambdas, we need special handling
        var stackTraceLambdaId = ExtractLambdaIdentifier(stackTrace.MethodName);
        if (!string.IsNullOrEmpty(stackTraceLambdaId))
        {
            // Check if the method base type parts contain the lambda identifier
            var lastTypePart = methodBase.TypeParts.LastOrDefault() ?? string.Empty;
            var typePartLambdaId = ExtractLambdaIdentifier(lastTypePart);

            if (!string.IsNullOrEmpty(typePartLambdaId))
            {
                return stackTraceLambdaId == typePartLambdaId;
            }
        }

        // If not a lambda match, then both type parts and method name must match exactly
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

    private static string NormalizeMethodText(string methodText)
    {
        // Remove parameter list if present (content within parentheses)
        var parenthesesIndex = methodText.IndexOf('(');
        if (parenthesesIndex > 0)
        {
            methodText = methodText.Substring(0, parenthesesIndex);
        }

        // Special handling for generic method type arguments in stack trace format [T1,T2]
        var genericIndex = methodText.IndexOf('[');
        if (genericIndex > 0)
        {
            var closeIndex = methodText.IndexOf(']', genericIndex);
            if (closeIndex > genericIndex)
            {
                // Count the number of type parameters
                var typeParams = methodText.Substring(genericIndex + 1, closeIndex - genericIndex - 1)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                // Replace with a normalized form
                methodText = methodText.Substring(0, genericIndex) + "`" + typeParams;
            }
        }

        return methodText;
    }

    private static string NormalizeMethodName(string methodName)
    {
        // Remove generic arity if present
        var backtickIndex = methodName.IndexOf('`');
        if (backtickIndex > 0)
        {
            methodName = methodName.Substring(0, backtickIndex);
        }

        return methodName;
    }

    private static string BuildMethodSignature(MethodBase method)
    {
        var sb = new StringBuilder();

        // Add declaring type's full name
        if (method.DeclaringType != null)
        {
            var typeFullName = method.DeclaringType.FullName ?? method.DeclaringType.Name;
            sb.Append(typeFullName.Replace("+", "."));
        }

        sb.Append('.');
        sb.Append(method.Name);

        return sb.ToString();
    }

    /// <summary>
    /// Represents the parts of a method's full name
    /// </summary>
    private class MethodNameParts
    {
        public MethodNameParts(string[] namespaceParts, string[] typeParts, string methodName)
        {
            NamespaceParts = namespaceParts;
            TypeParts = typeParts;
            MethodName = methodName;
        }

        public string[] NamespaceParts { get; }

        public string[] TypeParts { get; }

        public string MethodName { get; }
    }
}
