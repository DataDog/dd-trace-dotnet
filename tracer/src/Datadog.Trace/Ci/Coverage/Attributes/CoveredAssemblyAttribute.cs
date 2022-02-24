// <copyright file="CoveredAssemblyAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Ci.Coverage.Attributes
{
    /// <summary>
    /// Covered assembly attribute
    /// This attributes marks an assembly as a processed.
    /// </summary>
    public class CoveredAssemblyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoveredAssemblyAttribute"/> class.
        /// </summary>
        /// <param name="totalMethods">Total number of methods</param>
        /// <param name="totalInstructions">Total number of instructions</param>
        public CoveredAssemblyAttribute(ulong totalMethods, ulong totalInstructions)
        {
            TotalMethods = totalMethods;
            TotalInstructions = totalInstructions;
        }

        /// <summary>
        /// Gets total number methods
        /// </summary>
        public ulong TotalMethods { get; }

        /// <summary>
        /// Gets total number of instructions
        /// </summary>
        public ulong TotalInstructions { get; }
    }
}
