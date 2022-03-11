// <copyright file="AssemblyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Attributes;
using Datadog.Trace.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

[assembly: AvoidCoverage]

#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    internal class AssemblyProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AssemblyProcessor));
        private static readonly ConstructorInfo CoveredAssemblyAttributeTypeCtor = typeof(CoveredAssemblyAttribute).GetConstructors()[0];
        private static readonly MethodInfo ReportTryGetScopeMethodInfo = typeof(CoverageReporter).GetMethod("TryGetScope")!;
        private static readonly MethodInfo ScopeReportMethodInfo = typeof(CoverageScope).GetMethod("Report", new[] { typeof(ulong) })!;
        private static readonly MethodInfo ScopeReport2MethodInfo = typeof(CoverageScope).GetMethod("Report", new[] { typeof(ulong), typeof(ulong) })!;
        private static readonly Assembly TracerAssembly = typeof(CoverageReporter).Assembly;

        private readonly CIVisibilitySettings? _ciVisibilitySettings;
        private readonly ICollectorLogger _logger;
        private readonly string _assemblyFilePath;
        private readonly string _pdbFilePath;
        private readonly string _assemblyFilePathBackup;
        private readonly string _pdbFilePathBackup;

        private byte[]? _strongNameKeyBlob;

        public AssemblyProcessor(string filePath, ICollectorLogger? logger = null, CIVisibilitySettings? ciVisibilitySettings = null)
        {
            _logger = logger ?? new ConsoleCollectorLogger();
            _ciVisibilitySettings = ciVisibilitySettings;
            _assemblyFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _pdbFilePath = Path.ChangeExtension(filePath, ".pdb");
            _assemblyFilePathBackup = Path.ChangeExtension(filePath, Path.GetExtension(filePath) + "Backup");
            _pdbFilePathBackup = Path.ChangeExtension(filePath, ".pdbBackup");

            if (!File.Exists(_assemblyFilePath))
            {
                throw new FileNotFoundException($"Assembly not found in path: {_assemblyFilePath}");
            }

            if (!File.Exists(_pdbFilePath))
            {
                Ci.Coverage.Exceptions.PdbNotFoundException.Throw();
            }
        }

        public string FilePath => _assemblyFilePath;

        public void Process()
        {
            try
            {
                _logger.Debug($"Processing: {_assemblyFilePath}");

                var customResolver = new CustomResolver(_logger);
                using var assemblyDefinition = AssemblyDefinition.ReadAssembly(_assemblyFilePath, new ReaderParameters
                {
                    ReadSymbols = true,
                    ReadWrite = true,
                    AssemblyResolver = customResolver,
                });

                if (assemblyDefinition.CustomAttributes.Any(cAttr =>
                    cAttr.Constructor.DeclaringType.Name == nameof(AvoidCoverageAttribute)))
                {
                    _logger.Debug($"Assembly: {FilePath}, ignored.");
                    return;
                }

                if (assemblyDefinition.CustomAttributes.Any(cAttr =>
                    cAttr.Constructor.DeclaringType.Name == nameof(CoveredAssemblyAttribute)))
                {
                    _logger.Debug($"Assembly: {FilePath}, already have coverage information.");
                    return;
                }

                // Gets the Datadog.Trace target framework
                var tracerTarget = GetTracerTarget(assemblyDefinition);

                if (assemblyDefinition.Name.HasPublicKey)
                {
                    _logger.Debug($"Assembly: {FilePath} is signed.");

                    var snkFilePath = _ciVisibilitySettings?.CodeCoverageSnkFilePath;
                    _logger.Debug($"Assembly: {FilePath} loading .snk file: {snkFilePath}.");
                    if (!string.IsNullOrWhiteSpace(snkFilePath) && File.Exists(snkFilePath))
                    {
                        _logger.Debug($"{snkFilePath} exists.");
                        _strongNameKeyBlob = File.ReadAllBytes(snkFilePath);
                        _logger.Debug($"{snkFilePath} loaded.");
                    }
                    else if (tracerTarget == TracerTarget.Net461)
                    {
                        _logger.Debug($"Assembly: {FilePath}, is a net461 signed assembly, a .snk file is required ({Configuration.ConfigurationKeys.CIVisibility.CodeCoverageSnkFile} environment variable).");
                        return;
                    }
                }

                bool isDirty = false;
                ulong totalMethods = 0;
                ulong totalInstructions = 0;

                // Process all modules in the assembly
                foreach (ModuleDefinition module in assemblyDefinition.Modules)
                {
                    _logger.Debug($"Processing module: {module.Name}");

                    // Process all types defined in the module
                    foreach (TypeDefinition moduleType in module.GetTypes())
                    {
                        if (moduleType.CustomAttributes.Any(cAttr =>
                            cAttr.Constructor.DeclaringType.Name == nameof(AvoidCoverageAttribute)))
                        {
                            continue;
                        }

                        _logger.Debug($"\t{moduleType.FullName}");

                        // Process all Methods in the type
                        foreach (var moduleTypeMethod in moduleType.Methods)
                        {
                            if (moduleTypeMethod.CustomAttributes.Any(cAttr =>
                                cAttr.Constructor.DeclaringType.Name == nameof(AvoidCoverageAttribute)))
                            {
                                continue;
                            }

                            if (moduleTypeMethod.DebugInformation is null || !moduleTypeMethod.DebugInformation.HasSequencePoints)
                            {
                                _logger.Debug($"\t\t[NO] {moduleTypeMethod.FullName}");
                                continue;
                            }

                            _logger.Debug($"\t\t[YES] {moduleTypeMethod.FullName}.");

                            totalMethods++;

                            // Extract body from the method
                            if (moduleTypeMethod.HasBody)
                            {
                                var methodBody = moduleTypeMethod.Body;
                                var instructions = methodBody.Instructions;
                                var instructionsOriginalLength = instructions.Count;
                                var sequencePoints = moduleTypeMethod.DebugInformation.SequencePoints;
                                var sequencePointsOriginalLength = sequencePoints.Count;
                                string? methodFileName = null;
                                uint localInstructions = 0;

                                // Step 1 - Clone instructions
                                for (var i = 0; i < instructionsOriginalLength; i++)
                                {
                                    instructions.Add(CloneInstruction(instructions[i]));
                                }

                                // Step 2 - Fix jumps in cloned instructions
                                for (var i = 0; i < instructionsOriginalLength; i++)
                                {
                                    var currentInstruction = instructions[i];

                                    if (currentInstruction.Operand is Instruction jmpTargetInstruction)
                                    {
                                        // Normal jump

                                        // Get index of the jump target
                                        var jmpTargetInstructionIndex = instructions.IndexOf(jmpTargetInstruction);

                                        // Modify the clone instruction with the cloned jump target
                                        var clonedInstruction = instructions[i + instructionsOriginalLength];
                                        RemoveShortOpCodes(clonedInstruction);
                                        clonedInstruction.Operand = instructions[jmpTargetInstructionIndex + instructionsOriginalLength];
                                    }
                                    else if (currentInstruction.Operand is Instruction[] jmpTargetInstructions)
                                    {
                                        // Switch jumps

                                        // Create a new array of instructions with the cloned jump targets
                                        var newJmpTargetInstructions = new Instruction[jmpTargetInstructions.Length];
                                        for (var j = 0; j < jmpTargetInstructions.Length; j++)
                                        {
                                            newJmpTargetInstructions[j] = instructions[instructions.IndexOf(jmpTargetInstructions[j]) + instructionsOriginalLength];
                                        }

                                        // Modify the clone instruction with the cloned jump target
                                        var clonedInstruction = instructions[i + instructionsOriginalLength];
                                        RemoveShortOpCodes(clonedInstruction);
                                        clonedInstruction.Operand = newJmpTargetInstructions;
                                    }
                                }

                                // Step 3 - Clone exception handlers
                                if (methodBody.HasExceptionHandlers)
                                {
                                    var exceptionHandlers = methodBody.ExceptionHandlers;
                                    var exceptionHandlersOrignalLength = exceptionHandlers.Count;

                                    for (var i = 0; i < exceptionHandlersOrignalLength; i++)
                                    {
                                        var currentExceptionHandler = exceptionHandlers[i];
                                        var clonedExceptionHandler = new ExceptionHandler(currentExceptionHandler.HandlerType);
                                        clonedExceptionHandler.CatchType = currentExceptionHandler.CatchType;

                                        if (currentExceptionHandler.TryStart is not null)
                                        {
                                            clonedExceptionHandler.TryStart = instructions[instructions.IndexOf(currentExceptionHandler.TryStart) + instructionsOriginalLength];
                                        }

                                        if (currentExceptionHandler.TryEnd is not null)
                                        {
                                            clonedExceptionHandler.TryEnd = instructions[instructions.IndexOf(currentExceptionHandler.TryEnd) + instructionsOriginalLength];
                                        }

                                        if (currentExceptionHandler.HandlerStart is not null)
                                        {
                                            clonedExceptionHandler.HandlerStart = instructions[instructions.IndexOf(currentExceptionHandler.HandlerStart) + instructionsOriginalLength];
                                        }

                                        if (currentExceptionHandler.HandlerEnd is not null)
                                        {
                                            clonedExceptionHandler.HandlerEnd = instructions[instructions.IndexOf(currentExceptionHandler.HandlerEnd) + instructionsOriginalLength];
                                        }

                                        if (currentExceptionHandler.FilterStart is not null)
                                        {
                                            clonedExceptionHandler.FilterStart = instructions[instructions.IndexOf(currentExceptionHandler.FilterStart) + instructionsOriginalLength];
                                        }

                                        methodBody.ExceptionHandlers.Add(clonedExceptionHandler);
                                    }
                                }

                                // Step 4 - Clone sequence points
                                List<Tuple<Instruction, SequencePoint>> clonedInstructionsWithOriginalSequencePoint = new List<Tuple<Instruction, SequencePoint>>();
                                for (var i = 0; i < sequencePointsOriginalLength; i++)
                                {
                                    var currentSequencePoint = sequencePoints[i];
                                    var currentInstruction = instructions.FirstOrDefault(i => i.Offset == currentSequencePoint.Offset);
                                    var clonedInstruction = instructions[instructions.IndexOf(currentInstruction) + instructionsOriginalLength];

                                    if (!currentSequencePoint.IsHidden)
                                    {
                                        totalInstructions++;
                                        localInstructions++;
                                        clonedInstructionsWithOriginalSequencePoint.Add(Tuple.Create(clonedInstruction, currentSequencePoint));
                                    }

                                    var clonedSequencePoint = new SequencePoint(clonedInstruction, currentSequencePoint.Document);
                                    clonedSequencePoint.StartLine = currentSequencePoint.StartLine;
                                    clonedSequencePoint.StartColumn = currentSequencePoint.StartColumn;
                                    clonedSequencePoint.EndLine = currentSequencePoint.EndLine;
                                    clonedSequencePoint.EndColumn = currentSequencePoint.EndColumn;
                                    sequencePoints.Add(clonedSequencePoint);

                                    if (string.IsNullOrEmpty(methodFileName))
                                    {
                                        methodFileName = currentSequencePoint.Document.Url;
                                    }
                                }

                                var clonedInstructions = instructions.Skip(instructionsOriginalLength).ToList();
                                var clonedInstructionsLength = clonedInstructions.Count;

                                // Step 6 - Modify local var to add the Coverage Scope instance.
                                var coverageScopeVariable = new VariableDefinition(module.ImportReference(typeof(CoverageScope)));
                                methodBody.Variables.Add(coverageScopeVariable);

                                // Step 7 - Insert initial condition
                                var tryGetScopeMethodRef = module.ImportReference(ReportTryGetScopeMethodInfo);
                                instructions.Insert(0, Instruction.Create(OpCodes.Brtrue, instructions[instructionsOriginalLength]));
                                instructions.Insert(0, Instruction.Create(OpCodes.Call, tryGetScopeMethodRef));
                                instructions.Insert(0, Instruction.Create(OpCodes.Ldloca, coverageScopeVariable));
                                if (string.IsNullOrEmpty(methodFileName))
                                {
                                    methodFileName = "unknown";
                                }

                                instructions.Insert(0, Instruction.Create(OpCodes.Ldstr, methodFileName));

                                // Step 8 - Insert line reporter
                                var scopeReportMethodRef = module.ImportReference(ScopeReportMethodInfo);
                                var scopeReport2MethodRef = module.ImportReference(ScopeReport2MethodInfo);
                                for (var i = 0; i < clonedInstructionsWithOriginalSequencePoint.Count; i++)
                                {
                                    var currentItem = clonedInstructionsWithOriginalSequencePoint[i];
                                    var currentInstruction = currentItem.Item1;
                                    var currentSequencePoint = currentItem.Item2;

                                    var currentInstructionRange = ((ulong)(ushort)currentSequencePoint.StartLine << 48) |
                                                                  ((ulong)(ushort)currentSequencePoint.StartColumn << 32) |
                                                                  ((ulong)(ushort)currentSequencePoint.EndLine << 16) |
                                                                  ((ulong)(ushort)currentSequencePoint.EndColumn);

                                    var currentInstructionIndex = instructions.IndexOf(currentInstruction);
                                    var currentInstructionClone = CloneInstruction(currentInstruction);

                                    if (i < clonedInstructionsWithOriginalSequencePoint.Count - 1)
                                    {
                                        var nextItem = clonedInstructionsWithOriginalSequencePoint[i + 1];
                                        var nextInstruction = nextItem.Item1;

                                        // We check if the next instruction with sequence point is a NOP and is inmediatly after the current one.
                                        if (instructions.IndexOf(nextInstruction) - 1 == currentInstructionIndex &&
                                            (currentInstruction.OpCode == OpCodes.Nop || nextInstruction.OpCode == OpCodes.Nop) &&
                                            !methodBody.ExceptionHandlers.Any(eHandler =>
                                                eHandler.TryStart == nextInstruction ||
                                                eHandler.TryEnd == nextInstruction ||
                                                eHandler.HandlerStart == nextInstruction ||
                                                eHandler.HandlerEnd == nextInstruction ||
                                                eHandler.FilterStart == nextInstruction))
                                        {
                                            var nextSequencePoint = nextItem.Item2;

                                            var nextInstructionRange = ((ulong)(ushort)nextSequencePoint.StartLine << 48) |
                                                                       ((ulong)(ushort)nextSequencePoint.StartColumn << 32) |
                                                                       ((ulong)(ushort)nextSequencePoint.EndLine << 16) |
                                                                       ((ulong)(ushort)nextSequencePoint.EndColumn);

                                            var nextInstructionIndex = instructions.IndexOf(nextInstruction);

                                            currentInstruction.OpCode = OpCodes.Ldloca;
                                            currentInstruction.Operand = coverageScopeVariable;
                                            instructions.Insert(currentInstructionIndex + 1, Instruction.Create(OpCodes.Ldc_I8, (long)currentInstructionRange));
                                            instructions.Insert(currentInstructionIndex + 2, Instruction.Create(OpCodes.Ldc_I8, (long)nextInstructionRange));
                                            instructions.Insert(currentInstructionIndex + 3, Instruction.Create(OpCodes.Call, scopeReport2MethodRef));
                                            instructions.Insert(currentInstructionIndex + 4, currentInstructionClone);

                                            // We process both instructions in a single call.
                                            i++;
                                            continue;
                                        }
                                    }

                                    currentInstruction.OpCode = OpCodes.Ldloca;
                                    currentInstruction.Operand = coverageScopeVariable;
                                    instructions.Insert(currentInstructionIndex + 1, Instruction.Create(OpCodes.Ldc_I8, (long)currentInstructionRange));
                                    instructions.Insert(currentInstructionIndex + 2, Instruction.Create(OpCodes.Call, scopeReportMethodRef));
                                    instructions.Insert(currentInstructionIndex + 3, currentInstructionClone);
                                }

                                isDirty = true;
                            }
                        }
                    }

                    // Change attributes to drop native bits
                    if ((module.Attributes & ModuleAttributes.ILLibrary) == ModuleAttributes.ILLibrary)
                    {
                        module.Architecture = TargetArchitecture.I386;
                        module.Attributes &= ~ModuleAttributes.ILLibrary;
                        module.Attributes |= ModuleAttributes.ILOnly;
                    }
                }

                // Save assembly if we modify it successfully
                if (isDirty)
                {
                    var coveredAssemblyAttributeTypeCtorRef = assemblyDefinition.MainModule.ImportReference(CoveredAssemblyAttributeTypeCtor);
                    var coveredAssemblyAttribute = new CustomAttribute(coveredAssemblyAttributeTypeCtorRef);
                    coveredAssemblyAttribute.ConstructorArguments.Add(new CustomAttributeArgument(assemblyDefinition.MainModule.ImportReference(typeof(ulong)), totalMethods));
                    coveredAssemblyAttribute.ConstructorArguments.Add(new CustomAttributeArgument(assemblyDefinition.MainModule.ImportReference(typeof(ulong)), totalInstructions));
                    assemblyDefinition.CustomAttributes.Add(coveredAssemblyAttribute);

                    _logger.Debug($"Saving assembly: {_assemblyFilePath}");

                    // Create backup for dll and pdb and copy the Datadog.Trace assembly
                    var tracerAssemblyLocation = CreateBackupAndCopyRequiredAssemblies(assemblyDefinition, tracerTarget);
                    customResolver.SetTracerAssemblyLocation(tracerAssemblyLocation);

                    assemblyDefinition.Write(new WriterParameters
                    {
                        WriteSymbols = true,
                        StrongNameKeyBlob = _strongNameKeyBlob
                    });

                    _logger.Debug($"Done: {_assemblyFilePath}");
                }
            }
            catch (SymbolsNotFoundException)
            {
                Ci.Coverage.Exceptions.PdbNotFoundException.Throw();
            }
            catch (SymbolsNotMatchingException)
            {
                Ci.Coverage.Exceptions.PdbNotFoundException.Throw();
            }
            catch
            {
                Revert();
                throw;
            }
        }

        public void Revert()
        {
            try
            {
                if (File.Exists(_assemblyFilePathBackup))
                {
                    File.Copy(_assemblyFilePathBackup, _assemblyFilePath, true);
                    File.Copy(_pdbFilePathBackup, _pdbFilePath, true);
                    File.Delete(_assemblyFilePathBackup);
                    File.Delete(_pdbFilePathBackup);

                    var assemblyFilePathFileInfo = new FileInfo(_assemblyFilePath);
                    assemblyFilePathFileInfo.Attributes &= ~FileAttributes.Hidden;

                    var pdfFilePathFileInfo = new FileInfo(_pdbFilePath);
                    pdfFilePathFileInfo.Attributes &= ~FileAttributes.Hidden;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
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
            if (instruction.Operand is null)
            {
                return Instruction.Create(instruction.OpCode);
            }

            if (instruction.Operand is string strOp)
            {
                return Instruction.Create(instruction.OpCode, strOp);
            }

            if (instruction.Operand is int intOp)
            {
                return Instruction.Create(instruction.OpCode, intOp);
            }

            if (instruction.Operand is long lngOp)
            {
                return Instruction.Create(instruction.OpCode, lngOp);
            }

            if (instruction.Operand is byte byteOp)
            {
                return Instruction.Create(instruction.OpCode, byteOp);
            }

            if (instruction.Operand is sbyte sbyteOp)
            {
                return Instruction.Create(instruction.OpCode, sbyteOp);
            }

            if (instruction.Operand is double dblOp)
            {
                return Instruction.Create(instruction.OpCode, dblOp);
            }

            if (instruction.Operand is FieldReference fRefOp)
            {
                return Instruction.Create(instruction.OpCode, fRefOp);
            }

            if (instruction.Operand is MethodReference mRefOp)
            {
                return Instruction.Create(instruction.OpCode, mRefOp);
            }

            if (instruction.Operand is CallSite callOp)
            {
                return Instruction.Create(instruction.OpCode, callOp);
            }

            if (instruction.Operand is Instruction instOp)
            {
                return Instruction.Create(instruction.OpCode, instOp);
            }

            if (instruction.Operand is Instruction[] instsOp)
            {
                return Instruction.Create(instruction.OpCode, instsOp);
            }

            if (instruction.Operand is VariableDefinition vDefOp)
            {
                return Instruction.Create(instruction.OpCode, vDefOp);
            }

            if (instruction.Operand is ParameterDefinition pDefOp)
            {
                return Instruction.Create(instruction.OpCode, pDefOp);
            }

            if (instruction.Operand is TypeReference tRefOp)
            {
                return Instruction.Create(instruction.OpCode, tRefOp);
            }

            if (instruction.Operand is float sOp)
            {
                return Instruction.Create(instruction.OpCode, sOp);
            }

            throw new Exception($"Instruction: {instruction.OpCode} cannot be cloned.");
        }

        private string CreateBackupAndCopyRequiredAssemblies(AssemblyDefinition assemblyDefinition, TracerTarget tracerTarget)
        {
            try
            {
                // Create backup files and set the hidden attribute.
                File.Copy(_assemblyFilePath, _assemblyFilePathBackup, true);
                File.Copy(_pdbFilePath, _pdbFilePathBackup, true);

                // Hide backup files
                var assemblyFilePathBackupFileInfo = new FileInfo(_assemblyFilePathBackup);
                assemblyFilePathBackupFileInfo.Attributes |= FileAttributes.Hidden;

                var pdfFilePathBackupFileInfo = new FileInfo(_pdbFilePathBackup);
                pdfFilePathBackupFileInfo.Attributes |= FileAttributes.Hidden;

                // Get the Datadog.Trace stream
                Stream? datadogTraceDllStream = null;
                Stream? datadogTracePdbStream = null;
                var currentAssembly = typeof(AssemblyProcessor).Assembly;
                switch (tracerTarget)
                {
                    case TracerTarget.Net461:
                        datadogTraceDllStream = currentAssembly.GetManifestResourceStream("Datadog.Trace.Coverage.collector.net461.Datadog.Trace.dll");
                        datadogTracePdbStream = currentAssembly.GetManifestResourceStream("Datadog.Trace.Coverage.collector.net461.Datadog.Trace.pdb");
                        break;
                    case TracerTarget.Netstandard20:
                        datadogTraceDllStream = currentAssembly.GetManifestResourceStream("Datadog.Trace.Coverage.collector.netstandard2._0.Datadog.Trace.dll");
                        datadogTracePdbStream = currentAssembly.GetManifestResourceStream("Datadog.Trace.Coverage.collector.netstandard2._0.Datadog.Trace.pdb");
                        break;
                    case TracerTarget.Netcoreapp31:
                        datadogTraceDllStream = currentAssembly.GetManifestResourceStream("Datadog.Trace.Coverage.collector.netcoreapp3._1.Datadog.Trace.dll");
                        datadogTracePdbStream = currentAssembly.GetManifestResourceStream("Datadog.Trace.Coverage.collector.netcoreapp3._1.Datadog.Trace.pdb");
                        break;
                }

                // Copying the Datadog.Trace assembly
                var assembly = typeof(Tracer).Assembly;
                var assemblyLocation = assembly.Location;
                var outputAssemblyDllLocation = Path.Combine(Path.GetDirectoryName(_assemblyFilePath) ?? string.Empty, Path.GetFileName(assemblyLocation));
                var outputAssemblyPdbLocation = Path.Combine(Path.GetDirectoryName(_assemblyFilePath) ?? string.Empty, Path.GetFileNameWithoutExtension(assemblyLocation) + ".pdb");
                if (!File.Exists(outputAssemblyDllLocation) ||
                    assembly.GetName().Version >= AssemblyName.GetAssemblyName(outputAssemblyDllLocation).Version)
                {
                    _logger.Debug($"GetTracerTarget: Writing {outputAssemblyDllLocation} ...");
                    if (datadogTraceDllStream is not null)
                    {
                        using var dllFileStream = new FileStream(outputAssemblyDllLocation, FileMode.Create, FileAccess.Write, FileShare.Read);
                        datadogTraceDllStream.CopyTo(dllFileStream);
                    }

                    if (datadogTracePdbStream is not null)
                    {
                        using var pdbFileStream = new FileStream(outputAssemblyPdbLocation, FileMode.Create, FileAccess.Write, FileShare.Read);
                        datadogTracePdbStream.CopyTo(pdbFileStream);
                    }
                }

                return outputAssemblyDllLocation;
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
                    if (targetValue.Contains(".NETFramework,Version="))
                    {
                        _logger.Debug($"GetTracerTarget: Returning TracerTarget.Net461 from {targetValue}");
                        return TracerTarget.Net461;
                    }

                    switch (targetValue)
                    {
                        case ".NETCoreApp,Version=v2.0":
                        case ".NETCoreApp,Version=v2.1":
                        case ".NETCoreApp,Version=v2.2":
                        case ".NETCoreApp,Version=v3.0":
                            _logger.Debug($"GetTracerTarget: Returning TracerTarget.Netstandard20 from {targetValue}");
                            return TracerTarget.Netstandard20;

                        case ".NETCoreApp,Version=v3.1":
                        case ".NETCoreApp,Version=v5.0":
                        case ".NETCoreApp,Version=v6.0":
                        case ".NETCoreApp,Version=v7.0":
                            _logger.Debug($"GetTracerTarget: Returning TracerTarget.Netcoreapp31 from {targetValue}");
                            return TracerTarget.Netcoreapp31;
                    }
                }
            }

            var coreLibrary = assemblyDefinition.MainModule.TypeSystem.CoreLibrary;
            _logger.Debug($"GetTracerTarget: Calculating TracerTarget from: {((AssemblyNameReference)coreLibrary).FullName}");
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

            public CustomResolver(ICollectorLogger logger)
            {
                _tracerAssemblyLocation = string.Empty;
                _logger = logger;
                _defaultResolver = new DefaultAssemblyResolver();
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                AssemblyDefinition assembly;
                try
                {
                    assembly = _defaultResolver.Resolve(name);
                }
                catch (AssemblyResolutionException ex)
                {
                    var tracerAssemblyName = TracerAssembly.GetName();
                    if (name.Name == tracerAssemblyName.Name && name.Version == tracerAssemblyName.Version)
                    {
                        if (!string.IsNullOrEmpty(_tracerAssemblyLocation))
                        {
                            assembly = AssemblyDefinition.ReadAssembly(_tracerAssemblyLocation);
                        }
                        else
                        {
                            assembly = AssemblyDefinition.ReadAssembly(TracerAssembly.Location);
                        }
                    }
                    else
                    {
                        _logger.Error(ex, $"Error in the Custom Resolver for: {name.FullName}");
                        throw;
                    }
                }

                return assembly;
            }

            public void SetTracerAssemblyLocation(string assemblyLocation)
            {
                _tracerAssemblyLocation = assemblyLocation;
            }
        }
    }
}
