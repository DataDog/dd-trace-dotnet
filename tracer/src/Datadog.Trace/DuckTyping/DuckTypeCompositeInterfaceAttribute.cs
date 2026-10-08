// <copyright file="DuckTypeCompositeInterfaceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Declares an interface that inherits several interfaces of a library, for a reverse proxy that must implement all of
    /// them (a reverse proxy implements a single type). <see cref="DuckType.GetCompositeInterface"/> gets it: dynamic duck
    /// typing emits it, and a NativeAOT build generates it from this declaration when the application has the interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class DuckTypeCompositeInterfaceAttribute : Attribute
    {
        public DuckTypeCompositeInterfaceAttribute(string name, params string[] interfaceTypeNames)
        {
            Name = name;
            InterfaceTypeNames = interfaceTypeNames;
        }

        /// <summary>
        /// Gets the name of the interface, in the namespace <see cref="DuckType.CompositeInterfacesNamespace"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the assembly qualified names of the interfaces it inherits.
        /// </summary>
        public string[] InterfaceTypeNames { get; }
    }
}
