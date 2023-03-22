// <copyright file="SupportedTypesService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static class SupportedTypesService
    {
        private static readonly Type[] AllowedCollectionTypes =
        {
            typeof(List<>),
            typeof(ArrayList),
            typeof(LinkedList<>),
            typeof(SortedList),
            typeof(SortedList<,>),
            typeof(Stack),
            typeof(Stack<>),
            typeof(ConcurrentStack<>),
            typeof(Queue),
            typeof(Queue<>),
            typeof(ConcurrentQueue<>),
            typeof(HashSet<>),
            typeof(SortedSet<>),
            typeof(ConcurrentBag<>),
            typeof(BlockingCollection<>),
            typeof(ConditionalWeakTable<,>),
        };

        private static readonly Type[] AllowedDictionaryTypes =
        {
            typeof(Dictionary<,>),
            typeof(SortedDictionary<,>),
            typeof(ConcurrentDictionary<,>),
            typeof(Hashtable),
        };

        private static readonly string[] AllowedSpecialCasedCollectionTypeNames = { }; // "RangeIterator"

        private static readonly Type[] AllowedTypesSafeToCallToString =
        {
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(DateTimeOffset),
            typeof(Uri),
            typeof(Guid),
            typeof(Version),
            typeof(StackTrace),
            typeof(StringBuilder)
        };

        private static readonly Type[] DeniedTypes =
        {
            typeof(SecureString),
        };

        internal static bool IsSafeToCallToString(Type type, bool includeCollection = true)
        {
            return TypeExtensions.IsSimple(type) ||
                   AllowedTypesSafeToCallToString.Contains(type) ||
                   (includeCollection && IsSupportedCollection(type));
        }

        internal static bool IsSupportedDictionary(object o)
        {
            if (o == null)
            {
                return false;
            }

            var type = o.GetType();
            return AllowedDictionaryTypes.Any(whiteType => whiteType == type || (type.IsGenericType && whiteType == type.GetGenericTypeDefinition()));
        }

        internal static bool IsSupportedCollection(object o)
        {
            if (o == null)
            {
                return false;
            }

            return IsSupportedCollection(o.GetType());
        }

        internal static bool IsSupportedCollection(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsArray)
            {
                return true;
            }

            return AllowedCollectionTypes.Any(whiteType => whiteType == type || (type.IsGenericType && whiteType == type.GetGenericTypeDefinition())) ||
                   AllowedSpecialCasedCollectionTypeNames.Any(white => white.Equals(type.Name, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsDenied(Type type)
        {
            if (type == null)
            {
                return false;
            }

            return DeniedTypes.Any(deniedType => deniedType == type || (type.IsGenericType && deniedType == type.GetGenericTypeDefinition()));
        }
    }
}
