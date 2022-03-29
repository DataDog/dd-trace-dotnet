// <copyright file="DefinitionItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler;
using Mono.Cecil;

namespace Datadog.Trace.Tools.Runner.Aot
{
    internal readonly struct DefinitionItem
    {
        public readonly AssemblyDefinition TargetAssemblyDefinition;
        public readonly TypeDefinition TargetTypeDefinition;
        public readonly MethodDefinition TargetMethodDefinition;
        public readonly Type IntegrationType;
        public readonly NativeCallTargetDefinition Definition;

        public DefinitionItem(AssemblyDefinition targetAssemblyDefinition, TypeDefinition targetTypeDefinition, MethodDefinition targetMethodDefinition, Type integrationType, NativeCallTargetDefinition definition)
        {
            TargetAssemblyDefinition = targetAssemblyDefinition;
            TargetTypeDefinition = targetTypeDefinition;
            TargetMethodDefinition = targetMethodDefinition;
            IntegrationType = integrationType;
            Definition = definition;
        }
    }
}
