// <copyright file="SymbolMethodExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Pdb
{
    internal static class SymbolMethodExtensions
    {
        public static IEnumerable<SymbolVariable> GetLocalVariablesInScope(this SymbolMethod method, int offset)
        {
            return
                GetAllScopes(method)
                   .Where(s => s.StartOffset <= offset && s.EndOffset >= offset)
                   .SelectMany(s => s.Locals);
        }

        public static IEnumerable<SymbolVariable> GetLocalVariables(this SymbolMethod method)
        {
            return
                GetAllScopes(method)
                   .SelectMany(s => s.Locals);
        }

        private static IList<SymbolScope> GetAllScopes(SymbolMethod method)
        {
            var result = new List<SymbolScope>();
            RetrieveAllNestedScopes(method.RootScope, result);
            return result;
        }

        private static void RetrieveAllNestedScopes(SymbolScope scope, List<SymbolScope> result)
        {
            // Recursively extract all nested scopes in method
            if (scope == null)
            {
                return;
            }

            result.Add(scope);
            foreach (var innerScope in scope.Children)
            {
                RetrieveAllNestedScopes(innerScope, result);
            }
        }
    }
}
