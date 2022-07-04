using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.InstrumentedAssemblyGenerator
{
    /// <summary>
    /// This class is a sink for all available modules and their members
    /// We use it when we want dummy resolve TypeRef to TypeDef
    /// </summary>
    internal class InstrumentedAssemblyGeneratorContext
    {
        public readonly List<ModuleDefMD> AllLoadedModules;
        private readonly Dictionary<Token, TypeDef> _exportedTypesDefinitions;

        internal ModuleDefMD[] OriginalsModulesOfInstrumentedMembers { get; }
        internal Dictionary<(string moduleName, Guid?), ModuleTokensMapping> OriginalModulesTypesTokens { get; }
        internal Dictionary<(string moduleName, Guid? mvid), ModuleTokensMapping> InstrumentedModulesTypesTokens { get; }

        public ILookup<(string moduleName, Guid? mvid), InstrumentedMethod> InstrumentedMethodsByModule { get; }
        private readonly AssemblyEqualityComparer _assemblyComparer;

        public InstrumentedAssemblyGeneratorContext(List<ModuleDefMD> allLoadedModules,
                                                    ModuleDefMD[] originalModulesOfInstrumentedMembers,
                                                    Dictionary<(string moduleName, Guid? mvid), ModuleTokensMapping> originalModulesTypesTokens,
                                                    Dictionary<(string moduleNAme, Guid? mvid), ModuleTokensMapping> instrumentedModulesTypesTokens,
                                                    ILookup<(string moduleName, Guid? mvid), InstrumentedMethod> instrumentedMethodsByModule)
        {
            AllLoadedModules = allLoadedModules;
            OriginalsModulesOfInstrumentedMembers = originalModulesOfInstrumentedMembers;
            OriginalModulesTypesTokens = originalModulesTypesTokens;
            _exportedTypesDefinitions = new Dictionary<Token, TypeDef>();
            InstrumentedModulesTypesTokens = instrumentedModulesTypesTokens;
            InstrumentedMethodsByModule = instrumentedMethodsByModule;
            _assemblyComparer = new AssemblyEqualityComparer();
        }

        internal (ModuleDef scope, TypeDef type) ResolveType(ITypeDefOrRef defOrRef)
        {
            if (defOrRef is TypeDef def)
            {
                return (def.Module, def);
            }

            if (defOrRef is not TypeRef typeRef)
            {
                return (null, null);
            }

            var token = new Token(typeRef.MDToken.Raw);
            if (_exportedTypesDefinitions.TryGetValue(token, out TypeDef typeDef))
            {
                return (typeDef.Module, typeDef);
            }

            ModuleDef definitionAssembly = GetDefinitionAssembly(typeRef);

            if (definitionAssembly == null)
            {
                Logger.Error($"Assembly definition: '{typeRef.DefinitionAssembly}' not found, for TypeRef '{typeRef.FullName}'");
                _exportedTypesDefinitions.Add(token, null);
                return (null, null);
            }

            var resolvedTypeDef = GetTypeDefFromDefinitionAssembly(typeRef, definitionAssembly);

            if (resolvedTypeDef == null)
            {
                Logger.Error($"TypeDef not found for TypeRef: '{typeRef.FullName}' in assembly: '{definitionAssembly.FullName}'");
                _exportedTypesDefinitions.Add(token, null);
                return (null, null);
            }

            _exportedTypesDefinitions.Add(token, resolvedTypeDef);
            return (definitionAssembly, resolvedTypeDef);
        }

        private TypeDef GetTypeDefFromDefinitionAssembly(TypeRef typeRef, ModuleDef definitionAssembly)
        {
            var resolvedTypeDef = definitionAssembly.GetTypes().SingleOrDefault(t => t.FullName == typeRef.FullName);
            if (resolvedTypeDef != null)
            {
                return resolvedTypeDef;
            }

            var exportedType = definitionAssembly.ExportedTypes.SingleOrDefault(t => t.FullName == typeRef.FullName);
            if (exportedType != null)
            {
                definitionAssembly = AllLoadedModules.FirstOrDefault(m => _assemblyComparer.Equals(m.Assembly, exportedType.DefinitionAssembly));
                if (definitionAssembly != null)
                {
                    resolvedTypeDef = definitionAssembly.GetTypes().SingleOrDefault(t => t.FullName == typeRef.FullName);
                }
            }
            else if (definitionAssembly.FullName is "System.Private.CoreLib.dll" or "netstandard.dll" or "System.Runtime.dll")
            {
                foreach (var module in AllLoadedModules)
                {
                    resolvedTypeDef = module.GetTypes().SingleOrDefault(t => t.FullName == typeRef.FullName);
                    if (resolvedTypeDef != null)
                    {
                        break;
                    }
                }
            }

            return resolvedTypeDef;
        }

        private ModuleDef GetDefinitionAssembly(TypeRef typeRef)
        {
            ModuleDefMD definitionAssembly;
            if (typeRef.DefinitionAssembly.IsCorLib())
            {
                // Could be Private.Corlib & mscorlib, but in this case I'll take the *first one loaded*
                definitionAssembly = AllLoadedModules.FirstOrDefault(m => m.IsCoreLibraryModule != null && m.IsCoreLibraryModule.Value);
            }
            else
            {
                definitionAssembly = AllLoadedModules.FirstOrDefault(m => _assemblyComparer.Equals(m.Assembly, typeRef.DefinitionAssembly));
            }

            if (definitionAssembly == null)
            {
                var assemblyByName = AllLoadedModules.FirstOrDefault(m => m.Assembly.Name == typeRef.DefinitionAssembly.Name);
                if (assemblyByName != null)
                {
                    definitionAssembly = assemblyByName;
                }
            }

            return definitionAssembly;
        }

        internal TypeDef ResolveExportedType(ExportedType et)
        {
            var token = new Token(et.MDToken.Raw);
            if (_exportedTypesDefinitions.TryGetValue(token, out TypeDef typeDef))
            {
                return typeDef;
            }

            ModuleDefMD definitionAssembly;
            if (et.DefinitionAssembly.IsCorLib())
            {
                definitionAssembly = AllLoadedModules.SingleOrDefault(m => m.IsCoreLibraryModule != null && m.IsCoreLibraryModule.Value);
            }
            else
            {
                definitionAssembly = AllLoadedModules.SingleOrDefault(m => m.Assembly.FullName == et.DefinitionAssembly.FullName);
            }

            if (definitionAssembly == null)
            {
                Logger.Error($"Assembly definition: '{et.DefinitionAssembly}' not found, for ExportedType '{et.FullName}'");
                _exportedTypesDefinitions.Add(token, null);
                return null;
            }

            var resolvedTypeDef = definitionAssembly.GetTypes().SingleOrDefault(t => t.FullName == et.FullName);

            if (resolvedTypeDef == null)
            {
                var exported = definitionAssembly.ExportedTypes.SingleOrDefault(t => t.FullName == et.FullName);
                if (exported != null)
                {
                    return ResolveExportedType(exported);
                }

                Logger.Error($"TypeDef not found for ExportedType: '{et.FullName}' in assembly: '{definitionAssembly.FullName}'");
                _exportedTypesDefinitions.Add(token, null);
                return null;
            }

            _exportedTypesDefinitions.Add(token, resolvedTypeDef);
            return resolvedTypeDef;
        }

        internal ITypeDefOrRef ResolveInstrumentedMappedType(ModuleDef module, MetadataMember metadataMember, Token originalToken, Dictionary<string, ITypeDefOrRef> importedTypes)
        {
            ITypeDefOrRef type = null;
            switch (originalToken.Table)
            {
                case MetadataTable.TypeDef:
                {
                    type = module.GetTypes().FirstOrDefault(t => t.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase));
                    break;
                }
                case MetadataTable.TypeRef:
                {
                    importedTypes?.TryGetValue(metadataMember.Type, out type);
                    type ??= module.GetTypeRefs().FirstOrDefault(t => t.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase));
                    break;
                }
                case MetadataTable.TypeSpec:
                {
                    var module2 = module as ModuleDefMD;
                    for (uint i = 0; i <= module2?.Metadata.TablesStream.TypeSpecTable.Rows; i++)
                    {
                        if (!module2.Metadata.TablesStream.TryReadTypeSpecRow(i, out var row))
                        {
                            continue;
                        }

                        var typeSpec = module2.ResolveTypeSpec(i);
                        if (typeSpec.FullName.Equals(metadataMember.FullName, StringComparison.InvariantCultureIgnoreCase) ||
                            typeSpec.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase))
                        {
                            type = typeSpec;
                            break;
                        }

                        if (typeSpec.ScopeType?.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase) == true &&
                            typeSpec.TypeSig is GenericInstSig instSig &&
                            string.Join(",", instSig.GenericArguments.Select(ga => ga.FullName))
                                  .Equals(string.Join(",", metadataMember.TypeGenericParameters.Select(ga => ga.GetTypeSig(module2, this).FullName))))
                        {
                            type = typeSpec;
                            break;
                        }
                    }
                    break;
                }

                default:
                    Logger.Error($"{nameof(ResolveInstrumentedMappedType)}: Case {originalToken.Table} is not implemented. {metadataMember}");
                    break;
            }

            if (type == null)
            {
                var corlib = module.GetAssemblyRef("mscorlib") ?? module.GetAssemblyRef("System.Private.CoreLib");
                type = module.CorLibTypes.GetCorLibTypeSig(
                    "System",
                    metadataMember.Type.Substring(metadataMember.Type.IndexOf(".", StringComparison.Ordinal) + 1), corlib)?.TypeDefOrRef;
            }

            if (type == null)
            {
                throw new InvalidOperationException($"{nameof(ResolveInstrumentedMappedType)}: Can not find type {metadataMember}");
            }

            return type;
        }

        internal IMethod ResolveInstrumentedMappedMethod(ModuleDef module, MetadataMember metadataMember, Token originalToken, GenericParamContext genericContext)
        {
            IMethod method = null;
            switch (originalToken.Table)
            {
                case MetadataTable.Method:
                {
                    method = ResolveMethodDef(module, metadataMember);
                    break;
                }

                case MetadataTable.MemberRef:
                {
                    method = ResolveMemberRef(module, metadataMember);
                    break;
                }

                case MetadataTable.MethodSpec:
                {
                    method = ResolveMethodSpec(module, metadataMember);
                    break;
                }

                default:
                    Logger.Error($"{nameof(ResolveInstrumentedMappedMethod)}: Case {originalToken.Table} is not implemented. {metadataMember}");
                    break;
            }

            if (method == null)
            {
                throw new InvalidOperationException($"{nameof(ResolveInstrumentedMappedMethod)}: Can't find method {metadataMember}");
            }

            uint newToken = method.MDToken.Raw;
            return module.ResolveToken(newToken, genericContext) as IMethod;
        }

        internal IField ResolveInstrumentedMappedField(ModuleDef module, Token originalToken, MetadataMember metadataMember, GenericParamContext genericContext)
        {
            IField field = null;
            switch (originalToken.Table)
            {
                case MetadataTable.MemberRef:
                    field = module.GetMemberRefs(genericContext).FirstOrDefault(mr => mr.FullName.Equals(metadataMember.FullName, StringComparison.InvariantCultureIgnoreCase));
                    break;

                case MetadataTable.Field:
                    field = module.GetTypes().SelectMany(t => t.Fields).
                                   FirstOrDefault(f => f.FullName.Equals(metadataMember.FullName, StringComparison.InvariantCultureIgnoreCase) ||
                                                       f.Name.String.Equals(metadataMember.MethodOrField, StringComparison.InvariantCultureIgnoreCase) &&
                                                       f.DeclaringType.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase) &&
                                                       f.FieldType.FullName.Equals(metadataMember.ReturnTypeSig.GetTypeSig(module, this).FullName, StringComparison.InvariantCultureIgnoreCase));
                    break;
                default:
                    Logger.Error($"{nameof(ResolveInstrumentedMappedField)}: Case {originalToken.Table} is not implemented. {metadataMember}");
                    break;
            }

            if (field == null)
            {
                throw new InvalidOperationException($"{nameof(ResolveInstrumentedMappedField)}: Can not find field {metadataMember}");
            }
            return field;
        }

        private MethodDef ResolveMethodDef(ModuleDef module, MetadataMember metadataMember)
        {
            var candidatesDefs = module.GetTypes().SelectMany(t => t.Methods)
                           .Where(m => m.FullName.Equals(metadataMember.FullName, StringComparison.InvariantCultureIgnoreCase) ||
                                      m.Name.String.Equals(metadataMember.MethodOrField, StringComparison.InvariantCultureIgnoreCase) &&
                                        m.GetParams().Count == metadataMember.Parameters.Length &&
                                                 (m.DeclaringType == null ||
                                                  m.DeclaringType.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase) ||
                                                  m.DeclaringType.Interfaces.Any(@interface => @interface.Interface.FullName.Equals(metadataMember.Type, StringComparison.InvariantCultureIgnoreCase)))).ToList();

            var founded = candidatesDefs.FirstOrDefault(cd => cd.FullName.Equals(metadataMember.FullName, StringComparison.InvariantCultureIgnoreCase));
            if (founded != null)
            {
                return founded;
            }

            if (candidatesDefs.Count == 1)
            {
                return candidatesDefs[0];
            }

            MethodDef method = null;
            var parametersTypes = metadataMember.Parameters.Select(p => (IFullName) p.GetTypeSig(module, this)).ToList();
            foreach (var candidatesDef in candidatesDefs)
            {
                var candidateParams = candidatesDef.GetParams();
                for (int i = 0; i < metadataMember.Parameters.Length; i++)
                {
                    if (candidateParams[i].FullName != parametersTypes[i].FullName)
                    {
                        method = null;
                        break;
                    }

                    method = candidatesDef;
                }

                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private MemberRef ResolveMemberRef(ModuleDef module, MetadataMember metadataMember)
        {
            var parametersTypes = metadataMember.Parameters.Select(p => (IFullName) p.GetTypeSig(module, this)).ToList();
            foreach (var memberRef in module.GetMemberRefs())
            {
                if (!memberRef.Name.String.Equals(metadataMember.MethodOrField, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (memberRef.GetParams().Count != metadataMember.Parameters.Length)
                {
                    continue;
                }

                if (memberRef.DeclaringType != null)
                {
                    if (memberRef.DeclaringType.NumberOfGenericParameters != metadataMember.TypeGenericParameters.Length)
                    {
                        continue;
                    }

                    if (!memberRef.DeclaringType.FullName.Equals(((IFullName) metadataMember.TypeSig.GetTypeSig(module, this))?.FullName ?? metadataMember.Type, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                }

                if (metadataMember.Parameters.Length == 0)
                {
                    return memberRef;
                }

                var candidateParams = memberRef.GetParams();
                bool founded = true;
                for (int i = 0; i < metadataMember.Parameters.Length; i++)
                {
                    if (!candidateParams[i].FullName.Equals(parametersTypes[i].FullName, StringComparison.InvariantCultureIgnoreCase) &&
                        !candidateParams[i].ContainsGenericParameter)
                    {
                        founded = false;
                        break;
                    }

                    if (candidateParams[i].ContainsGenericParameter &&
                        candidateParams[i].ElementType != ElementType.Var &&
                        candidateParams[i].ElementType != ElementType.MVar)
                    {
                        string name = parametersTypes[i].FullName;
                        int startOfGenericArgs = MetadataNameParser.GetStartOfGenericIndex(name);
                        if (startOfGenericArgs < 0 || !name.Substring(0, startOfGenericArgs).Equals(candidateParams[i].FullName.Substring(0, startOfGenericArgs), StringComparison.InvariantCultureIgnoreCase))
                        {
                            founded = false;
                            break;
                        }

                        int closedGenericIndex = name.Substring(startOfGenericArgs).LastIndexOf('>') + startOfGenericArgs;
                        if (MetadataNameParser.GetGenericParamsFromMethodOrTypeName(name, startOfGenericArgs, closedGenericIndex).Length != candidateParams[i].ToGenericInstSig().GenericArguments.Count)
                        {
                            founded = false;
                            break;
                        }
                    }
                }

                if (founded)
                {
                    return memberRef;
                }
            }

            return null;
        }

        private MethodSpec ResolveMethodSpec(ModuleDef module, MetadataMember metadataMember)
        {
            var module2 = module as ModuleDefMD;
            var parametersTypes = metadataMember.Parameters.Select(p => (IFullName) p.GetTypeSig(module, this)).ToList();
            for (uint i = 0; i <= module2?.Metadata.TablesStream.MethodSpecTable.Rows; i++)
            {
                if (!module2.Metadata.TablesStream.TryReadMethodSpecRow(i, out _))
                {
                    continue;
                }

                // check equality for name, number of params and number of generic params
                var methodSpec = module2.ResolveMethodSpec(i);
                if (methodSpec.Name.String.Equals(metadataMember.MethodOrField, StringComparison.InvariantCultureIgnoreCase) &&
                    methodSpec.Method.NumberOfGenericParameters == metadataMember.MethodGenericParameters.Length &&
                    methodSpec.Method.GetParamCount() == metadataMember.Parameters.Length)
                {
                    // existing types full name are equals, there is match
                    if (methodSpec.FullName.Equals(metadataMember.FullName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return methodSpec;
                    }

                    // check for parameters and generic parameters definition
                    // try populate instantiation parameters
                    var candidateInstParams = methodSpec.Method.GetParams().Select(p => p.FullName).ToList();
                    var instMethodSpecParams = methodSpec.GenericInstMethodSig.GetGenericArguments();
                    if (candidateInstParams.Count > 0 && instMethodSpecParams.Count > 0)
                    {
                        for (int j = 0; j < candidateInstParams.Count; j++)
                        {
                            string candidateParam = candidateInstParams[j];
                            int indexOfVarOrMvar = candidateParam.IndexOf("!", StringComparison.InvariantCultureIgnoreCase);
                            if (indexOfVarOrMvar >= 0)
                            {
                                int startCounter = 1;
                                int endCounter = 1;
                                while (startCounter < candidateParam.Length &&
                                       !char.IsNumber(candidateParam[indexOfVarOrMvar + startCounter]))
                                {
                                    startCounter++;
                                }

                                endCounter = startCounter + 1;
                                while (endCounter < candidateParam.Length &&
                                       char.IsNumber(candidateParam[indexOfVarOrMvar + endCounter]))
                                {
                                    endCounter++;
                                }

                                string mvarNumber = candidateParam.Substring(startCounter + indexOfVarOrMvar, endCounter - startCounter);
                                var instParam = instMethodSpecParams[int.Parse(mvarNumber)];
                                string varOrMvarString = candidateParam.Substring(indexOfVarOrMvar, endCounter);
                                candidateInstParams[j] = candidateParam.Replace(varOrMvarString, instParam.FullName);
                            }
                        }
                    }

                    bool isMatch = true;
                    var candidateParams = methodSpec.Method.GetParams().Select(p => p.FullName).ToList();
                    for (int j = 0; j < metadataMember.Parameters.Length; j++)
                    {
                        // check for parameters, instantiated or not
                        if (candidateInstParams[j] != parametersTypes[j].FullName &&
                            candidateParams[j] != parametersTypes[j].FullName)
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch)
                    {
                        // check for generic parameters
                        var genericArgsTypes = metadataMember.MethodGenericParameters.Select(p => (IFullName) p.GetTypeSig(module, this)).ToList();
                        for (int j = 0; j < metadataMember.MethodGenericParameters.Length; j++)
                        {
                            if (instMethodSpecParams[j].FullName != genericArgsTypes[j].FullName)
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        return methodSpec;
                    }
                }
            }

            return null;
        }
    }
}
