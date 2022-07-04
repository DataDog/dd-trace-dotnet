using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace Datadog.InstrumentedAssemblyGenerator
{
    /// <summary>
    /// Responsible for adding all metadata modifications that were performed using the CLR Profiling API at runtime
    /// </summary>
    internal class ProfilerMetadataImporter
    {
        private readonly InstrumentedAssemblyGeneratorContext _instrumentedAssemblyGeneratorContext;
        private readonly Dictionary<string, ITypeDefOrRef> _importedTypes = new();
        private readonly List<ITypeDefOrRef> _allImportedTypes = new();
        private readonly List<IMethodDefOrRef> _allImportedMembers = new();
        private readonly List<AssemblyRef> _allImportedAssemblies = new();
        private readonly List<MethodSpec> _allImportedMethodsSpec = new();
        private readonly List<string> _allImportedStrings = new();
        private readonly List<string> _assembliesToAddReference = new();
        private readonly AssemblyGeneratorArgs _assemblyGeneratorArgs;
        internal ModuleDefMD OriginalModule { get; }

        public ProfilerMetadataImporter(InstrumentedAssemblyGeneratorContext instrumentedAssemblyGeneratorContext, ModuleDefMD originalModule, AssemblyGeneratorArgs assemblyGeneratorArgs)
        {
            _instrumentedAssemblyGeneratorContext = instrumentedAssemblyGeneratorContext;
            OriginalModule = originalModule;
            _assemblyGeneratorArgs = assemblyGeneratorArgs;
        }

        internal void Import()
        {
            ImportInstrumentedAssemblyMetadata();
            ImportAssemblyReferences();
        }

        private void ImportInstrumentedAssemblyMetadata()
        {
            if (!_instrumentedAssemblyGeneratorContext.InstrumentedModulesTypesTokens.TryGetValue((OriginalModule.FullName, OriginalModule.Mvid), out var module))
            {
                Logger.Warn("Can't find module for import to");
                return;
            }

            foreach (var tokensAndName in module.TokensAndNames)
            {
                switch (tokensAndName.Key.Table)
                {
                    case MetadataTable.TypeDef:
                    {
                        // todo: add type flags
                        var newTypeDef = new TypeDefUser(tokensAndName.Value.Type);
                        OriginalModule.AddAsNonNestedType(newTypeDef);
                        break;
                    }

                    case MetadataTable.TypeRef:
                    {
                        AddTypeToCache(ImportTypeRef(tokensAndName.Value.Type), tokensAndName);
                        break;
                    }

                    case MetadataTable.AssemblyRef:
                    {
                        _assembliesToAddReference.Add(tokensAndName.Value.FullName);
                        break;
                    }

                    case MetadataTable.ModuleRef:
                    {
                        try
                        {
                            var modulePath = tokensAndName.Value.FullName;
                            if (!File.Exists(modulePath))
                            {
                                modulePath = Path.Combine(_assemblyGeneratorArgs.OriginalModulesFolder, Path.GetFileName(modulePath));
                            }

                            if (File.Exists(modulePath))
                            {
                                var moduleDefMd = ModuleDefMD.Load(modulePath);
                                OriginalModule.Assembly.Modules.Add(moduleDefMd);
                            }

                            Logger.Debug($"{Path.GetFileName(modulePath)} doesn't exist in {modulePath} nor in {tokensAndName.Value.FullName}");
                        }
                        catch (BadImageFormatException e)
                        {
                            Logger.Debug($"{tokensAndName.Value.FullName}:{Environment.NewLine}{e}");
                        }
                        break;
                    }

                    case MetadataTable.Field:
                    {
                        var typeToAddField = OriginalModule.Types.SingleOrDefault(t =>
                                                                                      t.Name.String.Equals(tokensAndName.Value.Type, StringComparison.InvariantCultureIgnoreCase));
                        typeToAddField?.Fields.Add(
                            new FieldDefUser(tokensAndName.Value.MethodOrField,
                                             new FieldSig(tokensAndName.Value.ReturnTypeSig.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes)),
                                             (FieldAttributes) tokensAndName.Value.MethodOrFieldAttr));

                        break;
                    }

                    case MetadataTable.UserString:
                    {
                        _allImportedStrings.Add(tokensAndName.Value.FullName);
                        break;
                    }

                    case MetadataTable.TypeSpec:
                    {
                        Logger.Error("ImportTypes: TypeSpec token");
                        break;
                    }

                    case MetadataTable.Method:
                    {
                        // todo: add method flags
                        var instrumentedMethods =
                            _instrumentedAssemblyGeneratorContext.InstrumentedMethodsByModule[(OriginalModule.Name, OriginalModule.Mvid)];

                        InstrumentedMethod instrumentedMethodInfo =
                            instrumentedMethods.SingleOrDefault(rm =>
                                                rm.ModuleName.Equals(OriginalModule.Name, StringComparison.InvariantCultureIgnoreCase) &&
                                                rm.MethodToken == tokensAndName.Key.Raw &&
                                                rm.MethodName.Equals(tokensAndName.Value.MethodOrField, StringComparison.InvariantCultureIgnoreCase));

                        MethodDefUser method;
                        var parameters = tokensAndName.Value.Parameters.Select(p => p.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes)).ToArray();
                        if (instrumentedMethodInfo != null)
                        {
                            method = new MethodDefUser(tokensAndName.Value.MethodOrField,
                                                           instrumentedMethodInfo.IsStatic
                                                               ? MethodSig.CreateStatic(
                                                                   instrumentedMethodInfo.ReturnType.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes), parameters)
                                                               : MethodSig.CreateInstance(
                                                                   instrumentedMethodInfo.ReturnType.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes), parameters));
                        }
                        else
                        {
                            Logger.Debug($" {tokensAndName.Value.FullName} has no IL (extern) or we failed to write his IL in the native side");
                            method = new MethodDefUser(tokensAndName.Value.MethodOrField,
                                                       MethodSig.CreateStatic(
                                                           tokensAndName.Value.ReturnTypeSig?.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes) ?? OriginalModule.CorLibTypes.Void, parameters),
                                                       MethodAttributes.PinvokeImpl);
                        }

                        OriginalModule.Types.SingleOrDefault(t =>
                                                                 t.Name.String.Equals(tokensAndName.Value.Type, StringComparison.InvariantCultureIgnoreCase))?.Methods.Add(method);
                        _allImportedMembers.Add(method);
                        break;
                    }

                    case MetadataTable.MemberRef:
                    {
                        if (!_importedTypes.TryGetValue(tokensAndName.Value.Type, out var imported) &&
                            !AddTypeToCache(imported = ImportTypeRef(tokensAndName.Value.Type), tokensAndName))
                        {
                            break;
                        }

                        TypeSpec newTypeSpec = null;
                        if (tokensAndName.Value.IsGenericType)
                        {
                            AddTypeToCache(newTypeSpec = ImportTypeSpec(tokensAndName), tokensAndName);
                        }

                        MethodDef existingMethod = FindExistingMatchedMethod(tokensAndName, imported);

                        if (existingMethod != null)
                        {
                            MemberRef memberRef = CreateMemberRef(tokensAndName, existingMethod, newTypeSpec, imported);
                            var newMemberRef = OriginalModule.Import(memberRef);
                            _allImportedMembers.Add(newMemberRef);
                        }
                        else
                        {
                            Logger.Error($"{tokensAndName.Key.Table}: Can't find {tokensAndName.Value.FullName}");
                        }
                        break;
                    }

                    case MetadataTable.MethodSpec:
                    {
                        if (!_importedTypes.TryGetValue(tokensAndName.Value.Type, out var imported) &&
                            !AddTypeToCache(imported = ImportTypeRef(tokensAndName.Value.Type), tokensAndName))
                        {
                            break;
                        }

                        if (tokensAndName.Value.IsGenericType)
                        {
                            AddTypeToCache(ImportTypeSpec(tokensAndName), tokensAndName);
                        }

                        MethodDef existingMethod = FindExistingMatchedMethod(tokensAndName, imported);

                        if (existingMethod != null)
                        {
                            var genericInstMethodSig = tokensAndName.Value.MethodGenericParameters.Select(p => p.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes)).ToList();
                            var methodSpec = new MethodSpecUser(existingMethod, new GenericInstMethodSig(genericInstMethodSig));
                            var newMethodSpec = OriginalModule.Import(methodSpec);
                            _allImportedMembers.Add(newMethodSpec.Method);
                            _allImportedMethodsSpec.Add(newMethodSpec);
                        }
                        else
                        {
                            Logger.Error($"{tokensAndName.Key.Table}: Can't find {tokensAndName.Value.FullName}");
                        }
                        break;
                    }

                    default:
                        Logger.Error($"{nameof(ImportInstrumentedAssemblyMetadata)}: Token is unhandled - " + tokensAndName.Key);
                        break;
                }
            }
        }

        private void ImportAssemblyReferences()
        {
            foreach (string assemblyName in _assembliesToAddReference)
            {
                foreach (KeyValuePair<string, ITypeDefOrRef> importedType in _importedTypes)
                {
                    if (importedType.Value.DefinitionAssembly.Name.Equals(assemblyName))
                    {
                        var assemblyRef = OriginalModule.UpdateRowId(new AssemblyRefUser(importedType.Value.DefinitionAssembly));
                        _allImportedAssemblies.Add(assemblyRef);
                        break;
                    }
                }
            }
        }

        private MethodDef FindExistingMatchedMethod(KeyValuePair<Token, MetadataMember> tokensAndName, ITypeDefOrRef imported)
        {
            var parametersTypes = GetParametersTypesSig(tokensAndName);

            MethodDef existingMethod = null;
            var existingMethods = GetTypeRefMethodsByName(imported, tokensAndName.Value.MethodOrField);

            foreach (MethodDef method in existingMethods)
            {
                var existingMethodParameters = method.Parameters.Where(p => p.HasParamDef).ToList();
                if (existingMethodParameters.Count != parametersTypes.Count ||
                    method.GenericParameters.Count != tokensAndName.Value.MethodGenericParameters.Length)
                {
                    continue;
                }

                // No overloads
                if (existingMethodParameters.Count == 0)
                {
                    existingMethod = method;
                    break;
                }

                bool founded = SearchForMatchingParameters(existingMethodParameters, parametersTypes);

                if (founded)
                {
                    existingMethod = method;
                    break;
                }
            }

            return existingMethod;
        }

        private static MemberRef CreateMemberRef(KeyValuePair<Token, MetadataMember> tokensAndName, IMethod existingMethod, IMemberRefParent newTypeSpec, IMemberRefParent type)
        {
            MemberRef memberRef;
            if (tokensAndName.Value.IsGenericType)
            {
                memberRef = new MemberRefUser(
                    existingMethod.Module,
                    existingMethod.Name,
                    existingMethod.MethodSig,
                    newTypeSpec);
            }
            else
            {
                memberRef = new MemberRefUser(
                    existingMethod.Module,
                    existingMethod.Name,
                    existingMethod.MethodSig,
                    type);
            }

            return memberRef;
        }

        private static bool SearchForMatchingParameters(IReadOnlyList<Parameter> existingMethodParameters, IReadOnlyList<TypeSig> parametersTypes)
        {
            bool founded = true;
            for (int i = 0; i < existingMethodParameters.Count; i++)
            {
                if (parametersTypes[i].IsByRef && !existingMethodParameters[i].Type.IsByRef || !parametersTypes[i].IsByRef && existingMethodParameters[i].Type.IsByRef)
                {
                    founded = false;
                    break;
                }

                var sourceMethodVar = parametersTypes[i] as GenericMVar;
                var targetMethodVar = existingMethodParameters[i].Type as GenericMVar;
                var sourceMethodTypeVar = parametersTypes[i] as GenericVar;
                var targetMethodTypeVar = existingMethodParameters[i].Type as GenericVar;

                if (sourceMethodVar?.Number != targetMethodVar?.Number ||
                    sourceMethodTypeVar?.Number != targetMethodTypeVar?.Number)
                {
                    founded = false;
                    break;
                }

                if (sourceMethodVar == null &&
                    sourceMethodTypeVar == null &&
                    !parametersTypes[i].ContainsGenericParameter &&
                    !existingMethodParameters[i].Type.ContainsGenericParameter &&
                    !parametersTypes[i].FullName.Equals(existingMethodParameters[i].Type.FullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    founded = false;
                    break;
                }
            }

            return founded;
        }

        private List<MethodDef> GetTypeRefMethodsByName(ITypeDefOrRef type, string name)
        {
            return _instrumentedAssemblyGeneratorContext.ResolveType(type).type?.Methods
                                                        .Where(m => name.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        private List<TypeSig> GetParametersTypesSig(KeyValuePair<Token, MetadataMember> tokensAndName)
        {
            return tokensAndName.Value.Parameters.Select(p => p.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes)).ToList();
        }

        private TypeSpec ImportTypeSpec(KeyValuePair<Token, MetadataMember> tokensAndName)
        {
            var spec = new TypeSpecUser(tokensAndName.Value.TypeSig.GetTypeSig(OriginalModule, _instrumentedAssemblyGeneratorContext, _importedTypes));
            TypeSpec newTypeSpec = OriginalModule.Import(spec);
            return newTypeSpec;
        }

        private bool AddTypeToCache(ITypeDefOrRef type, KeyValuePair<Token, MetadataMember> tokensAndName)
        {
            if (type != null)
            {
                _importedTypes[type.FullName] = type;
                _allImportedTypes.Add(type);
                return true;
            }
            else
            {
                Logger.Error($"{tokensAndName.Key.Table}: Can't find member to import {tokensAndName.Value.FullName}");
                return false;
            }
        }

        private ITypeDefOrRef ImportTypeRef(string typeName)
        {
            var moduleToImportFrom = _instrumentedAssemblyGeneratorContext.AllLoadedModules.Where(loadedModule =>
            {
                if (loadedModule.FullName.Length > typeName.Length)
                { return false; }

                string[] moduleToFindNameParts = typeName.Split('.');
                string[] loadedModuleNameParts = loadedModule.FullName.Split('.');

                if (loadedModuleNameParts.Length > moduleToFindNameParts.Length)
                {
                    return false;
                }

                for (int index = 0; index < loadedModuleNameParts.Length; index++)
                {
                    if (loadedModuleNameParts[index].ToLower().Equals("dll") || loadedModuleNameParts[index].ToLower().Equals("exe"))
                    { break; }

                    if (!loadedModuleNameParts[index].Equals(moduleToFindNameParts[index], StringComparison.OrdinalIgnoreCase))
                    { return false; }
                }

                return true;
            }).ToList();

            if (moduleToImportFrom.Any())
            {
                var typeDefToImport = moduleToImportFrom.SelectMany(m => m.Types).
                    SingleOrDefault(t => t.FullName.Equals(typeName, StringComparison.InvariantCultureIgnoreCase));
                if (typeDefToImport != null)
                {
                    return OriginalModule.Import(typeDefToImport);
                }
            }

            var corLibType = OriginalModule.CorLibTypes.GetAllTypes()
                                           .SingleOrDefault(t => typeName.EndsWith(t.name, StringComparison.InvariantCultureIgnoreCase));
            if (corLibType.sig != null)
            {
                return OriginalModule.Import(corLibType.sig).ToTypeDefOrRef();
            }

            Type typeToImport = null;
            bool isSingle = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.SafeGetTypes())
                    {
                        if (type.FullName?.Equals(typeName, StringComparison.InvariantCultureIgnoreCase) == true)
                        {
                            if (typeToImport != null)
                            {
                                isSingle = false;
                                break;
                            }
                            typeToImport = type;
                        }
                    }
                    if (!isSingle)
                    {
                        break;
                    }
                }
                catch
                {
                    // nothing
                }
            }

            if (typeToImport != null)
            {
                return OriginalModule.Import(typeToImport);
            }
            Logger.Warn($"Fail to import {typeName}");
            return null;
        }

        /// <summary>
        /// Work around a weird quirk in how dnlib was built.
        /// `Import` does not actually import them to metadata,
        /// i.e. it does not populate the relevant tables with new tokens which is done only when dnlib loads the module,
        /// and for this to work the tokens must be in use so thew will be written.
        /// It's meaning that Resolve will not work, so we need to actually use these types in code
        /// (or interject ourselves into the module-writing process via dnlib events),
        /// to enforce that the metadata actually gets written.
        /// Ultimately chose the approach of code that uses these types.
        /// </summary>
        /// <exception cref="InvalidOperationException">Can't find appropriate method to inject usages</exception>
        public string EnforceUsage()
        {
            MethodDef methodToChange = FindMethodToChange();

            if (methodToChange == null)
            {
                throw new InvalidOperationException($"Can not find a safe method to inject {nameof(EnforceUsage)}");
            }

            IMethod getTypeFromHandleRef = OriginalModule.Import(_instrumentedAssemblyGeneratorContext.ResolveType(ImportTypeRef(typeof(Type).FullName)).type.Methods.FirstOrDefault(m => m.Name == "GetTypeFromHandle"));
            IMethod getMethodFromHandleRef = OriginalModule.Import(_instrumentedAssemblyGeneratorContext.ResolveType(ImportTypeRef(typeof(MethodBase).FullName)).type.Methods.FirstOrDefault(m => m.Name == "GetMethodFromHandle" && m.Parameters.Count == 1 && m.Parameters.First().Type.TypeName == "RuntimeMethodHandle"));

            Logger.Debug($"{nameof(EnforceUsage)} instructions injected to: {methodToChange.FullName}");

            methodToChange.Body.Instructions.RemoveAt(methodToChange.Body.Instructions.Count - 1);
            var newInstructions = new List<Instruction>();

            foreach (var typeDefOrRef in _allImportedTypes)
            {
                newInstructions.Add(new Instruction(OpCodes.Ldtoken, typeDefOrRef));
                newInstructions.Add(new Instruction(OpCodes.Callvirt, getTypeFromHandleRef));
                newInstructions.Add(new Instruction(OpCodes.Pop));
            }

            foreach (IMethodDefOrRef methodDefOrRef in _allImportedMembers)
            {
                newInstructions.Add(new Instruction(OpCodes.Ldtoken, methodDefOrRef));
                newInstructions.Add(new Instruction(OpCodes.Callvirt, getMethodFromHandleRef));
                newInstructions.Add(new Instruction(OpCodes.Pop));
            }

            foreach (MethodSpec methodSpec in _allImportedMethodsSpec)
            {
                newInstructions.Add(new Instruction(OpCodes.Ldtoken, methodSpec));
                newInstructions.Add(new Instruction(OpCodes.Callvirt, getMethodFromHandleRef));
                newInstructions.Add(new Instruction(OpCodes.Pop));
            }

            foreach (AssemblyRef assemblyRef in _allImportedAssemblies)
            {
                newInstructions.Add(new Instruction(OpCodes.Ldtoken, assemblyRef));
                newInstructions.Add(new Instruction(OpCodes.Callvirt, getTypeFromHandleRef));
                newInstructions.Add(new Instruction(OpCodes.Pop));
            }

            foreach (string userString in _allImportedStrings)
            {
                newInstructions.Add(new Instruction(OpCodes.Ldstr, userString));
                newInstructions.Add(new Instruction(OpCodes.Callvirt, getTypeFromHandleRef));
                newInstructions.Add(new Instruction(OpCodes.Pop));
            }

            foreach (var instruction in newInstructions)
            {
                methodToChange.Body.Instructions.Add(instruction);
            }

            methodToChange.Body.Instructions.Add(new Instruction(OpCodes.Ret));
            methodToChange.Body.UpdateInstructionOffsets();

            return methodToChange.FullName;
            MethodDef FindMethodToChange()
            {
                var instrumentedMethods = _instrumentedAssemblyGeneratorContext.InstrumentedMethodsByModule[(OriginalModule.Name, OriginalModule.Mvid)];
                // Search for a simple method (i.e. without EHS etc.)
                // that already exists in the module, in order to instrument it with the new added tokens.
                var allMethods = OriginalModule.Types.SelectMany(t => t.Methods).ToList();
                var methodFounded = allMethods.FirstOrDefault(
                    method =>
                        method.HasBody &&
                        !method.Body.HasExceptionHandlers &&
                        !method.Body.Instructions.Any(ins => ins.IsConditionalBranch()) &&
                        instrumentedMethods.All(m => m.MethodName != method.Name));
                return methodFounded;
            }
        }
    }
}
