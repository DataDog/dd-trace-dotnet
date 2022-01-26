// <copyright file="FolderProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.Tools.Runner.Aot
{
    internal class FolderProcessor
    {
        private static readonly NativeCallTargetDefinition[] Definitions;
        private static readonly NativeCallTargetDefinition[] DerivedDefinitions;

        private string _inputFolder;
        private string _outputFolder;
        private AssemblyProcessor[] _assemblyProcessors;

        static FolderProcessor()
        {
            Definitions = InstrumentationDefinitions.GetAllDefinitions().Definitions;
            DerivedDefinitions = InstrumentationDefinitions.GetDerivedDefinitions().Definitions;
        }

        public FolderProcessor(string inputFolder, string outputFolder)
        {
            _inputFolder = inputFolder;
            _outputFolder = outputFolder;
            _assemblyProcessors = null;
            _ = _assemblyProcessors;
        }
    }
}
