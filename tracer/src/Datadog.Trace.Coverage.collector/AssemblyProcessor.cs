// <copyright file="AssemblyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Attributes;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Ci.Coverage.Util;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CallSite = Mono.Cecil.CallSite;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Datadog.Trace.Coverage.Collector
{
    internal class AssemblyProcessor
    {
        private static readonly string? ExcludeFromCodeCoverageAttributeFullName = typeof(ExcludeFromCodeCoverageAttribute).FullName;
        private static readonly string? AvoidCoverageAttributeFullName = typeof(AvoidCoverageAttribute).FullName;
        private static readonly string? CoveredAssemblyAttributeFullName = typeof(CoveredAssemblyAttribute).FullName;
        private static readonly string? InternalsVisibleToAttributeFullName = typeof(InternalsVisibleToAttribute).FullName;
        private static readonly string? DebuggableAttributeFullName = typeof(DebuggableAttribute).FullName;

        private static readonly object PadLock = new();
        private static readonly Regex NetCorePattern = new(@".NETCoreApp,Version=v(\d.\d)", RegexOptions.Compiled);
        private static readonly Assembly TracerAssembly = typeof(CoverageReporter).Assembly;
        private static readonly string[] IgnoredAssemblies =
        {
            "NUnit3.TestAdapter.dll",
            "xunit.abstractions.dll",
            "xunit.assert.dll",
            "xunit.core.dll",
            "xunit.execution.dotnet.dll",
            "xunit.runner.reporters.netcoreapp10.dll",
            "xunit.runner.utility.netcoreapp10.dll",
            "xunit.runner.visualstudio.dotnetcore.testadapter.dll",
            "Xunit.SkippableFact.dll",
        };

        private readonly CoverageSettings _settings;
        private readonly ICollectorLogger _logger;
        private readonly string _assemblyFilePath;
        private readonly bool _enableJitOptimizations;
        private readonly CoverageMode _coverageMode;

        private byte[]? _strongNameKeyBlob;

        public AssemblyProcessor(string filePath, CoverageSettings settings, ICollectorLogger? logger = null)
        {
            _settings = settings;
            _logger = logger ?? new ConsoleCollectorLogger();
            _assemblyFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _enableJitOptimizations = settings.CIVisibility.CodeCoverageEnableJitOptimizations;
            _coverageMode = CoverageMode.LineExecution;
            if (settings.CIVisibility.CodeCoverageMode is { Length: > 0 } strCodeCoverageMode)
            {
                if (Enum.TryParse<CoverageMode>(strCodeCoverageMode, ignoreCase: true, out var coverageMode))
                {
                    _coverageMode = coverageMode;
                }
            }

            if (!File.Exists(_assemblyFilePath))
            {
                throw new FileNotFoundException($"Assembly not found in path: {_assemblyFilePath}");
            }

            if (!File.Exists(Path.ChangeExtension(filePath, ".pdb")))
            {
                Ci.Coverage.Exceptions.PdbNotFoundException.Throw();
            }
        }

        public string FilePath => _assemblyFilePath;

        public bool HasTracerAssemblyCopied { get; private set; }

        public unsafe void Process()
        {
            try
            {
                HasTracerAssemblyCopied = false;
                _logger.Debug($"Processing: {_assemblyFilePath}");

                // Check if the assembly is in the ignored assemblies list.
                var assemblyFullName = Path.GetFileName(_assemblyFilePath);
                if (Array.Exists(IgnoredAssemblies, i => assemblyFullName == i))
                {
                    return;
                }

                // Open the assembly
                var customResolver = new CustomResolver(_logger, _assemblyFilePath);
                customResolver.AddSearchDirectory(Path.GetDirectoryName(_assemblyFilePath));
                using var assemblyDefinition = AssemblyDefinition.ReadAssembly(_assemblyFilePath, new ReaderParameters
                {
                    ReadSymbols = true,
                    ReadWrite = true,
                    AssemblyResolver = customResolver,
                });

                var hasInternalsVisibleAttribute = false;
                foreach (var cAttr in assemblyDefinition.CustomAttributes)
                {
                    var attrFullName = cAttr.Constructor.DeclaringType.FullName;
                    if (attrFullName == AvoidCoverageAttributeFullName || attrFullName == ExcludeFromCodeCoverageAttributeFullName)
                    {
                        _logger.Debug($"Assembly: {FilePath}, ignored.");
                        return;
                    }

                    if (attrFullName == CoveredAssemblyAttributeFullName)
                    {
                        _logger.Debug($"Assembly: {FilePath}, already have coverage information.");
                        return;
                    }

                    if (FiltersHelper.FilteredByAttribute(attrFullName, _settings.ExcludeByAttribute))
                    {
                        _logger.Debug($"Assembly: {FilePath}, ignored by settings attribute filter.");
                        return;
                    }

                    hasInternalsVisibleAttribute |= attrFullName == InternalsVisibleToAttributeFullName;

                    // Enable Jit Optimizations
                    if (_enableJitOptimizations)
                    {
                        // We check for the Debuggable attribute to enable jit optimizations and improve coverage performance.
                        if (attrFullName == DebuggableAttributeFullName)
                        {
                            _logger.Debug($"Modifying the DebuggableAttribute to enable jit optimizations");

                            // If the attribute is using the .ctor: DebuggableAttribute(DebuggableAttribute+DebuggingModes)
                            // We change it to `Default (1)` to enable jit optimizations.
                            if (cAttr.ConstructorArguments.Count == 1)
                            {
                                cAttr.ConstructorArguments[0] = new CustomAttributeArgument(cAttr.ConstructorArguments[0].Type, 2);
                            }

                            // If the attribute is using the .ctor: DebuggableAttribute(Boolean, Boolean)
                            // We change the `isJITOptimizerDisabled` second argument to `false` to enable jit optimizations.
                            if (cAttr.ConstructorArguments.Count == 2)
                            {
                                cAttr.ConstructorArguments[1] = new CustomAttributeArgument(cAttr.ConstructorArguments[1].Type, false);
                            }
                        }
                    }
                }

                // Gets the Datadog.Trace target framework
                var tracerTarget = GetTracerTarget(assemblyDefinition);

                if (assemblyDefinition.Name.HasPublicKey)
                {
                    _logger.Debug($"Assembly: {FilePath} is signed.");

                    var snkFilePath = _settings.CIVisibility.CodeCoverageSnkFilePath;
                    _logger.Debug($"Assembly: {FilePath} loading .snk file: {snkFilePath}.");
                    if (!string.IsNullOrWhiteSpace(snkFilePath) && File.Exists(snkFilePath))
                    {
                        _logger.Debug($"{snkFilePath} exists.");
                        _strongNameKeyBlob = File.ReadAllBytes(snkFilePath);
                        _logger.Debug($"{snkFilePath} loaded.");
                    }
                    else if (tracerTarget == TracerTarget.Net461)
                    {
                        _logger.Warning($"Assembly: {FilePath}, is a net461 signed assembly, a .snk file is required ({Configuration.ConfigurationKeys.CIVisibility.CodeCoverageSnkFile} environment variable).");
                        return;
                    }
                    else if (hasInternalsVisibleAttribute)
                    {
                        _logger.Warning($"Assembly: {FilePath}, is a signed assembly with the InternalsVisibleTo attribute. A .snk file is required ({Configuration.ConfigurationKeys.CIVisibility.CodeCoverageSnkFile} environment variable).");
                        return;
                    }
                }

                // We open the exact datadog assembly to be copied to the target, this is because the AssemblyReference lists
                // differs depends on the target runtime. (netstandard, .NET 5.0 or .NET 4.6.2)
                using var datadogTracerAssembly = AssemblyDefinition.ReadAssembly(GetDatadogTracer(tracerTarget));

                var isDirty = false;
                var fileMetadataIndex = 0;

                // Process all modules in the assembly
                var module = assemblyDefinition.MainModule;
                _logger.Debug($"Processing module: {module.Name}");
                if (FiltersHelper.FilteredByAssemblyAndType(module.FileName, null, _settings.ExcludeFilters))
                {
                    _logger.Debug($"Module: {module.FileName}, ignored by settings filter");
                    return;
                }

                // Process all types defined in the module
                var moduleTypes = module.GetTypes().ToList();
                var fileDictionaryIndex = new Dictionary<string, FileMetadata>();

                var moduleCoverageMetadataTypeDefinition = datadogTracerAssembly.MainModule.GetType(typeof(ModuleCoverageMetadata).FullName);
                var moduleCoverageMetadataTypeReference = module.ImportReference(moduleCoverageMetadataTypeDefinition);

                var moduleCoverageMetadataImplTypeDef = new TypeDefinition(
                    typeof(ModuleCoverageMetadata).Namespace + ".Target",
                    "ModuleCoverage",
                    TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                    moduleCoverageMetadataTypeReference);

                var moduleCoverageMetadataImplCtor = new MethodDefinition(
                    ".ctor",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName,
                    module.TypeSystem.Void);

                // Create metadata instructions list
                var lstMetadataInstructions = new List<Instruction>();

                // Add the .ctor to the metadata type
                moduleCoverageMetadataImplTypeDef.Methods.Add(moduleCoverageMetadataImplCtor);

                var coverageReporterTypeDefinition = datadogTracerAssembly.MainModule.GetType(typeof(CoverageReporter<>).FullName);
                var coverageReporterTypeReference = module.ImportReference(coverageReporterTypeDefinition);
                var reportTypeGenericInstance = new GenericInstanceType(coverageReporterTypeReference);
                reportTypeGenericInstance.GenericArguments.Add(moduleCoverageMetadataImplTypeDef);

                var reportGetCountersMethod = new MethodReference("GetFileCounter", new PointerType(module.TypeSystem.Void), reportTypeGenericInstance)
                {
                    HasThis = false,
                    Parameters =
                    {
                        new ParameterDefinition(module.TypeSystem.Int32) { Name = "fileIndex" }
                    }
                };

                // GenericInstanceMethod? arrayEmptyOfIntMethodReference = null;
                for (var typeIndex = 0; typeIndex < moduleTypes.Count; typeIndex++)
                {
                    var moduleType = moduleTypes[typeIndex];
                    var skipType = false;
                    foreach (var cAttr in moduleType.CustomAttributes)
                    {
                        var attrFullName = cAttr.Constructor.DeclaringType.FullName;
                        if (attrFullName.Contains("TestSDKAutoGeneratedCode"))
                        {
                            // Test SDK adds an empty Type with an empty entrypoint with symbols that never gets executed.
                            skipType = true;
                            break;
                        }

                        if (attrFullName == ExcludeFromCodeCoverageAttributeFullName)
                        {
                            _logger.Debug($"Type: {moduleType.FullName}, ignored by: {ExcludeFromCodeCoverageAttributeFullName}");
                            skipType = true;
                            break;
                        }

                        if (FiltersHelper.FilteredByAttribute(attrFullName, _settings.ExcludeByAttribute))
                        {
                            _logger.Debug($"Type: {moduleType.FullName}, ignored by settings attribute filter");
                            skipType = true;
                            break;
                        }
                    }

                    var filteredTargetType = moduleType;
                    while (filteredTargetType is not null)
                    {
                        if (FiltersHelper.FilteredByAssemblyAndType(module.FileName, filteredTargetType.FullName, _settings.ExcludeFilters))
                        {
                            _logger.Debug($"Type: {filteredTargetType.FullName}, ignored by settings filter");
                            skipType = true;
                            break;
                        }

                        if (!filteredTargetType.IsNested)
                        {
                            break;
                        }

                        // Nested types are skipped if the declaring type is skipped.
                        filteredTargetType = filteredTargetType.DeclaringType;
                    }

                    if (skipType)
                    {
                        continue;
                    }

                    _logger.Debug($"\t{moduleType.FullName}");

                    var moduleTypeMethods = moduleType.Methods;

                    // Process all Methods in the type
                    for (var methodIndex = 0; methodIndex < moduleTypeMethods.Count; methodIndex++)
                    {
                        var moduleTypeMethod = moduleTypeMethods[methodIndex];
                        var skipMethod = false;
                        if (moduleTypeMethod.DebugInformation is null || !moduleTypeMethod.DebugInformation.HasSequencePoints)
                        {
                            _logger.Debug($"\t\t[NO] {moduleTypeMethod.FullName}");
                            continue;
                        }

                        foreach (var cAttr in moduleTypeMethod.CustomAttributes)
                        {
                            var attrFullName = cAttr.Constructor.DeclaringType.FullName;
                            if (FiltersHelper.FilteredByAttribute(attrFullName, _settings.ExcludeByAttribute))
                            {
                                _logger.Debug($"\t\t[NO] {moduleTypeMethod.FullName}, ignored by settings attribute filter");
                                skipMethod = true;
                                break;
                            }

                            if (attrFullName == ExcludeFromCodeCoverageAttributeFullName)
                            {
                                _logger.Debug($"\t\t[NO] {moduleTypeMethod.FullName}, ignored by: {ExcludeFromCodeCoverageAttributeFullName}");
                                skipType = true;
                                break;
                            }
                        }

                        if (skipMethod)
                        {
                            continue;
                        }

                        _logger.Debug($"\t\t[YES] {moduleTypeMethod.FullName}.");

                        // Extract body from the method
                        if (moduleTypeMethod.HasBody)
                        {
                            /*** This block rewrites the method body code that looks like this:
                             *
                             *  public static class MyMathClass
                             *  {
                             *      public static int Factorial(int value)
                             *      {
                             *          if (value == 1)
                             *          {
                             *              return 1;
                             *          }
                             *          return value * Factorial(value - 1);
                             *      }
                             *  }
                             *
                             *** To this:
                             *
                             *  using Datadog.Trace.Ci.Coverage;
                             *
                             *  public static int Factorial(int value)
                             *  {
                             *      var counters = CoverageReporter<ModuleCoverage>.GetCounters(5)
                             *      _ = counters[5];
                             *      counters[0]++;
                             *      counters[1]++;
                             *      int result;
                             *      if (value == 1)
                             *      {
                             *          counters[2]++;
                             *          counters[3]++;
                             *          result = 1;
                             *      }
                             *      else
                             *      {
                             *          counters[4]++;
                             *          result = value * Factorial(value - 1);
                             *      }
                             *      counters[5]++;
                             *      return result;
                             *  }
                             */

                            var sequencePoints = moduleTypeMethod.DebugInformation.SequencePoints;

                            string? filePath = null;
                            foreach (var pt in sequencePoints)
                            {
                                if (pt.IsHidden || pt.Document is null || string.IsNullOrWhiteSpace(pt.Document.Url))
                                {
                                    continue;
                                }

                                var documentUrl = pt.Document.Url;
                                if (string.IsNullOrEmpty(documentUrl))
                                {
                                    continue;
                                }

                                if (filePath is null)
                                {
                                    filePath = documentUrl;
                                    break;
                                }
                            }

                            if (filePath is null)
                            {
                                continue;
                            }

                            if (FiltersHelper.FilteredBySourceFile(filePath, _settings.ExcludeSourceFiles))
                            {
                                _logger.Debug($"\t\t[NO] {moduleTypeMethod.FullName}, ignored by settings source filter");
                                continue;
                            }

                            var methodBody = moduleTypeMethod.Body;
                            var instructions = methodBody.Instructions;
                            var instructionsOriginalLength = instructions.Count;
                            if (instructions.Capacity < instructionsOriginalLength * 2)
                            {
                                instructions.Capacity = instructionsOriginalLength * 2;
                            }

                            // Step 1 - Remove Short OpCodes
                            foreach (var instruction in instructions)
                            {
                                RemoveShortOpCodes(instruction);
                            }

                            // Step 2 - Clone sequence points
                            var instructionsWithValidSequencePoints = new List<(Instruction Instruction, SequencePoint? SequencePoint)>();
                            for (var i = 0; i < sequencePoints.Count; i++)
                            {
                                var currentSequencePoint = sequencePoints[i];
                                if (!currentSequencePoint.IsHidden)
                                {
                                    instructionsWithValidSequencePoints.Add((instructions.First(i => i.Offset == currentSequencePoint.Offset), currentSequencePoint));
                                }
                            }

                            VariableDefinition? countersVariable = null;
                            if (instructionsWithValidSequencePoints.Count > 1 || instructions[0] != instructionsWithValidSequencePoints[0].Instruction)
                            {
                                // Step 3 - Modify local var to add the Coverage counters instance.
                                if (_coverageMode == CoverageMode.LineExecution)
                                {
                                    countersVariable = new VariableDefinition(new PointerType(module.TypeSystem.Byte));
                                }
                                else
                                {
                                    countersVariable = new VariableDefinition(new PointerType(module.TypeSystem.Int32));
                                }

                                methodBody.Variables.Add(countersVariable);
                            }

                            // Step 4 - Insert the counter retriever
                            FileMetadata fileMetadata;
                            if (!fileDictionaryIndex.TryGetValue(filePath, out fileMetadata))
                            {
                                fileMetadata = new FileMetadata(fileMetadataIndex++, filePath);
                                fileDictionaryIndex[filePath] = fileMetadata;
                            }

                            instructions.Insert(0, Instruction.Create(OpCodes.Ldc_I4, fileMetadata.Index));
                            instructions.Insert(1, Instruction.Create(OpCodes.Call, reportGetCountersMethod));
                            if (countersVariable is not null)
                            {
                                instructions.Insert(2, Instruction.Create(OpCodes.Stloc, countersVariable));
                            }

                            // Step 5 - Insert line reporter
                            for (var i = 0; i < instructionsWithValidSequencePoints.Count; i++)
                            {
                                var currentInstructionAndSequencePoint = instructionsWithValidSequencePoints[i];
                                var currentInstruction = currentInstructionAndSequencePoint.Instruction;
                                var currentSequencePoint = currentInstructionAndSequencePoint.SequencePoint;
                                if (currentSequencePoint is null)
                                {
                                    // If is null is because we already wrote the line reporter for this instruction in an optimization.
                                    continue;
                                }

                                // let's check if the next sequence point is for the same line, so we can optimize the line reporter
                                if (i + 1 < instructionsWithValidSequencePoints.Count)
                                {
                                    var nextSequencePoint = instructionsWithValidSequencePoints[i + 1].SequencePoint;
                                    if (currentSequencePoint.StartLine == nextSequencePoint?.StartLine)
                                    {
                                        // next sequence point is for the same line, so we can skip this one.
                                        continue;
                                    }
                                }

                                var currentInstructionIndex = instructions.IndexOf(currentInstruction);
                                var currentInstructionClone = CloneInstruction(currentInstruction);

                                if (countersVariable is not null)
                                {
                                    currentInstruction.OpCode = OpCodes.Ldloc;
                                    currentInstruction.Operand = countersVariable;
                                }

                                var optIdx = currentInstructionIndex;
                                var indexValue = currentSequencePoint.StartLine - 1;
                                fileMetadata.Lines.Add(currentSequencePoint.StartLine);

                                switch (_coverageMode)
                                {
                                    case CoverageMode.LineCallCount:
                                        // Increments items in the counters array (to have the number of times a line was executed)
                                        if (countersVariable is null)
                                        {
                                            if (indexValue == 1)
                                            {
                                                currentInstruction.OpCode = OpCodes.Ldc_I4_4;
                                                currentInstruction.Operand = null;
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }
                                            else if (indexValue > 1)
                                            {
                                                currentInstruction.OpCode = OpCodes.Ldc_I4;
                                                currentInstruction.Operand = indexValue;
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Conv_I));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_4));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Mul));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }

                                            if (indexValue == 0)
                                            {
                                                currentInstruction.OpCode = OpCodes.Dup;
                                                currentInstruction.Operand = null;
                                            }
                                            else
                                            {
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Dup));
                                            }

                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldind_I4));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_1));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Stind_I4));
                                        }
                                        else
                                        {
                                            if (indexValue == 1)
                                            {
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_4));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }
                                            else if (indexValue > 1)
                                            {
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4, indexValue));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Conv_I));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_4));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Mul));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }

                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Dup));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldind_I4));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_1));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Stind_I4));
                                        }

                                        break;
                                    case CoverageMode.LineExecution:
                                        // Set 1 to items in the counters pointer (to check if the line was executed or not)
                                        if (countersVariable is null)
                                        {
                                            if (indexValue == 1)
                                            {
                                                currentInstruction.OpCode = OpCodes.Ldc_I4_1;
                                                currentInstruction.Operand = null;
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }
                                            else if (indexValue > 1)
                                            {
                                                currentInstruction.OpCode = OpCodes.Ldc_I4;
                                                currentInstruction.Operand = indexValue;
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }

                                            if (indexValue == 0)
                                            {
                                                currentInstruction.OpCode = OpCodes.Ldc_I4_1;
                                                currentInstruction.Operand = null;
                                            }
                                            else
                                            {
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_1));
                                            }

                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Stind_I1));
                                        }
                                        else
                                        {
                                            if (indexValue == 1)
                                            {
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_1));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }
                                            else if (indexValue > 1)
                                            {
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4, indexValue));
                                                instructions.Insert(++optIdx, Instruction.Create(OpCodes.Add));
                                            }

                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Ldc_I4_1));
                                            instructions.Insert(++optIdx, Instruction.Create(OpCodes.Stind_I1));
                                        }

                                        break;
                                }

                                instructions.Insert(++optIdx, currentInstructionClone);
                            }

                            isDirty = true;
                        }
                    }
                }

                // ****************************************************************************************************
                // Module metadata: Files field
                var fileCoverageMetadataTypeDefinition = datadogTracerAssembly.MainModule.GetType(typeof(FileCoverageMetadata).FullName);
                var fileCoverageMetadataTypeReference = module.ImportReference(fileCoverageMetadataTypeDefinition);
                var moduleCoverageMetadataImplFileMetadataField = new FieldReference("Files", new ArrayType(fileCoverageMetadataTypeReference), moduleCoverageMetadataTypeReference);
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, fileDictionaryIndex.Count));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Newarr, fileCoverageMetadataTypeReference));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stfld, moduleCoverageMetadataImplFileMetadataField));

                // Going through the fileDictionaryIndex to add the FileMetadata to the Files field
                var moduleCoverageMetadataImplFileMetadataCtor = new MethodReference(".ctor", module.TypeSystem.Void, fileCoverageMetadataTypeReference)
                {
                    HasThis = true,
                    Parameters =
                    {
                        new ParameterDefinition(module.TypeSystem.String),
                        new ParameterDefinition(module.TypeSystem.Int32),
                        new ParameterDefinition(module.TypeSystem.Int32),
                        new ParameterDefinition(new ArrayType(module.TypeSystem.Byte))
                    }
                };

                var fileOffset = 0;
                var fileBitmapBuffer = stackalloc byte[512];
                foreach (var fileMetadata in fileDictionaryIndex.Values.OrderBy(v => v.Index))
                {
                    var maxLine = fileMetadata.MaxLine;
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldfld, moduleCoverageMetadataImplFileMetadataField));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, fileMetadata.Index));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldstr, fileMetadata.FileName));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, fileOffset));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, maxLine));

                    // Create file bitmap
                    var fileBitmapSize = FileBitmap.GetSize(maxLine);
                    using var fileBitmap = fileBitmapSize <= 512 ? new FileBitmap(fileBitmapBuffer, fileBitmapSize) : new FileBitmap(new byte[fileBitmapSize]);
                    foreach (var line in fileMetadata.Lines)
                    {
                        fileBitmap.Set(line);
                    }

                    // Create bitmap array and set the values
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, fileBitmap.Size));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Newarr, module.TypeSystem.Byte));
                    var arrayItem = 0;
                    foreach (var bitmapItem in fileBitmap)
                    {
                        lstMetadataInstructions.Add(Instruction.Create(OpCodes.Dup));
                        lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, arrayItem++));
                        lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)bitmapItem));
                        lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stelem_I1));
                    }

                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Newobj, moduleCoverageMetadataImplFileMetadataCtor));
                    lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stelem_Any, fileCoverageMetadataTypeReference));
                    fileOffset += maxLine;
                }

                var moduleCoverageMetadataImplFileTotalLinesField = new FieldReference("TotalLines", module.TypeSystem.Int32, moduleCoverageMetadataTypeReference);
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)fileOffset));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stfld, moduleCoverageMetadataImplFileTotalLinesField));

                var moduleCoverageMetadataImplFileCoverageModeField = new FieldReference("CoverageMode", module.TypeSystem.Int32, moduleCoverageMetadataTypeReference);
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)_coverageMode));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stfld, moduleCoverageMetadataImplFileCoverageModeField));

                // ****************************************************************************************************
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ret));

                // Copy metadata instructions to the .ctor
                foreach (var instruction in lstMetadataInstructions)
                {
                    moduleCoverageMetadataImplCtor.Body.Instructions.Add(instruction);
                }

                module.Types.Add(moduleCoverageMetadataImplTypeDef);

                // Change attributes to drop native bits
                if ((module.Attributes & ModuleAttributes.ILLibrary) == ModuleAttributes.ILLibrary)
                {
                    module.Architecture = TargetArchitecture.I386;
                    module.Attributes &= ~ModuleAttributes.ILLibrary;
                    module.Attributes |= ModuleAttributes.ILOnly;
                }

                // Save assembly if we modify it successfully
                if (isDirty)
                {
                    var coveredAssemblyAttributeTypeReference = module.ImportReference(datadogTracerAssembly.MainModule.GetType(typeof(CoveredAssemblyAttribute).FullName));
                    assemblyDefinition.CustomAttributes.Add(new CustomAttribute(new MethodReference(".ctor", module.TypeSystem.Void, coveredAssemblyAttributeTypeReference)
                    {
                        HasThis = true
                    }));

                    _logger.Debug($"Saving assembly: {_assemblyFilePath}");

                    // Create backup for dll and pdb and copy the Datadog.Trace assembly
                    var tracerAssemblyLocation = CopyRequiredAssemblies(assemblyDefinition, tracerTarget);
                    customResolver.SetTracerAssemblyLocation(tracerAssemblyLocation);

                    assemblyDefinition.Write(new WriterParameters
                    {
                        WriteSymbols = true,
                        StrongNameKeyBlob = _strongNameKeyBlob
                    });
                }

                _logger.Debug($"Done: {_assemblyFilePath} [Modified:{isDirty}]");
            }
            catch (SymbolsNotFoundException)
            {
                Ci.Coverage.Exceptions.PdbNotFoundException.Throw();
            }
            catch (SymbolsNotMatchingException)
            {
                Ci.Coverage.Exceptions.PdbNotFoundException.Throw();
            }
        }

        private static void RemoveShortOpCodes(Instruction instruction)
        {
            if (instruction.OpCode == OpCodes.Br_S) { instruction.OpCode = OpCodes.Br; }
            if (instruction.OpCode == OpCodes.Brfalse_S) { instruction.OpCode = OpCodes.Brfalse; }
            if (instruction.OpCode == OpCodes.Brtrue_S) { instruction.OpCode = OpCodes.Brtrue; }
            if (instruction.OpCode == OpCodes.Leave_S) { instruction.OpCode = OpCodes.Leave; }
            if (instruction.OpCode == OpCodes.Blt_S) { instruction.OpCode = OpCodes.Blt; }
            if (instruction.OpCode == OpCodes.Blt_Un_S) { instruction.OpCode = OpCodes.Blt_Un; }
            if (instruction.OpCode == OpCodes.Ble_S) { instruction.OpCode = OpCodes.Ble; }
            if (instruction.OpCode == OpCodes.Ble_Un_S) { instruction.OpCode = OpCodes.Ble_Un; }
            if (instruction.OpCode == OpCodes.Bgt_S) { instruction.OpCode = OpCodes.Bgt; }
            if (instruction.OpCode == OpCodes.Bgt_Un_S) { instruction.OpCode = OpCodes.Bgt_Un; }
            if (instruction.OpCode == OpCodes.Bge_S) { instruction.OpCode = OpCodes.Bge; }
            if (instruction.OpCode == OpCodes.Bge_Un_S) { instruction.OpCode = OpCodes.Bge_Un; }
            if (instruction.OpCode == OpCodes.Beq_S) { instruction.OpCode = OpCodes.Beq; }
            if (instruction.OpCode == OpCodes.Bne_Un_S) { instruction.OpCode = OpCodes.Bne_Un; }
        }

        private static Instruction CloneInstruction(Instruction instruction)
        {
            return instruction.Operand switch
            {
                null => Instruction.Create(instruction.OpCode),
                string strOp => Instruction.Create(instruction.OpCode, strOp),
                int intOp => Instruction.Create(instruction.OpCode, intOp),
                long lngOp => Instruction.Create(instruction.OpCode, lngOp),
                byte byteOp => Instruction.Create(instruction.OpCode, byteOp),
                sbyte sbyteOp => Instruction.Create(instruction.OpCode, sbyteOp),
                double dblOp => Instruction.Create(instruction.OpCode, dblOp),
                FieldReference fRefOp => Instruction.Create(instruction.OpCode, fRefOp),
                MethodReference mRefOp => Instruction.Create(instruction.OpCode, mRefOp),
                CallSite callOp => Instruction.Create(instruction.OpCode, callOp),
                Instruction instOp => Instruction.Create(instruction.OpCode, instOp),
                Instruction[] instsOp => Instruction.Create(instruction.OpCode, instsOp),
                VariableDefinition vDefOp => Instruction.Create(instruction.OpCode, vDefOp),
                ParameterDefinition pDefOp => Instruction.Create(instruction.OpCode, pDefOp),
                TypeReference tRefOp => Instruction.Create(instruction.OpCode, tRefOp),
                float sOp => Instruction.Create(instruction.OpCode, sOp),
                _ => throw new Exception($"Instruction: {instruction.OpCode} cannot be cloned.")
            };
        }

        private string GetDatadogTracer(TracerTarget tracerTarget)
        {
            // Get the Datadog.Trace path

            if (string.IsNullOrEmpty(_settings.TracerHome))
            {
                // If tracer home is empty then we try to load the Datadog.Trace.dll in the current folder.
                return "Datadog.Trace.dll";
            }

            var targetFolder = "net461";
            switch (tracerTarget)
            {
                case TracerTarget.Net461:
                    targetFolder = "net461";
                    break;
                case TracerTarget.Netstandard20:
                    targetFolder = "netstandard2.0";
                    break;
                case TracerTarget.Netcoreapp31:
                    targetFolder = "netcoreapp3.1";
                    break;
                case TracerTarget.Net60:
                    targetFolder = "net6.0";
                    break;
            }

            return Path.Combine(_settings.TracerHome, targetFolder, "Datadog.Trace.dll");
        }

        private string CopyRequiredAssemblies(AssemblyDefinition assemblyDefinition, TracerTarget tracerTarget)
        {
            try
            {
                // Get the Datadog.Trace path
                string targetFolder = "net461";
                switch (tracerTarget)
                {
                    case TracerTarget.Net461:
                        targetFolder = "net461";
                        break;
                    case TracerTarget.Netstandard20:
                        targetFolder = "netstandard2.0";
                        break;
                    case TracerTarget.Netcoreapp31:
                        targetFolder = "netcoreapp3.1";
                        break;
                    case TracerTarget.Net60:
                        targetFolder = "net6.0";
                        break;
                }

                var datadogTraceDllPath = Path.Combine(_settings.TracerHome, targetFolder, "Datadog.Trace.dll");
                var datadogTracePdbPath = Path.Combine(_settings.TracerHome, targetFolder, "Datadog.Trace.pdb");

                // Global lock for copying the Datadog.Trace assembly to the output folder
                lock (PadLock)
                {
                    // Copying the Datadog.Trace assembly
                    var assembly = typeof(Tracer).Assembly;
                    var assemblyLocation = assembly.Location;
                    var outputAssemblyDllLocation = Path.Combine(Path.GetDirectoryName(_assemblyFilePath) ?? string.Empty, Path.GetFileName(assemblyLocation));
                    var outputAssemblyPdbLocation = Path.Combine(Path.GetDirectoryName(_assemblyFilePath) ?? string.Empty, Path.GetFileNameWithoutExtension(assemblyLocation) + ".pdb");
                    if (!File.Exists(outputAssemblyDllLocation) ||
                        assembly.GetName().Version >= AssemblyName.GetAssemblyName(outputAssemblyDllLocation).Version)
                    {
                        _logger.Debug($"CopyRequiredAssemblies: Writing ({tracerTarget}) {outputAssemblyDllLocation} ...");

                        if (File.Exists(datadogTraceDllPath))
                        {
                            File.Copy(datadogTraceDllPath, outputAssemblyDllLocation, true);
                        }

                        if (File.Exists(datadogTracePdbPath))
                        {
                            File.Copy(datadogTracePdbPath, outputAssemblyPdbLocation, true);
                        }
                    }

                    HasTracerAssemblyCopied = true;
                    return outputAssemblyDllLocation;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return string.Empty;
        }

        internal TracerTarget GetTracerTarget(AssemblyDefinition assemblyDefinition)
        {
            foreach (var customAttribute in assemblyDefinition.CustomAttributes)
            {
                if (customAttribute.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
                {
                    var targetValue = (string)customAttribute.ConstructorArguments[0].Value;
                    _logger.Debug($"GetTracerTarget: TargetValue detected: {targetValue}");

                    if (targetValue.Contains(".NETFramework,Version="))
                    {
                        _logger.Debug($"GetTracerTarget: Returning TracerTarget.Net461 from {targetValue}");
                        return TracerTarget.Net461;
                    }

                    var matchTarget = NetCorePattern.Match(targetValue);
                    if (matchTarget.Success)
                    {
                        _logger.Debug($"GetTracerTarget: NetCoreApp pattern detected.");
                        var versionValue = matchTarget.Groups[1].Value;
                        _logger.Debug($"GetTracerTarget: Version value {versionValue}");
                        if (float.TryParse(versionValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var version))
                        {
                            _logger.Debug($"GetTracerTarget: Parse result {version}");

                            if (version >= 2.0 && version <= 3.0)
                            {
                                _logger.Debug($"GetTracerTarget: Returning TracerTarget.Netstandard20 from {targetValue}");
                                return TracerTarget.Netstandard20;
                            }

                            if (version > 3.0 && version <= 5.0)
                            {
                                _logger.Debug($"GetTracerTarget: Returning TracerTarget.Netcoreapp31 from {targetValue}");
                                return TracerTarget.Netcoreapp31;
                            }

                            if (version > 5.0)
                            {
                                _logger.Debug($"GetTracerTarget: Returning TracerTarget.Net60 from {targetValue}");
                                return TracerTarget.Net60;
                            }
                        }
                    }
                }
            }

            var coreLibrary = assemblyDefinition.MainModule.TypeSystem.CoreLibrary;
            _logger.Debug($"GetTracerTarget: Calculating TracerTarget from: {((AssemblyNameReference)coreLibrary).FullName} in {assemblyDefinition.FullName}");
            switch (coreLibrary.Name)
            {
                case "netstandard" when coreLibrary is AssemblyNameReference coreAsmRef && coreAsmRef.Version.Major == 2:
                case "System.Private.CoreLib":
                case "System.Runtime":
                    _logger.Debug("GetTracerTarget: Returning TracerTarget.Netstandard20");
                    return TracerTarget.Netstandard20;
            }

            _logger.Debug("GetTracerTarget: Returning TracerTarget.Net461");
            return TracerTarget.Net461;
        }

        private class CustomResolver : BaseAssemblyResolver
        {
            private readonly ICollectorLogger _logger;
            private DefaultAssemblyResolver _defaultResolver;
            private string _tracerAssemblyLocation;
            private string _assemblyFilePath;

            public CustomResolver(ICollectorLogger logger, string assemblyFilePath)
            {
                _tracerAssemblyLocation = string.Empty;
                _logger = logger;
                _assemblyFilePath = assemblyFilePath;
                _defaultResolver = new DefaultAssemblyResolver();
            }

            public override AssemblyDefinition? Resolve(AssemblyNameReference name)
            {
                AssemblyDefinition? assembly = null;
                try
                {
                    assembly = _defaultResolver.Resolve(name);
                }
                catch (AssemblyResolutionException arEx)
                {
                    var tracerAssemblyName = TracerAssembly.GetName();
                    if (name.Name == tracerAssemblyName.Name && name.Version == tracerAssemblyName.Version)
                    {
                        var cAssemblyLocation = !string.IsNullOrEmpty(_tracerAssemblyLocation) ? _tracerAssemblyLocation : TracerAssembly.Location;
                        try
                        {
                            assembly = AssemblyDefinition.ReadAssembly(cAssemblyLocation);
                        }
                        catch (Exception innerAssemblyException)
                        {
                            _logger.Error(innerAssemblyException, $"Error reading the tracer assembly: {cAssemblyLocation}");
                            throw;
                        }
                    }
                    else
                    {
                        var folder = Path.GetDirectoryName(_assemblyFilePath);
                        var pathTest = Path.Combine(folder ?? string.Empty, name.Name + ".dll");
                        _logger.Debug($"Looking for: {pathTest}");
                        if (File.Exists(pathTest))
                        {
                            try
                            {
                                return AssemblyDefinition.ReadAssembly(pathTest);
                            }
                            catch (Exception innerAssemblyException)
                            {
                                _logger.Error(innerAssemblyException, $"Error reading the assembly: {pathTest}");
                                throw;
                            }
                        }

                        if (name.Name == "mscorlib")
                        {
                            var path = GetMscorlibBasePath(name.Version);
                            var file = Path.Combine(path, "mscorlib.dll");
                            if (File.Exists(file))
                            {
                                return AssemblyDefinition.ReadAssembly(file);
                            }
                        }

                        _logger.Error(arEx, $"Error in the Custom Resolver processing '{_assemblyFilePath}' for: {name.FullName}");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error in the custom resolver when trying to resolve assembly: {name.FullName}");
                }

                return assembly;
            }

            public void SetTracerAssemblyLocation(string assemblyLocation)
            {
                _tracerAssemblyLocation = assemblyLocation;
            }

            private string GetMscorlibBasePath(Version version)
            {
                string? GetSubFolderForVersion()
                    => version.Major switch
                    {
                        1 when version.MajorRevision == 3300 => "v1.0.3705",
                        1 => "v1.1.4322",
                        2 => "v2.0.50727",
                        4 => "v4.0.30319",
                        _ => throw new NotSupportedException("Version not supported: " + version),
                    };

                var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET");
                string[] frameworkPaths =
                [
                    Path.Combine(rootPath, "Framework"),
                    Path.Combine(rootPath, "Framework64")
                ];

                var folder = GetSubFolderForVersion();

                if (folder != null)
                {
                    foreach (var path in frameworkPaths)
                    {
                        var basePath = Path.Combine(path, folder);
                        if (Directory.Exists(basePath))
                        {
                            return basePath;
                        }
                    }
                }

                throw new NotSupportedException("Version not supported: " + version);
            }
        }

#pragma warning disable SA1201
        private struct FileMetadata
        {
            public FileMetadata(int index, string fileName)
            {
                Index = index;
                FileName = fileName;
                Lines = new HashSet<int>();
            }

            public int Index { get; private set; }

            public string FileName { get; }

            public HashSet<int> Lines { get; }

            public int MaxLine => Lines.Max();
        }
    }
}
