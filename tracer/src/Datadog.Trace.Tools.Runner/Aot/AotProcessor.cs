// <copyright file="AotProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.CallTarget;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.Aot
{
    internal class AotProcessor
    {
        private static readonly NativeCallTargetDefinition[] Definitions;
        private static readonly NativeCallTargetDefinition[] DerivedDefinitions;
        private static readonly Assembly TracerAssembly;
        private static readonly Type CallTargetInvokerType = typeof(Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvoker);

        static AotProcessor()
        {
            Definitions = InstrumentationDefinitions.GetAllDefinitions().Definitions;
            DerivedDefinitions = InstrumentationDefinitions.GetDerivedDefinitions().Definitions;
            TracerAssembly = typeof(Datadog.Trace.ClrProfiler.Instrumentation).Assembly;
        }

        public static void ProcessFolder(string inputFolder, string outputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException("Input folder doesn't exist.");
            }

            if (!Directory.Exists(outputFolder))
            {
                throw new DirectoryNotFoundException("Output folder doesn't exist.");
            }

            int processed = 0;
            Parallel.ForEach(Directory.EnumerateFiles(inputFolder, "*.dll", SearchOption.TopDirectoryOnly), file =>
            {
                if (TryProcessAssembly(file, Path.Combine(outputFolder, Path.GetFileName(file))))
                {
                    Interlocked.Increment(ref processed);
                }
            });

            AnsiConsole.WriteLine($"{processed} files processed.");
        }

        private static bool TryProcessAssembly(string inputPath, string outputPath)
        {
            bool processed = false;
            try
            {
                ModuleContext modCtx = ModuleDef.CreateModuleContext();
                using (ModuleDefMD module = ModuleDefMD.Load(inputPath, modCtx))
                {
                    var lstDefinitionsDefs = new List<DefinitionDef>();
                    var assemblyDef = module.Assembly;

                    // Extract direct definitions
                    foreach (var definition in Definitions)
                    {
                        if (definition.TargetAssembly != assemblyDef.Name)
                        {
                            continue;
                        }

                        if (assemblyDef.Version is not null)
                        {
                            var minVersion = new Version(definition.TargetMinimumMajor, definition.TargetMinimumMinor, definition.TargetMinimumPatch);
                            var maxVersion = new Version(definition.TargetMaximumMajor, definition.TargetMaximumMinor, definition.TargetMaximumPatch);

                            if (assemblyDef.Version < minVersion)
                            {
                                continue;
                            }

                            if (assemblyDef.Version > maxVersion)
                            {
                                continue;
                            }
                        }

                        var typeDef = module.Find(definition.TargetType, true);
                        if (typeDef is null)
                        {
                            typeDef = module.ExportedTypes.FirstOrDefault(eType => eType.FullName == definition.TargetType)?.Resolve();
                        }

                        if (typeDef is not null)
                        {
                            MethodDef methodDef = null;
                            foreach (var mDef in typeDef.FindMethods(definition.TargetMethod))
                            {
                                var lstParameters = mDef.Parameters.ToList();
                                if (lstParameters.Count != definition.TargetSignatureTypesLength)
                                {
                                    continue;
                                }

                                bool parameters = true;
                                var ptr = definition.TargetSignatureTypes;
                                for (var i = 0; i < definition.TargetSignatureTypesLength; i++)
                                {
                                    var localPtr = Marshal.ReadIntPtr(ptr);
                                    var localString = Marshal.PtrToStringUni(localPtr);
                                    ptr += Marshal.SizeOf(typeof(IntPtr));

                                    if (localString == "_")
                                    {
                                        continue;
                                    }

                                    if (i == 0)
                                    {
                                        if (mDef.ReturnType.FullName != localString)
                                        {
                                            parameters = false;
                                            break;
                                        }
                                    }
                                    else if (lstParameters[i].Type.FullName != localString)
                                    {
                                        parameters = false;
                                        break;
                                    }
                                }

                                if (parameters)
                                {
                                    methodDef = mDef;
                                    var integrationType = TracerAssembly.GetType(definition.IntegrationType, false);
                                    if (integrationType is not null)
                                    {
                                        lstDefinitionsDefs.Add(new DefinitionDef(assemblyDef, typeDef, methodDef, integrationType, definition));
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    // Extract derived definitions
                    var assemblyRefs = module.GetAssemblyRefs();
                    foreach (var assemblyRef in assemblyRefs)
                    {
                        foreach (var definition in DerivedDefinitions)
                        {
                            if (definition.TargetAssembly != assemblyRef.Name.String)
                            {
                                continue;
                            }

                            var minVersion = new Version(definition.TargetMinimumMajor, definition.TargetMinimumMinor, definition.TargetMinimumPatch);
                            var maxVersion = new Version(definition.TargetMaximumMajor, definition.TargetMaximumMinor, definition.TargetMaximumPatch);

                            if (assemblyDef.Version < minVersion)
                            {
                                continue;
                            }

                            if (assemblyDef.Version > maxVersion)
                            {
                                continue;
                            }

                            foreach (var typeDef in module.Types)
                            {
                                // ..
                            }
                        }
                    }

                    if (lstDefinitionsDefs.Count == 0)
                    {
                        return false;
                    }

                    AnsiConsole.WriteLine($"{assemblyDef.FullName} => {lstDefinitionsDefs.Count}");
                    if (ProcessDefinitionDefs(module, lstDefinitionsDefs))
                    {
                        module.Write(outputPath);
                        processed = true;
                    }
                }
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Utils.WriteError(ex.ToString());
                return false;
            }
            finally
            {
                if (!processed)
                {
                    File.Copy(inputPath, outputPath);
                }
            }

            return true;
        }

        private static bool ProcessDefinitionDefs(ModuleDef moduleDef, List<DefinitionDef> definitionDefs)
        {
            var callTargetInvokerType = moduleDef.Import(CallTargetInvokerType);
            var callTargetInvokerMethods = CallTargetInvokerType.GetMethods();
            var exceptionTypeRef = moduleDef.Import(typeof(Exception));

            foreach (var definitionDef in definitionDefs)
            {
                var methodDef = definitionDef.TargetMethodDef;
                if (!methodDef.HasBody)
                {
                    continue;
                }

                var integrationTypeSig = moduleDef.ImportAsTypeSig(definitionDef.IntegrationType);
                var targetTypeSig = definitionDef.TargetTypeDef.ToTypeSig();
                var methodReturnTypeSig = methodDef.ReturnType;

                var lstParameters = definitionDef.TargetMethodDef.Parameters.Where(p => p.IsNormalMethodParameter).ToList();

                // CallTargetReturn
                var callTargetReturnType = definitionDef.TargetMethodDef.HasReturnType ? typeof(CallTargetReturn<>) : typeof(CallTargetReturn);
                var callTargetReturnTypeRef = moduleDef.Import(callTargetReturnType);
                GenericInstSig callTargetReturnTypeGenInstSig = null;
                MemberRefUser callTargetReturnGetReturnValueMemberRef = null;
                if (definitionDef.TargetMethodDef.HasReturnType)
                {
                    callTargetReturnTypeGenInstSig = new GenericInstSig((ClassSig)callTargetReturnTypeRef.ToTypeSig(), methodReturnTypeSig);
                    var getReturnValueMethodInfo = callTargetReturnType.GetMethods().FirstOrDefault(m => m.Name == "GetReturnValue");
                    var getReturnValueMethodRef = moduleDef.Import(getReturnValueMethodInfo);
                    callTargetReturnGetReturnValueMemberRef = new MemberRefUser(moduleDef, "GetReturnValue", getReturnValueMethodRef.MethodSig, callTargetReturnTypeGenInstSig.ToTypeDefOrRef());
                }

                // BeginMethod
                var beginMethodMethodInfo = callTargetInvokerMethods.FirstOrDefault(m => m.Name == "BeginMethod" && m.GetParameters().Length == lstParameters.Count + 1);
                var beginMethodInfoMethodRef = moduleDef.Import(beginMethodMethodInfo);
                var lstBeginMethodGenericsTypeSigs = new List<TypeSig>();
                lstBeginMethodGenericsTypeSigs.Add(integrationTypeSig);
                lstBeginMethodGenericsTypeSigs.Add(targetTypeSig);
                foreach (var mParam in lstParameters)
                {
                    lstBeginMethodGenericsTypeSigs.Add(mParam.Type);
                }

                var beginMethodInfoMethodSpec = new MethodSpecUser((MemberRefUser)beginMethodInfoMethodRef, new GenericInstMethodSig(lstBeginMethodGenericsTypeSigs));
                var callTargetStateTypeSig = beginMethodInfoMethodRef.MethodSig.RetType;

                // EndMethod
                var endMethodMethodInfo = callTargetInvokerMethods.FirstOrDefault(
                    m =>
                    {
                        if (m.Name != "EndMethod")
                        {
                            return false;
                        }

                        if (definitionDef.TargetMethodDef.HasReturnType && m.ReturnType == typeof(CallTargetReturn))
                        {
                            return false;
                        }

                        if (!m.GetParameters().Last().IsIn)
                        {
                            return false;
                        }

                        return true;
                    });
                var endMethodInfoMethodRef = moduleDef.Import(endMethodMethodInfo);
                var lstEndMethodGenericsTypeSigs = new List<TypeSig>();
                lstEndMethodGenericsTypeSigs.Add(integrationTypeSig);
                lstEndMethodGenericsTypeSigs.Add(targetTypeSig);
                if (definitionDef.TargetMethodDef.HasReturnType)
                {
                    lstEndMethodGenericsTypeSigs.Add(methodReturnTypeSig);
                }

                var endMethodInfoMethodSpec = new MethodSpecUser((MemberRefUser)endMethodInfoMethodRef, new GenericInstMethodSig(lstEndMethodGenericsTypeSigs));

                // LogException
                var logExceptionMethodInfo = callTargetInvokerMethods.FirstOrDefault(m => m.Name == "LogException");
                var logExceptionMethodRef = moduleDef.Import(logExceptionMethodInfo);
                var lstLogExceptionGenericsTypeSigs = new List<TypeSig>();
                lstLogExceptionGenericsTypeSigs.Add(integrationTypeSig);
                lstLogExceptionGenericsTypeSigs.Add(targetTypeSig);
                var logExceptionMethodSpec = new MethodSpecUser((MemberRefUser)logExceptionMethodRef, new GenericInstMethodSig(lstLogExceptionGenericsTypeSigs));

                // GetDefaultValue
                var getDefaultValueMethodInfo = callTargetInvokerMethods.FirstOrDefault(m => m.Name == "GetDefaultValue");
                var getDefaultValueMethodRef = moduleDef.Import(getDefaultValueMethodInfo);

                var getDefaultValueReturnTypeMethodSpec = new MethodSpecUser((MemberRefUser)getDefaultValueMethodRef, new GenericInstMethodSig(methodReturnTypeSig));
                var getDefaultValueCallTargetReturnTypeMethodSpec = new MethodSpecUser((MemberRefUser)getDefaultValueMethodRef, new GenericInstMethodSig(callTargetReturnTypeGenInstSig ?? callTargetReturnTypeRef.ToTypeSig()));

                // Add new locals Add locals for TReturn (if non-void method), CallTargetState, CallTargetReturn/CallTargetReturn<TReturn>, Exception
                var methodBody = methodDef.Body;
                Local returnValueLocal = null;
                Local callTargetReturnLocal = null;
                if (definitionDef.TargetMethodDef.HasReturnType)
                {
                    returnValueLocal = methodBody.Variables.Add(new Local(methodReturnTypeSig, "cTargetReturnValue"));
                    callTargetReturnLocal = methodBody.Variables.Add(new Local(callTargetReturnTypeGenInstSig, "cTargetReturn"));
                }
                else
                {
                    callTargetReturnLocal = methodBody.Variables.Add(new Local(callTargetReturnTypeRef.ToTypeSig(), "cTargetReturn"));
                }

                var callTargetStateLocal = methodBody.Variables.Add(new Local(callTargetStateTypeSig, "cTargetState"));
                var exceptionLocal = methodBody.Variables.Add(new Local(exceptionTypeRef.ToTypeSig(), "cTargetException"));

                var oldStart = methodBody.Instructions.First();

                var index = 0;

                // *** BeginMethod
                var startInst = Instruction.Create(OpCodes.Call, getDefaultValueReturnTypeMethodSpec);
                methodBody.Instructions.Insert(index++, startInst);
                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc_S, returnValueLocal));
                }

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, getDefaultValueCallTargetReturnTypeMethodSpec));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc_S, callTargetReturnLocal));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldnull));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc_S, exceptionLocal));
                // ..
                if (methodDef.IsStatic)
                {
                    if (definitionDef.TargetTypeDef.IsValueType)
                    {
                        return false;
                    }

                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldnull));
                }
                else
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldarg_0));
                    if (definitionDef.TargetTypeDef.IsValueType)
                    {
                        if (definitionDef.TargetTypeDef.HasGenericParameters)
                        {
                            return false;
                        }

                        methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldobj, definitionDef.TargetTypeDef));
                    }
                }

                foreach (var mParameter in lstParameters)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldarga_S, mParameter));
                }

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, beginMethodInfoMethodSpec));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc_S, callTargetStateLocal));
                var beginMethodLeaveInst = Instruction.Create(OpCodes.Leave_S, oldStart);
                methodBody.Instructions.Insert(index++, beginMethodLeaveInst);
                var beginMethodCatchStartInst = Instruction.Create(OpCodes.Call, logExceptionMethodSpec);
                methodBody.Instructions.Insert(index++, beginMethodCatchStartInst);
                var beginMethodCatchLeaveInst = Instruction.Create(OpCodes.Leave_S, oldStart);
                methodBody.Instructions.Insert(index++, beginMethodCatchLeaveInst);

                // *** BeginMethod exception handling clause
                var beginMethodExClause = new ExceptionHandler();
                beginMethodExClause.TryStart = startInst;
                beginMethodExClause.TryEnd = beginMethodCatchStartInst;
                beginMethodExClause.HandlerStart = beginMethodCatchStartInst;
                beginMethodExClause.HandlerEnd = beginMethodCatchLeaveInst;
                beginMethodExClause.HandlerType = ExceptionHandlerType.Catch;
                beginMethodExClause.CatchType = exceptionTypeRef;
                methodBody.ExceptionHandlers.Add(beginMethodExClause);

                // ***
                // ENDING OF THE METHOD EXECUTION
                // ***

                // *** Create return instruction and insert it at the end
                var methodReturnInstr = Instruction.Create(OpCodes.Ret);
                methodBody.Instructions.Add(methodReturnInstr);
                index = methodBody.Instructions.IndexOf(methodReturnInstr);

                // ***
                // EXCEPTION CATCH
                // ***
                var startExceptionCatch = Instruction.Create(OpCodes.Stloc_S, exceptionLocal);
                methodBody.Instructions.Insert(index++, startExceptionCatch);
                var rethrowInstr = Instruction.Create(OpCodes.Rethrow);
                methodBody.Instructions.Insert(index++, rethrowInstr);

                // ***
                // EXCEPTION FINALLY / END METHOD PART
                // ***
                Instruction endMethodTryStartInstr;
                if (methodDef.IsStatic)
                {
                    if (definitionDef.TargetTypeDef.IsValueType)
                    {
                        return false;
                    }

                    endMethodTryStartInstr = Instruction.Create(OpCodes.Ldnull);
                    methodBody.Instructions.Insert(index++, endMethodTryStartInstr);
                }
                else
                {
                    endMethodTryStartInstr = Instruction.Create(OpCodes.Ldarg_0);
                    methodBody.Instructions.Insert(index++, endMethodTryStartInstr);
                    if (definitionDef.TargetTypeDef.IsValueType)
                    {
                        if (definitionDef.TargetTypeDef.HasGenericParameters)
                        {
                            return false;
                        }

                        methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldobj, definitionDef.TargetTypeDef));
                    }
                }

                // *** Load the return value is is not void
                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloc_S, returnValueLocal));
                }

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloc_S, exceptionLocal));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca_S, callTargetStateLocal));

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, endMethodInfoMethodSpec));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc_S, callTargetReturnLocal));

                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca_S, callTargetReturnLocal));
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, callTargetReturnGetReturnValueMemberRef));
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc_S, returnValueLocal));
                }

                var endMethodTryLeave = new Instruction(OpCodes.Leave_S);
                methodBody.Instructions.Insert(index++, endMethodTryLeave);

                // *** EndMethod call catch
                var endMethodCatchFirstInstr = Instruction.Create(OpCodes.Call, logExceptionMethodSpec);
                methodBody.Instructions.Insert(index++, endMethodCatchFirstInstr);
                var endMethodCatchLeaveInstr = new Instruction(OpCodes.Leave_S);
                methodBody.Instructions.Insert(index++, endMethodCatchLeaveInstr);

                // *** EndMethod exception handling clause
                var endMethodExClause = new ExceptionHandler();
                endMethodExClause.TryStart = endMethodTryStartInstr;
                endMethodExClause.TryEnd = endMethodCatchFirstInstr;
                endMethodExClause.HandlerStart = endMethodCatchFirstInstr;
                endMethodExClause.HandlerEnd = endMethodCatchLeaveInstr;
                endMethodExClause.HandlerType = ExceptionHandlerType.Catch;
                endMethodExClause.CatchType = exceptionTypeRef;
                methodBody.ExceptionHandlers.Add(endMethodExClause);

                // *** EndMethod leave to finally
                var endFinallyInstr = Instruction.Create(OpCodes.Endfinally);
                methodBody.Instructions.Insert(index++, endFinallyInstr);
                endMethodTryLeave.Operand = endFinallyInstr;
                endMethodCatchLeaveInstr.Operand = endFinallyInstr;

                // ***
                // METHOD RETURN
                // ***

                // Load the current return value from the local var
                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloc_S, returnValueLocal));
                }

                // Changes all returns to a LEAVE.S
                for (var i = 0; i < methodBody.Instructions.Count; i++)
                {
                    var instr = methodBody.Instructions[i];
                    if (instr.OpCode == OpCodes.Ret)
                    {
                        if (instr != methodReturnInstr)
                        {
                            if (returnValueLocal is not null)
                            {
                                methodBody.Instructions.Insert(i, Instruction.Create(OpCodes.Stloc_S, returnValueLocal));
                            }

                            instr.OpCode = OpCodes.Leave;
                            instr.Operand = methodBody.Instructions[methodBody.Instructions.IndexOf(endFinallyInstr) + 1];
                        }
                    }
                }

                // Exception handling clauses
                var exClause = new ExceptionHandler();
                exClause.TryStart = startInst;
                exClause.TryEnd = startExceptionCatch;
                exClause.HandlerStart = startExceptionCatch;
                exClause.HandlerEnd = rethrowInstr;
                exClause.HandlerType = ExceptionHandlerType.Catch;
                exClause.CatchType = exceptionTypeRef;
                methodBody.ExceptionHandlers.Add(exClause);

                var finallyClause = new ExceptionHandler();
                finallyClause.TryStart = startInst;
                finallyClause.TryEnd = methodBody.Instructions[methodBody.Instructions.IndexOf(rethrowInstr) + 1];
                finallyClause.HandlerStart = finallyClause.TryEnd;
                finallyClause.HandlerEnd = endFinallyInstr;
                finallyClause.HandlerType = ExceptionHandlerType.Finally;
                methodBody.ExceptionHandlers.Add(finallyClause);
            }

            return true;
        }
    }

#pragma warning disable SA1201
    internal readonly struct DefinitionDef
    {
        public readonly AssemblyDef TargetAssemblyDef;
        public readonly TypeDef TargetTypeDef;
        public readonly MethodDef TargetMethodDef;
        public readonly Type IntegrationType;
        public readonly NativeCallTargetDefinition Definition;

        public DefinitionDef(AssemblyDef targetAssemblyDef, TypeDef targetTypeDef, MethodDef targetMethodDef, Type integrationType, NativeCallTargetDefinition definition)
        {
            TargetAssemblyDef = targetAssemblyDef;
            TargetTypeDef = targetTypeDef;
            TargetMethodDef = targetMethodDef;
            IntegrationType = integrationType;
            Definition = definition;
        }
    }
}
