// <copyright file="AssemblyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Datadog.Trace.Coverage.Collector
{
    internal class AssemblyProcessor
    {
        private static readonly object PadLock = new();
        private static readonly CultureInfo USCultureInfo = new("us-US");
        private static readonly Regex NetCorePattern = new(@".NETCoreApp,Version=v(\d.\d)", RegexOptions.Compiled);
        private static readonly ConstructorInfo CoveredAssemblyAttributeTypeCtor = typeof(CoveredAssemblyAttribute).GetConstructors()[0];
        private static readonly MethodInfo ReportTryGetScopeMethodInfo = typeof(CoverageReporter).GetMethod("TryGetScope")!;
        private static readonly MethodInfo ScopeReportMethodInfo = typeof(CoverageScope).GetMethod("Report", new[] { typeof(ulong) })!;
        private static readonly MethodInfo ScopeReport2MethodInfo = typeof(CoverageScope).GetMethod("Report", new[] { typeof(ulong), typeof(ulong) })!;
        private static readonly Assembly TracerAssembly = typeof(CoverageReporter).Assembly;

        private readonly CIVisibilitySettings? _ciVisibilitySettings;
        private readonly ICollectorLogger _logger;
        private readonly string _tracerHome;
        private readonly string _assemblyFilePath;
        private readonly string _pdbFilePath;

        private byte[]? _strongNameKeyBlob;

        public AssemblyProcessor(string filePath, string tracerHome, ICollectorLogger? logger = null, CIVisibilitySettings? ciVisibilitySettings = null)
        {
            _tracerHome = tracerHome;
            _logger = logger ?? new ConsoleCollectorLogger();
            _ciVisibilitySettings = ciVisibilitySettings;
            _assemblyFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _pdbFilePath = Path.ChangeExtension(filePath, ".pdb");

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

                var avoidCoverageAttributeFullName = typeof(AvoidCoverageAttribute).FullName;
                var coveredAssemblyAttributeFullName = typeof(CoveredAssemblyAttribute).FullName;
                foreach (var cAttr in assemblyDefinition.CustomAttributes)
                {
                    var attrFullName = cAttr.Constructor.DeclaringType.FullName;
                    if (attrFullName == avoidCoverageAttributeFullName)
                    {
                        _logger.Debug($"Assembly: {FilePath}, ignored.");
                        return;
                    }

                    if (attrFullName == coveredAssemblyAttributeFullName)
                    {
                        _logger.Debug($"Assembly: {FilePath}, already have coverage information.");
                        return;
                    }
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
                                 *      if (!CoverageReporter.TryGetScope("/app/MyMathClass.cs", out var scope))
                                 *      {
                                 *          if (value == 1)
                                 *          {
                                 *              return 1;
                                 *          }
                                 *          return value * Factorial(value - 1);
                                 *      }
                                 *      scope.Report(1688871335493638uL, 1970363492139032uL);
                                 *      int result;
                                 *      if (value == 1)
                                 *      {
                                 *          scope.Report(2251838468915210uL, 2533330625560598uL);
                                 *          result = 1;
                                 *      }
                                 *      else
                                 *      {
                                 *          scope.Report(3377738376020013uL);
                                 *          result = value * Factorial(value - 1);
                                 *      }
                                 *      scope.Report(3659196172926982uL);
                                 *      return result;
                                 *  }
                                 */

                                var methodBody = moduleTypeMethod.Body;
                                var instructions = methodBody.Instructions;
                                var instructionsOriginalLength = instructions.Count;
                                if (instructions.Capacity < instructionsOriginalLength * 2)
                                {
                                    instructions.Capacity = instructionsOriginalLength * 2;
                                }

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
                                    var currentInstruction = instructions.First(i => i.Offset == currentSequencePoint.Offset);
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
                                if (string.IsNullOrEmpty(methodFileName))
                                {
                                    methodFileName = "unknown";
                                }

                                var tryGetScopeMethodRef = module.ImportReference(ReportTryGetScopeMethodInfo);
                                instructions.Insert(0, Instruction.Create(OpCodes.Ldstr, methodFileName));
                                instructions.Insert(1, Instruction.Create(OpCodes.Ldloca, coverageScopeVariable));
                                instructions.Insert(2, Instruction.Create(OpCodes.Call, tryGetScopeMethodRef));
                                instructions.Insert(3, Instruction.Create(OpCodes.Brtrue, instructions[instructionsOriginalLength + 3]));

                                // Step 8 - Insert line reporter
                                var scopeReportMethodRef = module.ImportReference(ScopeReportMethodInfo);
                                var scopeReport2MethodRef = module.ImportReference(ScopeReport2MethodInfo);
                                for (var i = 0; i < clonedInstructionsWithOriginalSequencePoint.Count; i++)
                                {
                                    var currentItem = clonedInstructionsWithOriginalSequencePoint[i];
                                    var currentInstruction = currentItem.Item1;
                                    var currentSequencePoint = currentItem.Item2;
                                    var currentInstructionRange = GetRangeFromSequencePoint(currentSequencePoint);
                                    var currentInstructionIndex = instructions.IndexOf(currentInstruction);
                                    var currentInstructionClone = CloneInstruction(currentInstruction);

                                    if (i < clonedInstructionsWithOriginalSequencePoint.Count - 1)
                                    {
                                        var nextItem = clonedInstructionsWithOriginalSequencePoint[i + 1];
                                        var nextInstruction = nextItem.Item1;

                                        // We check if the next instruction with sequence point is a NOP and is immediatly after the current one.
                                        // If that is the case we can group two ranges in a single instruction.
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
                                            var nextInstructionRange = GetRangeFromSequencePoint(nextSequencePoint);
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
                    assemblyDefinition.CustomAttributes.Add(coveredAssemblyAttribute);

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
            catch
            {
                throw;
            }
        }

        private static ulong GetRangeFromSequencePoint(SequencePoint currentSequencePoint)
        {
            var startLine = currentSequencePoint.StartLine;
            var startColumn = currentSequencePoint.StartColumn;
            var endLine = currentSequencePoint.EndLine;
            var endColumn = currentSequencePoint.EndColumn;

            if (startLine > ushort.MaxValue || endLine > ushort.MaxValue)
            {
                // If the line is greater than ushort.MaxValue we set the range as 0
                // This will keep the filename being reported and taken in consideration by the ITR
                // The ITR with range 0 means that any modification in the file is included.
                startLine = 0;
                startColumn = 0;
                endLine = 0;
                endColumn = 0;
            }

            if (startColumn > ushort.MaxValue || endColumn > ushort.MaxValue)
            {
                // If only the column is greater than ushort.MaxValue we only take in consideration
                // the line range.
                startColumn = 0;
                endColumn = 0;
            }

            var currentInstructionRange = ((ulong)(ushort)startLine << 48) |
                                          ((ulong)(ushort)startColumn << 32) |
                                          ((ulong)(ushort)endLine << 16) |
                                          ((ulong)(ushort)endColumn);

            return currentInstructionRange;
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

                var datadogTraceDllPath = Path.Combine(_tracerHome, targetFolder, "Datadog.Trace.dll");
                var datadogTracePdbPath = Path.Combine(_tracerHome, targetFolder, "Datadog.Trace.pdb");

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
                        _logger.Debug($"GetTracerTarget: Writing {outputAssemblyDllLocation} ...");

                        if (File.Exists(datadogTraceDllPath))
                        {
                            File.Copy(datadogTraceDllPath, outputAssemblyDllLocation, true);
                        }

                        if (File.Exists(datadogTracePdbPath))
                        {
                            File.Copy(datadogTracePdbPath, outputAssemblyPdbLocation, true);
                        }
                    }

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
                    if (targetValue.Contains(".NETFramework,Version="))
                    {
                        _logger.Debug($"GetTracerTarget: Returning TracerTarget.Net461 from {targetValue}");
                        return TracerTarget.Net461;
                    }

                    var matchTarget = NetCorePattern.Match(targetValue);
                    if (matchTarget.Success)
                    {
                        var versionValue = matchTarget.Groups[1].Value;
                        if (float.TryParse(versionValue, NumberStyles.AllowDecimalPoint, USCultureInfo, out var version))
                        {
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
