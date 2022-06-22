using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using dnlib.IO;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class InstrumentedAssemblyGenerator
    {
        private readonly AssemblyGeneratorArgs _args;
        private readonly ModuleContext _moduleContext;
        private readonly List<ModuleDefMD> _allLoadedModules;
        internal List<(string modulePath, List<string> methods)> ExportedModulesPathAndMethods { get; set; } = new();
        internal InstrumentedAssemblyGeneratorContext ModuleRewriteContext { get; private set; }

        public InstrumentedAssemblyGenerator(AssemblyGeneratorArgs args)
        {
            _args = args;
            _moduleContext = CreateModuleContext();
            _allLoadedModules = new List<ModuleDefMD>();
        }

        private ModuleContext CreateModuleContext()
        {
            // use shared module context between all modules
            // See: https://github.com/0xd4d/dnlib#resolving-references
            ModuleContext modCtx = ModuleDef.CreateModuleContext();
            var asmResolver = (AssemblyResolver)modCtx.AssemblyResolver;
            asmResolver.EnableTypeDefCache = true;
            return modCtx;
        }

        internal void Initialize()
        {
            var instrumentedMethodsByModule = GetInstrumentedMethodsByModule();
            var metadataChangesByModule = GetMetadataChangesByModule();

            var originalModulesPaths = ReadOriginalModulesPathsFromFile();
            var originalsModulesOfRewrittenMembers = GetOriginalModulesMetadata(instrumentedMethodsByModule, originalModulesPaths);

            LoadRelevantTokens(originalsModulesOfRewrittenMembers);
            var originalModulesTokensMaps = GetOriginalModulesTokensMap(originalsModulesOfRewrittenMembers);

            ModuleRewriteContext = new InstrumentedAssemblyGeneratorContext(_allLoadedModules,
                                                                            originalsModulesOfRewrittenMembers,
                                                                            originalModulesTokensMaps,
                                                                            metadataChangesByModule,
                                                                            instrumentedMethodsByModule);
        }

        private static void LoadRelevantTokens(ModuleDefMD[] originalsModulesOfRewrittenMembers)
        {
            Logger.Info("Loading all modules metadata");
            foreach (var originalModule in originalsModulesOfRewrittenMembers)
            {
                originalModule.LoadEverything();
            }
        }

        private Dictionary<(string, Guid?), ModuleTokensMapping> GetOriginalModulesTokensMap(ModuleDefMD[] originalsModulesOfRewrittenMembers)
        {
            // We must create a mapping between the tokens in the original module and the new ones,
            // as otherwise, when we parse method body, the original tokens will point to different types than what we'd expect.
            // This is because:
            // 1. When dnlib writes out assemblies, it does not include unused tokens in the metadata.
            // 2. When dnlib rebuild the metadata tables, depending on what changes were made, it may completely reorder the tokens.
            Logger.Info("Mapping original modules tokens");
            var originalModulesTokensMap = originalsModulesOfRewrittenMembers.
                                           Select(ModuleTokensMapping.CreateFromModule).
                                           ToDictionary(module => (module.ModuleName, module.ModuleMvid));
            return originalModulesTokensMap;
        }

        private ModuleDefMD[] GetOriginalModulesMetadata(
            ILookup<(string ModuleName, Guid? Mvid), InstrumentedMethod> instrumentedMethodsByModule, 
            List<string> originalModulesPaths)
        {
            Logger.Info("Collecting original modules metadata");
            // Collect all original modules that participating in the test - i.e. module name & mvid are same
            var originalsModulesOfRewrittenMembers = (from file in originalModulesPaths.Distinct()
                                                      let extension = Path.GetExtension(file).ToLower()
                                                      where extension is ".dll" or ".exe"
                                                      select LoadModuleWithDnlibSafe(file) into module
                                                      where module?.Mvid != null
                                                      let key = (module.Name, (Guid)module.Mvid)
                                                      //because the key contains mvid, we must be in the same compilation
                                                      //for the original dll and the module exist in the "txt@*.instrlog" file
                                                      where instrumentedMethodsByModule.Contains(key)
                                                      select module).ToArray();

            if (originalsModulesOfRewrittenMembers.Length != instrumentedMethodsByModule.Count)
            {
                // if we have in the list multiple modules with same name, we will fail but it should not an issue for instrumentation so skip this case
                if (instrumentedMethodsByModule.Select(d => d.Key.ModuleName).Distinct().Count() != originalsModulesOfRewrittenMembers.Length)
                {
                    throw new InvalidOperationException(
                        "Can't find all original modules to modify. " +
                        "Verify that modules in 'INPUT_OriginalAssemblies' directory are from the same compilation as it exported in '.instrlog' file");
                }
            }

            if (_args.ModulesToGenerate.Any())
            {
                originalsModulesOfRewrittenMembers = originalsModulesOfRewrittenMembers
                                                    .Where(mod =>
                                                               _args.ModulesToGenerate.Any(mod2 => mod2 == Path.GetFileName(mod.Location))).
                                                     ToArray();

                if (originalsModulesOfRewrittenMembers.Any() == false)
                {
                    throw new InvalidOperationException("There is no modules to verify. Check 'ModulesToGenerate' argument");
                }
            }

            return originalsModulesOfRewrittenMembers;
        }

        private Dictionary<(string, Guid?), ModuleTokensMapping> GetMetadataChangesByModule()
        {
            Logger.Info("Collecting metadata changes");
            // Collect all metadata changes and store them as a mapping -
            // from the original (module, token) pair of the thing that was added by the CLR profiler,
            // to a new object (MetadataMember) that represents it
            var metadataChangesByModule =
                Directory.EnumerateFiles(_args.InstrumentationInputLogs, $"*{ModuleMembersFileExtension}").
                          Select(moduleFile => ModuleTokensMapping.ReadFromFile(moduleFile, _args.ModulesToGenerate)).
                          Where(res => res != null).
                          ToDictionary(module => (module.ModuleName, module.ModuleMvid));

            if (!metadataChangesByModule.Any())
            {
                throw new InvalidOperationException("Can't find instrumented modules. " +
                                                    $"Check that the files exist in {_args.InstrumentationInputLogs} " +
                                                    "and verify that both the file names and extensions are in the expected pattern");
            }

            return metadataChangesByModule;
        }

        private ILookup<(string ModuleName, Guid? Mvid), InstrumentedMethod> GetInstrumentedMethodsByModule()
        {
            Logger.Info("Collecting instrumented methods");
            // Collect all instrumented method and group them by their module name and mvid (module -> methods)
            var instrumentedMethodsByModule =
                Directory.EnumerateFiles(_args.InstrumentationInputLogs, $"{TextFilePrefix}*{InstrumentedLogFileExtension}").
                          Select(InstrumentedMethod.ReadFromFile).
                          Where(res => res != null &&
                                       (_args.ModulesToGenerate.Length == 0 || _args.ModulesToGenerate.Contains(res.ModuleName))).
                          ToLookup(method => (method.ModuleName, method.Mvid));

            if (!instrumentedMethodsByModule.Any())
            {
                throw new InvalidOperationException("Can't find instrumented methods. " +
                                                    $"Check that instrumentation files exist under {_args.InstrumentationInputLogs} " +
                                                    "and verify that it from correct pattern - name and extension");
            }

            return instrumentedMethodsByModule;
        }

        private List<string> ReadOriginalModulesPathsFromFile()
        {
            Logger.Info("\r\nSanitize modules path");

            var newLines =
                (from line in File.ReadAllLines(_args.OriginalModulesFilePath)
                 where !string.IsNullOrWhiteSpace(line)
                 let line2 = line.Trim(new char['"'])
                 let ext = Path.GetExtension(line2)
                 where ext.ToLower() is ".dll" or ".exe"
                 let fileName = Path.GetFileName(line2)
                 where fileName != null
                 select Path.Combine(_args.OriginalModulesFolder, fileName)).ToList();

            if (!newLines.Any())
            {
                throw new InvalidOperationException($"No valid module found in file: {_args.OriginalModulesFilePath}{Environment.NewLine}Verify that module end with 'exe' or 'dll'.");
            }

            File.WriteAllLines(_args.OriginalModulesFilePath, newLines);

            var originalModules = File.ReadLines(_args.OriginalModulesFilePath).ToList();
            return originalModules;
        }

        private ModuleDefMD LoadModuleWithDnlibSafe(string filePath)
        {
            try
            {
                var module = ModuleDefMD.Load(filePath);
                if (_moduleContext != null)
                {
                    // Use the previously created (and shared) context
                    module.Context = _moduleContext;
                    //This code implicitly assumes we're using the default assembly resolver
                    ((AssemblyResolver)module.Context.AssemblyResolver).AddToCache(module);
                }
                _allLoadedModules.Add(module);
                return module;
            }
            catch (BadImageFormatException e)
            {
                // Not .NET dll
                Logger.Warn($"Could not load {filePath} because it is not a .NET assembly. Full details: {e}");
                return null;
            }
            catch (DataReaderException e)
            {
                // In case of invalid module file, we don't care about it,
                // because if the instrumented app worked properly, its dll's are good.
                // This can be if in the same folder there is an invalid dll
                Logger.Warn($"Could not load {filePath} because it is not a valid assembly. Full details: {e}");
                return null;
            }
            catch (FileNotFoundException e)
            {
                Logger.Warn($"Could not load {filePath} because the file was not found. Full details: {e}");
                return null;
            }
            catch (IOException e)
            {
                if (!e.Message.EndsWith("for reading. Error: 00000002"))
                {
                    Logger.Debug($"Could not load {filePath}. Full details: {e}");
                }

                return null;
            }
        }

        internal InstrumentedAssemblyGenerationResult ModifyMethods()
        {
            var modifiedModules = AddInstrumentedAssemblyMetadata();
            int succeededCounter = 0;
            var result = InstrumentedAssemblyGenerationResult.Succeeded;
            foreach (IGrouping<(string ModuleName, Guid? Mvid), InstrumentedMethod> methods in ModuleRewriteContext.InstrumentedMethodsByModule)
            {
                try
                {
                    var module = modifiedModules.SingleOrDefault(m => m.Name == methods.Key.ModuleName && m.Mvid == methods.Key.Mvid);
                    if (module == null)
                    {
                        Logger.Warn($"Can't find module {methods.Key.ModuleName}");
                        continue;
                    }

                    using (var memoryStream = new MemoryStream())
                    {
                        // in order to read the final and up-to-date representation of the assembly,
                        // dnlib works in a way that we must write it to stream or disk and re-read it
                        // (or we can add the new metadata in the saving operation events).
                        module.Write(memoryStream);
                        module = ModuleDefMD.Load(memoryStream);
                        module.LoadEverything();
                    }

                    Logger.Info($"Modifying methods of module: {module.Name}");
                    var methodsModified = new List<string>();
                    foreach (var instrumentedMethod in methods)
                    {
                        try
                        {
                            ModifyMethod(module, instrumentedMethod, ModuleRewriteContext.InstrumentedModulesTypesTokens[methods.Key], ModuleRewriteContext.OriginalModulesTypesTokens[methods.Key]);
                            Logger.Debug($"Method: {instrumentedMethod} has modified");

                            methodsModified.Add(instrumentedMethod.ToString() + $"({string.Join(",", instrumentedMethod.ArgumentsNames.Select(n => n.GetTypeSig(module, ModuleRewriteContext).FullName))})");
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Failed to apply the instrumented bytecode on method: {instrumentedMethod.TypeName}.{instrumentedMethod.MethodName}. Error was: " +
                                                           $"{Environment.NewLine}{e}");
                            result = InstrumentedAssemblyGenerationResult.PartiallySucceeded;
                        }
                    }

                    if (methodsModified.Count == 0)
                    {
                        Logger.Warn($"No methods were modified, so rewritten module will not be saved");
                        result = InstrumentedAssemblyGenerationResult.PartiallySucceeded;
                        continue;
                    }

                    Logger.Debug($"{methodsModified.Count} method(s) were modified");

                    string exportPath = Path.Combine(_args.InstrumentedAssembliesFolder, module.FullName);

                    Logger.Info($"Writing instrumented module: '{module.FullName}'");
                    var moduleWriteLogger = new ModuleWriterLogger();
                    var writeOptions = new ModuleWriterOptions(module)
                    {
                        Logger = moduleWriteLogger,
                    };
                    writeOptions.WriterEvent += ModuleWriterEvent;

                    module.Write(exportPath, writeOptions);
                    Logger.Successful($"Module: '{module.FullName}' was successfully written to: '{exportPath}'");

                    ExportedModulesPathAndMethods.Add((exportPath, methodsModified));
                    succeededCounter++;

                    if (moduleWriteLogger.ErrorsBuilder.Length > 0)
                    {
                        Logger.Info($"{Environment.NewLine}---- Methods that potentially can cause to InvalidProgramException in {module.FullName}:");
                        Logger.Error($"{moduleWriteLogger.ErrorsBuilder}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to modify module. Module: {methods.Key.ModuleName}. Error: {ex.Message}");
                    result = InstrumentedAssemblyGenerationResult.PartiallySucceeded;
                }
            }

            if (succeededCounter == 0)
            {
                return InstrumentedAssemblyGenerationResult.Failed;
            }

            return result;
        }

        private void ModifyMethod(ModuleDefMD module,
                                  InstrumentedMethod instrumentedMethod,
                                  ModuleTokensMapping instrumentedModuleTokens,
                                  ModuleTokensMapping originalModuleTokens)
        {
            MethodDef methodDef = FindInstrumentedMethod(module, instrumentedMethod, originalModuleTokens);

            if (methodDef == null)
            {
                throw new InvalidOperationException($"Could not find method {instrumentedMethod.MethodName}");
            }

            if (!methodDef.HasBody && instrumentedMethod.Code.Value.Length == 0)
            {
                Logger.Error($"Method {methodDef.FullName} has no body");
                return;
            }

            var locals = new LocalsCreator(instrumentedMethod, module, ModuleRewriteContext);
            var codeReader = ByteArrayDataReaderFactory.CreateReader(instrumentedMethod.Code.Value);
            var methodBodyReader =
                new InstrumentedMethodBodyReader(module,
                    codeReader,
                    methodDef.Parameters,
                    GenericParamContext.Create(methodDef),
                    ModuleRewriteContext,
                    instrumentedModuleTokens,
                    originalModuleTokens,
                    locals.CreateTypesSig().ToArray());

            if (!methodBodyReader.Read())
            {
                throw new InvalidOperationException("Can't read method body");
            }

            var body = methodBodyReader.CreateCilBody();
            methodDef.Body = body;
            LogMethodInstructions(methodDef);
        }

        private MethodDef FindInstrumentedMethod(ModuleDefMD module, InstrumentedMethod instrumentedMethod, ModuleTokensMapping originalModuleTokens)
        {
            var token = new Token((uint)instrumentedMethod.MethodToken);
            MethodDef method;
            var moduleMethods = module.GetTypes()
                                      .SelectMany(t => t.Methods);

            // If method doesn't exist in original module tokens, meaning we instrumenting a new added MethodDef
            if (!originalModuleTokens.TokensAndNames.TryGetValue(token, out var member))
            {
                Logger.Debug($"Method {instrumentedMethod.TypeName}.{instrumentedMethod.MethodName} not found in original module");
                method = moduleMethods
                   .SingleOrDefault(
                        m => m.Name.String.Equals(instrumentedMethod.MethodName, StringComparison.InvariantCultureIgnoreCase) &&
                             m.DeclaringType?.FullName.Equals(instrumentedMethod.TypeName, StringComparison.InvariantCultureIgnoreCase) == true &&
                             m.ReturnType.FullName.Equals(instrumentedMethod.ReturnType.GetTypeSig(module, ModuleRewriteContext).FullName, StringComparison.InvariantCultureIgnoreCase));
            }
            else
            {
                method = moduleMethods
                   .SingleOrDefault(
                        m => m.FullName.Equals(member.FullName, StringComparison.InvariantCultureIgnoreCase) ||
                             m.Name.String.Equals(member.MethodOrField, StringComparison.InvariantCultureIgnoreCase) &&
                             m.DeclaringType?.FullName.Equals(member.Type, StringComparison.InvariantCultureIgnoreCase) == true &&
                             m.ReturnType.FullName.Equals(member.ReturnTypeSig.GetTypeSig(module, ModuleRewriteContext).FullName, StringComparison.InvariantCultureIgnoreCase));
            }

            return method;
        }

        internal List<ModuleDefMD> AddInstrumentedAssemblyMetadata()
        {
            var modules = new List<ModuleDefMD>();
            foreach (var moduleDefMd in ModuleRewriteContext.OriginalsModulesOfInstrumentedMembers)
            {
                try
                {
                    Logger.Info($"Add instrumented assembly metadata to module: {moduleDefMd.Name}");
                    modules.Add(AddInstrumentedAssemblyMetadataToModule(moduleDefMd));
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to add instrumented assembly metadata to module: {moduleDefMd.Name}{Environment.NewLine}" +
                                                   $"{e}{Environment.NewLine}" +
                                                   "InstrumentedAssemblyGenerator will not be able to verify this module");
                }
            }

            return modules;
        }

        private ModuleDefMD AddInstrumentedAssemblyMetadataToModule(ModuleDefMD originalModule)
        {
            var metadataImporter = new ProfilerMetadataImporter(ModuleRewriteContext, originalModule, _args);
            metadataImporter.Import();
            metadataImporter.EnforceUsage();
            return metadataImporter.OriginalModule;
        }

        [Conditional("LogRewrittenMethodInstructions")]
        private void LogMethodInstructions(MethodDef method)
        {
            try
            {
                string typeFullName = method.DeclaringType?.FullName;
                typeFullName = typeFullName == null ? "" : typeFullName + ".";
                string name = typeFullName + method.Name + ".il";
                string filePath = Path.Combine(_args.InstrumentedMethodsFolder, new FileInfo(name).CleanFileName("%"));
                string instructions = string.Join(Environment.NewLine, method.Body.Instructions);
                File.WriteAllText(filePath, instructions, Encoding.Unicode);
            }
            catch (Exception e)
            {
                Logger.Warn(e);
            }
        }

        private void ModuleWriterEvent(object sender, ModuleWriterEventArgs e)
        {
            Logger.Verbose(e.Event.ToString());
        }
    }
}
