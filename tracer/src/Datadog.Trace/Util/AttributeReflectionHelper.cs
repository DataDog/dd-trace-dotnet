// <copyright file="AttributeReflectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Utility class for efficiently finding attributes on methods through reflection with caching.
    /// </summary>
    internal static class AttributeReflectionHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AttributeReflectionHelper));
        private static readonly ConcurrentDictionary<string, string?> AttributePropertyCache = new();

        /// <summary>
        /// Extracts a property value from a method attribute with full caching.
        /// </summary>
        /// <param name="entryPoint">The entry point in format "Namespace.ClassName.MethodName"</param>
        /// <param name="attributeName">The attribute name to search for (e.g., "ServiceBusTriggerAttribute")</param>
        /// <param name="propertyNames">Property names to search for (in order of preference)</param>
        /// <returns>The property value as string, or null if not found</returns>
        public static string? ExtractAttributeProperty(string entryPoint, string attributeName, params string[] propertyNames)
        {
            if (string.IsNullOrEmpty(entryPoint) || string.IsNullOrEmpty(attributeName) || propertyNames.Length == 0)
            {
                return null;
            }

            var cacheKey = $"{entryPoint}:{attributeName}:{string.Join(",", propertyNames)}";
            return AttributePropertyCache.GetOrAdd(cacheKey, _ => ExtractAttributePropertyInternal(entryPoint, attributeName, propertyNames));
        }

        private static string? ExtractAttributeProperty(object attribute, params string[] propertyNames)
        {
            try
            {
                var attributeType = attribute.GetType();
                var properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var propertyName in propertyNames)
                {
                    foreach (var property in properties)
                    {
                        if (property.Name == propertyName)
                        {
                            var propValue = property.GetValue(attribute);
                            if (propValue is string strValue && !string.IsNullOrEmpty(strValue))
                            {
                                return strValue;
                            }

                            break; // Found but empty
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting property from attribute {AttributeType}", attribute.GetType().Name);
            }

            return null;
        }

        private static string? ExtractAttributePropertyInternal(string entryPoint, string attributeName, string[] propertyNames)
        {
            var attribute = ExtractMethodAttribute(entryPoint, attributeName);
            if (attribute == null)
            {
                return null;
            }

            return ExtractAttributeProperty(attribute, propertyNames);
        }

        private static object? ExtractMethodAttribute(string entryPoint, string attributeName)
        {
            try
            {
                var parts = entryPoint.Split('.');
                if (parts.Length < 3)
                {
                    return null;
                }

                var methodName = parts[parts.Length - 1];
                var className = string.Join(".", parts.Take(parts.Length - 1));

                // Find the type across assemblies
                Type? functionType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (ShouldSkipAssembly(assembly))
                    {
                        continue;
                    }

                    try
                    {
                        functionType = assembly.GetType(className);
                        if (functionType != null)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error getting type {ClassName} from assembly {AssemblyName}", className, assembly.FullName ?? "Unknown");
                    }
                }

                if (functionType == null)
                {
                    return null;
                }

                var methods = functionType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                                              .Where(m => m.Name == methodName)
                                              .ToArray();
                MethodInfo? method = methods.FirstOrDefault();

                if (method == null)
                {
                    return null;
                }

                // Search method parameters for the attribute
                var parameters = method.GetParameters();
                foreach (var parameter in parameters)
                {
                    var attributes = parameter.GetCustomAttributes(false);
                    foreach (var attribute in attributes)
                    {
                        var attributeType = attribute.GetType();
                        if (attributeType.Name == attributeName)
                        {
                            return attribute;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting method attribute info for entry point: {EntryPoint}", entryPoint);
            }

            return null;
        }

        private static bool ShouldSkipAssembly(Assembly assembly)
        {
            try
            {
                return assembly.ManifestModule.IsResource() ||
                       IsSystemAssembly(assembly);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;
            if (string.IsNullOrEmpty(assemblyName))
            {
                return true;
            }

            return assemblyName.StartsWith("System.") ||
                   assemblyName.StartsWith("Microsoft.") ||
                   assemblyName.StartsWith("mscorlib") ||
                   assemblyName.StartsWith("netstandard") ||
                   assemblyName.StartsWith("Datadog.Trace");
        }
    }
}
