// <copyright file="AssemblyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Mono.Cecil;
using Mono.Cecil.Cil;
using CallSite = Mono.Cecil.CallSite;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Datadog.Trace.Coverage.Collector
{
    internal class AssemblyProcessor
    {
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

        private byte[]? _strongNameKeyBlob;

        public AssemblyProcessor(string filePath, CoverageSettings settings, ICollectorLogger? logger = null)
        {
            _settings = settings;
            _logger = logger ?? new ConsoleCollectorLogger();
            _assemblyFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _enableJitOptimizations = settings.CIVisibility?.CodeCoverageEnableJitOptimizations ?? true;

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

        public void Process()
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

                var avoidCoverageAttributeFullName = typeof(AvoidCoverageAttribute).FullName;
                var coveredAssemblyAttributeFullName = typeof(CoveredAssemblyAttribute).FullName;
                var internalsVisibleToAttributeFullName = typeof(InternalsVisibleToAttribute).FullName;
                var debuggableAttributeFullName = typeof(DebuggableAttribute).FullName;
                var hasInternalsVisibleAttribute = false;
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

                    if (FiltersHelper.FilteredByAttribute(attrFullName, _settings.ExcludeByAttribute))
                    {
                        _logger.Debug($"Assembly: {FilePath}, ignored by settings attribute filter.");
                        return;
                    }

                    hasInternalsVisibleAttribute |= attrFullName == internalsVisibleToAttributeFullName;

                    // Enable Jit Optimizations
                    if (_enableJitOptimizations)
                    {
                        // We check for the Debuggable attribute to enable jit optimizations and improve coverage performance.
                        if (attrFullName == debuggableAttributeFullName)
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

                    var snkFilePath = _settings.CIVisibility?.CodeCoverageSnkFilePath;
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

                // Get TotalInstructions field
                var moduleCoverageMetadataImplTotalInstructionsField = new FieldReference("TotalInstructions", module.TypeSystem.Int64, moduleCoverageMetadataTypeReference);

                // Create number of types array
                var totalMethods = 0;
                var sequencePointArrayCountInstruction = Instruction.Create(OpCodes.Ldc_I4, totalMethods);
                var metadataArrayCountInstruction = Instruction.Create(OpCodes.Ldc_I4, totalMethods);

                var moduleCoverageMetadataImplSequencePointField = new FieldReference("SequencePoints", new ArrayType(module.TypeSystem.Int32), moduleCoverageMetadataTypeReference);
                moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                moduleCoverageMetadataImplCtor.Body.Instructions.Add(sequencePointArrayCountInstruction);
                moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Newarr, module.TypeSystem.Int32));
                moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, moduleCoverageMetadataImplSequencePointField));

                var lstMetadataInstructions = new List<Instruction>();
                var moduleCoverageMetadataImplMetadataField = new FieldReference("Metadata", new ArrayType(module.TypeSystem.Int64), moduleCoverageMetadataTypeReference);
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                lstMetadataInstructions.Add(metadataArrayCountInstruction);
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Newarr, module.TypeSystem.Int64));
                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stfld, moduleCoverageMetadataImplMetadataField));

                moduleCoverageMetadataImplTypeDef.Methods.Add(moduleCoverageMetadataImplCtor);

                var coverageReporterTypeDefinition = datadogTracerAssembly.MainModule.GetType(typeof(CoverageReporter<>).FullName);
                var coverageReporterTypeReference = module.ImportReference(coverageReporterTypeDefinition);
                var reportTypeGenericInstance = new GenericInstanceType(coverageReporterTypeReference);
                reportTypeGenericInstance.GenericArguments.Add(moduleCoverageMetadataImplTypeDef);

                var reportGetCountersMethod = new MethodReference("GetCounters", new ArrayType(module.TypeSystem.Int32), reportTypeGenericInstance)
                {
                    HasThis = false,
                    Parameters =
                    {
                        new ParameterDefinition(module.TypeSystem.Int32) { Name = "methodIndex" }
                    }
                };

                long totalSequencePoints = 0;
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

                            string? file = null;
                            foreach (var pt in sequencePoints)
                            {
                                if (pt.IsHidden || pt.Document is null || string.IsNullOrWhiteSpace(pt.Document.Url))
                                {
                                    continue;
                                }

                                file = pt.Document.Url;
                                break;
                            }

                            if (file is not null && FiltersHelper.FilteredBySourceFile(file, _settings.ExcludeSourceFiles))
                            {
                                _logger.Debug($"\t\t[NO] {moduleTypeMethod.FullName}, ignored by settings source filter");
                                continue;
                            }

                            totalMethods++;
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
                            var instructionsWithValidSequencePoints = new List<Instruction>();
                            for (var i = 0; i < sequencePoints.Count; i++)
                            {
                                var currentSequencePoint = sequencePoints[i];
                                if (!currentSequencePoint.IsHidden)
                                {
                                    instructionsWithValidSequencePoints.Add(instructions.First(i => i.Offset == currentSequencePoint.Offset));
                                }
                            }

                            // Step 3 - Modify local var to add the Coverage counters instance.
                            var countersVariable = new VariableDefinition(new ArrayType(module.TypeSystem.Int32));
                            methodBody.Variables.Add(countersVariable);

                            // Step 4 - Create methods sequence points array
                            moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                            moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, moduleCoverageMetadataImplSequencePointField));
                            moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (totalMethods - 1)));
                            moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, instructionsWithValidSequencePoints.Count));
                            moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Stelem_I4));

                            lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                            lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldfld, moduleCoverageMetadataImplMetadataField));
                            lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, (totalMethods - 1)));
                            var indexes = ((long)typeIndex << 32) | (long)methodIndex;
                            if (indexes > int.MaxValue)
                            {
                                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I8, indexes));
                            }
                            else
                            {
                                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)indexes));
                                lstMetadataInstructions.Add(Instruction.Create(OpCodes.Conv_I8));
                            }

                            lstMetadataInstructions.Add(Instruction.Create(OpCodes.Stelem_I8));
                            totalSequencePoints += instructionsWithValidSequencePoints.Count;

                            // Step 5 - Insert the counter retriever
                            instructions.Insert(0, Instruction.Create(OpCodes.Ldc_I4, (totalMethods - 1)));
                            instructions.Insert(1, Instruction.Create(OpCodes.Call, reportGetCountersMethod));
                            instructions.Insert(2, Instruction.Create(OpCodes.Stloc, countersVariable));

                            // Step 6 - Insert line reporter
                            for (var i = 0; i < instructionsWithValidSequencePoints.Count; i++)
                            {
                                var currentInstruction = instructionsWithValidSequencePoints[i];
                                var currentInstructionIndex = instructions.IndexOf(currentInstruction);
                                var currentInstructionClone = CloneInstruction(currentInstruction);

                                currentInstruction.OpCode = OpCodes.Ldloc;
                                currentInstruction.Operand = countersVariable;

                                var optIdx = 0;
                                if (i == 0 && _enableJitOptimizations && instructionsWithValidSequencePoints.Count > 1)
                                {
                                    // If the jit optimizations are enabled and instructions count is >= 2,
                                    // we do a `_ = counters[{lastIndex}];` at the first report.
                                    // This will remove later counters bound checks improving the overall performance.
                                    instructions.Insert(currentInstructionIndex + 1, Instruction.Create(OpCodes.Ldc_I4, instructionsWithValidSequencePoints.Count - 1));
                                    instructions.Insert(currentInstructionIndex + 2, Instruction.Create(OpCodes.Ldelem_I4));
                                    instructions.Insert(currentInstructionIndex + 3, Instruction.Create(OpCodes.Pop));
                                    instructions.Insert(currentInstructionIndex + 4, Instruction.Create(OpCodes.Ldloc, countersVariable));
                                    optIdx = 4;
                                }

                                // Increments items in the counters array (to have the number of times a line was executed)
                                instructions.Insert(currentInstructionIndex + optIdx + 1, Instruction.Create(OpCodes.Ldc_I4, i));
                                instructions.Insert(currentInstructionIndex + optIdx + 2, Instruction.Create(OpCodes.Ldelema, module.TypeSystem.Int32));
                                instructions.Insert(currentInstructionIndex + optIdx + 3, Instruction.Create(OpCodes.Dup));
                                instructions.Insert(currentInstructionIndex + optIdx + 4, Instruction.Create(OpCodes.Ldind_I4));
                                instructions.Insert(currentInstructionIndex + optIdx + 5, Instruction.Create(OpCodes.Ldc_I4_1));
                                instructions.Insert(currentInstructionIndex + optIdx + 6, Instruction.Create(OpCodes.Add));
                                instructions.Insert(currentInstructionIndex + optIdx + 7, Instruction.Create(OpCodes.Stind_I4));
                                instructions.Insert(currentInstructionIndex + optIdx + 8, currentInstructionClone);
                            }

                            isDirty = true;
                        }
                    }
                }

                sequencePointArrayCountInstruction.Operand = totalMethods;
                metadataArrayCountInstruction.Operand = totalMethods;
                module.Types.Add(moduleCoverageMetadataImplTypeDef);

                // Copy metadata instructions to the .ctor
                foreach (var instruction in lstMetadataInstructions)
                {
                    moduleCoverageMetadataImplCtor.Body.Instructions.Add(instruction);
                }

                // Sets the TotalInstructions field
                moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                if (totalSequencePoints > int.MaxValue)
                {
                    moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I8, totalSequencePoints));
                }
                else
                {
                    moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)totalSequencePoints));
                    moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Conv_I8));
                }

                moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, moduleCoverageMetadataImplTotalInstructionsField));
                moduleCoverageMetadataImplCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

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
        }
    }
}
