// <copyright file="MethodExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Util;

#nullable enable
namespace Datadog.Trace.Debugger.Helpers
{
    internal static class MethodExtensions
    {
        /// <summary>
        /// Gets fully qualified name of a method with parameters and generics. For example SkyApm.Sample.ConsoleApp.Program.Main(String[] args).
        /// Code was copied from System.Diagnostics.StackTrace.ToString() - .NET Standard implementation, not .NET Framework
        /// </summary>
        internal static string? GetFullyQualifiedName(this MethodBase mb)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            try
            {
                sb.Append(mb.Name + "_");

                var declaringType = mb.DeclaringType;
                var methodName = mb.Name;
                var methodChanged = false;
                if (declaringType != null && declaringType.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                {
                    var isAsync = typeof(IAsyncStateMachine).IsAssignableFrom(declaringType);
                    if (isAsync || typeof(IEnumerator).IsAssignableFrom(declaringType))
                    {
                        methodChanged = TryResolveStateMachineMethod(ref mb, out declaringType);
                    }
                }

                // if there is a type (non global method) print it
                // ResolveStateMachineMethod may have set declaringType to null
                if (declaringType != null)
                {
                    // Append t.FullName, replacing '+' with '.'
                    var fullName = declaringType.FullName;
                    if (fullName == null)
                    {
                        return null;
                    }

                    for (var i = 0; i < fullName.Length; i++)
                    {
                        var ch = fullName[i];
                        sb.Append(ch == '+' ? '.' : ch);
                    }

                    sb.Append('.');
                }

                sb.Append(mb.Name);

                // deal with the generic portion of the method
                if (mb is MethodInfo mi && mi.IsGenericMethod)
                {
                    var typars = mi.GetGenericArguments();
                    sb.Append('[');
                    var k = 0;
                    var fFirstTyParam = true;
                    while (k < typars.Length)
                    {
                        if (fFirstTyParam == false)
                        {
                            sb.Append(',');
                        }
                        else
                        {
                            fFirstTyParam = false;
                        }

                        sb.Append(typars[k].Name);
                        k++;
                    }

                    sb.Append(']');
                }

                ParameterInfo[]? pi = null;
                try
                {
                    pi = mb.GetParameters();
                }
                catch
                {
                    // The parameter info cannot be loaded, so we don't
                    // append the parameter list.
                }

                if (pi != null)
                {
                    // arguments printing
                    sb.Append('(');
                    var fFirstParam = true;
                    for (var j = 0; j < pi.Length; j++)
                    {
                        if (fFirstParam == false)
                        {
                            sb.Append(", ");
                        }
                        else
                        {
                            fFirstParam = false;
                        }

                        var typeName = pi[j].ParameterType?.Name ?? "<UnknownType>";
                        sb.Append(typeName);
                        sb.Append(' ');
                        sb.Append(pi[j].Name);
                    }

                    sb.Append(')');
                }

                if (methodChanged)
                {
                    // Append original method name e.g. +MoveNext()
                    sb.Append('+');
                    sb.Append(methodName);
                    sb.Append('(').Append(')');
                }

                return sb.ToString();
            }
            catch
            {
                return null;
            }
            finally
            {
                StringBuilderCache.Release(sb);
            }
        }

        private static bool TryResolveStateMachineMethod(ref MethodBase method, out Type? declaringType)
        {
            declaringType = method.DeclaringType;

            var parentType = declaringType?.DeclaringType;
            if (parentType == null)
            {
                return false;
            }

            var methods = parentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (MethodInfo candidateMethod in methods)
            {
                var attributes = candidateMethod.GetCustomAttributes<StateMachineAttribute>(inherit: false);

                bool foundAttribute = false, foundIteratorAttribute = false;
                foreach (var attr in attributes)
                {
                    if (attr.StateMachineType == declaringType)
                    {
                        foundAttribute = true;
                        foundIteratorAttribute |= attr is IteratorStateMachineAttribute || attr.GetType().Name == "AsyncIteratorStateMachineAttribute";
                    }
                }

                if (foundAttribute)
                {
                    // If this is an iterator (sync or async), mark the iterator as changed, so it gets the + annotation
                    // of the original method. Non-iterator async state machines resolve directly to their builder methods
                    // so aren't marked as changed.
                    method = candidateMethod;
                    declaringType = candidateMethod.DeclaringType;
                    return foundIteratorAttribute;
                }
            }

            return false;
        }
    }
}
