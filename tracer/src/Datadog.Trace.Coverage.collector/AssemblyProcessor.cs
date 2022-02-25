// <copyright file="AssemblyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

[assembly: Datadog.Trace.Ci.Coverage.Attributes.AvoidCoverage]

#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    internal class AssemblyProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AssemblyProcessor));
        private static readonly ConstructorInfo CoveredAssemblyAttributeTypeCtor = typeof(Datadog.Trace.Ci.Coverage.Attributes.CoveredAssemblyAttribute).GetConstructors()[0];
        private static readonly MethodInfo ReportTryGetScopeMethodInfo = typeof(Datadog.Trace.Ci.Coverage.CoverageReporter).GetMethod("TryGetScope");
        private static readonly MethodInfo ScopeReportMethodInfo = typeof(Datadog.Trace.Ci.Coverage.CoverageScope).GetMethod("Report", new[] { typeof(ulong) });
        private static readonly MethodInfo ScopeReport2MethodInfo = typeof(Datadog.Trace.Ci.Coverage.CoverageScope).GetMethod("Report", new[] { typeof(ulong), typeof(ulong) });

        private readonly string _assemblyFilePath;

        public AssemblyProcessor(string filePath)
        {
            _assemblyFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(_assemblyFilePath))
            {
                throw new FileNotFoundException($"Assembly not found in path: {_assemblyFilePath}");
            }
        }

        public string FilePath => _assemblyFilePath;

        public void ProcessAndSaveTo()
        {
            var filePath = FilePath;
            var pdbFilePath = Path.ChangeExtension(filePath, ".pdb");
            var backupFilePath = Path.ChangeExtension(filePath, Path.GetExtension(filePath) + "Backup");
            var backupPdbFilePath = Path.ChangeExtension(filePath, ".pdbBackup");

            try
            {
                using var assemblyDefinition = AssemblyDefinition.ReadAssembly(_assemblyFilePath, new ReaderParameters
                {
                    ReadSymbols = true,
                    ReadWrite = true
                });

                /*
                if (assemblyDefinition.Name.HasPublicKey)
                {
                    Console.WriteLine($"Assembly: {FilePath}, StrongKey not yet supported.");
                    return;
                }
                */

                if (assemblyDefinition.CustomAttributes.Any(cAttr =>
                    cAttr.Constructor.DeclaringType.Name == typeof(Datadog.Trace.Ci.Coverage.Attributes.AvoidCoverageAttribute).Name))
                {
                    Console.WriteLine($"Assembly: {FilePath}, ignored.");
                    return;
                }

                if (assemblyDefinition.CustomAttributes.Any(cAttr =>
                    cAttr.Constructor.DeclaringType.Name == typeof(Datadog.Trace.Ci.Coverage.Attributes.CoveredAssemblyAttribute).Name))
                {
                    Console.WriteLine($"Assembly: {FilePath}, already have coverage information.");
                    return;
                }

                bool isDirty = false;
                ulong totalMethods = 0;
                ulong totalInstructions = 0;

                // Process all modules in the assembly
                foreach (ModuleDefinition module in assemblyDefinition.Modules)
                {
                    Console.WriteLine($"Processing module: {module.Name}");

                    // Process all types defined in the module
                    foreach (TypeDefinition moduleType in module.GetTypes())
                    {
                        if (moduleType.CustomAttributes.Any(cAttr =>
                            cAttr.Constructor.DeclaringType.Name == typeof(Datadog.Trace.Ci.Coverage.Attributes.AvoidCoverageAttribute).Name))
                        {
                            continue;
                        }

                        Console.WriteLine($"\t{moduleType.FullName}");

                        // Process all Methods in the type
                        foreach (var moduleTypeMethod in moduleType.Methods)
                        {
                            if (moduleTypeMethod.CustomAttributes.Any(cAttr =>
                                cAttr.Constructor.DeclaringType.Name == typeof(Datadog.Trace.Ci.Coverage.Attributes.AvoidCoverageAttribute).Name))
                            {
                                continue;
                            }

                            if (moduleTypeMethod.DebugInformation is null || !moduleTypeMethod.DebugInformation.HasSequencePoints)
                            {
                                Console.WriteLine($"\t\t[NO] {moduleTypeMethod.FullName}");
                                continue;
                            }

                            Console.WriteLine($"\t\t[YES] {moduleTypeMethod.FullName}.");

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
                                        RemoveShortOpcodes(clonedInstruction);
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
                                        RemoveShortOpcodes(clonedInstruction);
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
                                var coverageScopeVariable = new VariableDefinition(module.ImportReference(typeof(Datadog.Trace.Ci.Coverage.CoverageScope)));
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
                }

                // Save assembly if we modify it successfully
                if (isDirty)
                {
                    var coveredAssemblyAttributeTypeCtorRef = assemblyDefinition.MainModule.ImportReference(CoveredAssemblyAttributeTypeCtor);
                    var coveredAssemblyAttribute = new CustomAttribute(coveredAssemblyAttributeTypeCtorRef);
                    coveredAssemblyAttribute.ConstructorArguments.Add(new CustomAttributeArgument(assemblyDefinition.MainModule.ImportReference(typeof(ulong)), totalMethods));
                    coveredAssemblyAttribute.ConstructorArguments.Add(new CustomAttributeArgument(assemblyDefinition.MainModule.ImportReference(typeof(ulong)), totalInstructions));
                    assemblyDefinition.CustomAttributes.Add(coveredAssemblyAttribute);

                    Console.WriteLine($"Saving assembly: {filePath}");

                    try
                    {
                        File.Copy(filePath, backupFilePath, true);
                        File.Copy(pdbFilePath, backupPdbFilePath, true);
                        new FileInfo(backupFilePath).Attributes = FileAttributes.Hidden;
                        new FileInfo(backupPdbFilePath).Attributes = FileAttributes.Hidden;

                        var asmLocation = typeof(Datadog.Trace.Tracer).Assembly.Location;
                        File.Copy(asmLocation, Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(asmLocation)));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    assemblyDefinition.Write(new WriterParameters
                    {
                       WriteSymbols = true,
                    });

                    Console.WriteLine($"Done: {filePath}");
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
                try
                {
                    if (File.Exists(backupFilePath))
                    {
                        File.Copy(backupFilePath, FilePath, true);
                        File.Copy(backupPdbFilePath, pdbFilePath, true);
                        File.Delete(backupFilePath);
                        File.Delete(backupPdbFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                throw;
            }
        }

        private static void RemoveShortOpcodes(Instruction instruction)
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
            else if (instruction.Operand is string strOp)
            {
                return Instruction.Create(instruction.OpCode, strOp);
            }
            else if (instruction.Operand is int intOp)
            {
                return Instruction.Create(instruction.OpCode, intOp);
            }
            else if (instruction.Operand is long lngOp)
            {
                return Instruction.Create(instruction.OpCode, lngOp);
            }
            else if (instruction.Operand is byte byteOp)
            {
                return Instruction.Create(instruction.OpCode, byteOp);
            }
            else if (instruction.Operand is sbyte sbyteOp)
            {
                return Instruction.Create(instruction.OpCode, sbyteOp);
            }
            else if (instruction.Operand is double dblOp)
            {
                return Instruction.Create(instruction.OpCode, dblOp);
            }
            else if (instruction.Operand is FieldReference fRefOp)
            {
                return Instruction.Create(instruction.OpCode, fRefOp);
            }
            else if (instruction.Operand is MethodReference mRefOp)
            {
                return Instruction.Create(instruction.OpCode, mRefOp);
            }
            else if (instruction.Operand is CallSite callOp)
            {
                return Instruction.Create(instruction.OpCode, callOp);
            }
            else if (instruction.Operand is Instruction instOp)
            {
                return Instruction.Create(instruction.OpCode, instOp);
            }
            else if (instruction.Operand is Instruction[] instsOp)
            {
                return Instruction.Create(instruction.OpCode, instsOp);
            }
            else if (instruction.Operand is VariableDefinition vDefOp)
            {
                return Instruction.Create(instruction.OpCode, vDefOp);
            }
            else if (instruction.Operand is ParameterDefinition pDefOp)
            {
                return Instruction.Create(instruction.OpCode, pDefOp);
            }
            else if (instruction.Operand is TypeReference tRefOp)
            {
                return Instruction.Create(instruction.OpCode, tRefOp);
            }
            else if (instruction.Operand is float sOp)
            {
                return Instruction.Create(instruction.OpCode, sOp);
            }

            throw new Exception($"Instruction: {instruction.OpCode} cannot be cloned.");
        }
    }
}
