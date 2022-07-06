using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
#pragma warning disable CS0618

namespace Datadog.InstrumentedAssemblyVerification
{
    internal class PrepareMethodVerification : IVerification
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public |
                                           BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly InstrumentationVerificationLogger _logger;
        private readonly string _assemblyLocation;
        // List of exception types that we want to notify about, that rest will ignore
        private readonly List<Type> _exceptionsToConsider;
        private readonly List<string> _referencesLocations;
        private readonly List<string> _gacPaths;
        private bool _isNetCore;
        private readonly List<string> _errors;
        private readonly List<string> _methods;

        /// <summary>
        /// Run <see cref="RuntimeHelpers.PrepareMethod(System.RuntimeMethodHandle)"/> on each method in the assembly, thus forcing the .NET JIT to compile the method
        /// </summary>
        /// <param name="assemblyLocation">The assembly that you want to verify</param>
        /// <param name="methods">methods that should b verified</param>
        /// <param name="logger">The logger</param>
        public PrepareMethodVerification(string assemblyLocation, List<string> methods, InstrumentationVerificationLogger logger)
        {
            _assemblyLocation = assemblyLocation;
            _methods = methods;
            _logger = logger;
            _errors = new List<string>();
            _exceptionsToConsider = new List<Type>()
            {
                typeof(InvalidProgramException),
                typeof(BadImageFormatException),
                typeof(ExecutionEngineException)
            };

            _referencesLocations = new List<string>
            {
                Directory.GetParent(assemblyLocation).FullName,
                $@"{AssemblyUtils.GetProgramFilesPath()}\Reference Assemblies\Microsoft\Framework\.NETFramework\",
                $@"{AssemblyUtils.GetProgramFilesPath()}\Reference Assemblies\Microsoft\Framework\v3.0\",
                $@"{AssemblyUtils.GetProgramFilesPath()}\Reference Assemblies\Microsoft\Framework\v3.5\",
                $@"{AssemblyUtils.GetProgramFilesPath(preferX64: true)}\dotnet\shared\Microsoft.NETCore.App\2.0.0\",
                $@"{AssemblyUtils.GetProgramFilesPath()}\Reference Assemblies\Microsoft\Framework\",
                $@"{AssemblyUtils.GetProgramFilesPath(preferX64: true)}\dotnet\shared\Microsoft.NETCore.App\",
                Directory.GetParent(_assemblyLocation)?.FullName
            };
            _gacPaths = AssemblyUtils.GetDefaultWindowsGacPaths().ToList();
        }

        //[System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions()]
        //TODO: When PrepareMethod throws a CorruptedStateException like ExecutionEngineException
        // We can't catch it even with this attribute because netcore ignore it (legacyCorruptedStateExceptionsPolicy also don't work).
        // In order to catch it, we need to either force the verification tool to be target net461 (which means it won't run on Linux),
        // or put this logic in an external process :/
        public List<string> Verify()
        {
            Assembly asm = null;
            try
            {
                _logger.Info($"{nameof(PrepareMethodVerification)} Verifying {_assemblyLocation} starts");
                _logger.Debug($"{nameof(PrepareMethodVerification)} Note that {nameof(PrepareMethodVerification)} wouldn't work in the case of R2R module");

                asm = AssemblyUtils.LoadAssemblyFromAndLogFailure(_assemblyLocation, _logger);
                _isNetCore = AssemblyUtils.IsNetFramework(asm);
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;


                foreach (string method in _methods)
                {
                    try
                    {
                        int methodNameIndex = method.Substring(0, method.LastIndexOf('(')).LastIndexOf(".", StringComparison.InvariantCulture);
                        string typeName = method.Substring(0, methodNameIndex);
                        var existingType = asm.GetType(typeName);
                        if (existingType == null)
                        {
                            _logger.Warn($"{nameof(PrepareMethodVerification)} Type {typeName} not found in assembly {asm.FullName}");
                            continue;
                        }

                        Type copyType;
                        List<Type> genericTypeArguments = new List<Type>();

                        if (existingType.IsGenericTypeDefinition) // open generic type
                        {
                            copyType = MakeGenericType(existingType, genericTypeArguments);
                        }
                        else
                        {
                            copyType = existingType;
                        }

                        string methodName = method.Substring(methodNameIndex + 1);
                        var allMethods = copyType.GetMethods(Flags).Cast<MethodBase>().Union(copyType.GetConstructors(Flags)).ToList();
                        var foundedMb = allMethods.SingleOrDefault(m => m.Name + $"({string.Join(",", m.GetParameters().Select(p => p.ParameterType.FullName))})" == methodName);
                        IEnumerable<MethodBase> foundedMbs;
                        if (foundedMb == null)
                        {
                            string errorMessage = $"{nameof(PrepareMethodVerification)} Method {methodName} not found in type {copyType.FullName}";
                            _logger.Debug(errorMessage);
                            foundedMbs = allMethods.Where(m => methodName.StartsWith(m.Name));
                        }
                        else
                        {
                            foundedMbs = new[] { foundedMb };
                        }

                        foreach (MethodBase mb in foundedMbs)
                        {
                            try
                            {
                                if (mb.IsAbstract || mb.MethodImplementationFlags != MethodImplAttributes.IL)
                                {
                                    continue;
                                }

                                if (mb.DeclaringType?.IsGenericTypeDefinition == true)
                                {
                                    // Don't know exactly why, but declaring type of .ctor of compiler generating type
                                    // is generic type definition even after I construct him, so I skip for know
                                    continue;
                                }

                                var genericMethodArguments = new List<Type>();
                                MethodBase copyMethod;
                                if (mb.IsGenericMethodDefinition)
                                {
                                    copyMethod = MakeGenericMethod(mb, genericMethodArguments);
                                }
                                else
                                {
                                    copyMethod = mb;
                                }

                                var instantiation = genericTypeArguments.Concat(genericMethodArguments).ToArray();

                                if (instantiation.Length == 0)
                                {
                                    RuntimeHelpers.PrepareMethod(copyMethod.MethodHandle);
                                }
                                else
                                {
                                    RuntimeHelpers.PrepareMethod(copyMethod.MethodHandle, instantiation.Select(inst => inst.TypeHandle).ToArray());
                                }
                            }
                            catch (InvalidProgramException ex)
                            {
                                // Common Language Runtime detected an invalid program
                                // or JIT Compiler encountered an internal limitation
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "Invalid IL"));

                                // Return after first InvalidProgramException. If we keep going we might crash with an AccessViolationException
                                break;
                            }
                            catch (BadImageFormatException ex)
                            {
                                // Metadata issues
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "Bad image format"));
                            }
                            catch (ExecutionEngineException ex)
                            {
                                // An unspecified fatal error in the runtime (obsolete but still in use by the runtime)
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "Unspecified runtime error"));
                            }
                            catch (FileNotFoundException ex)
                            {
                                AddErrorIfNotNullOrEmpty(
                                    HandleException(
                                        ex,
                                        mb.DeclaringType.FullName,
                                        mb.Name,
                                        "Assembly resolution failed. May occur if we try to prepare a method that is dependent on a module that was not loaded at runtime"));
                            }
                            catch (TypeLoadException ex)
                            {
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "Type resolution failed"));
                            }
                            catch (System.Security.VerificationException ex)
                            {
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "Can happen when type argument violates T constraint"));
                            }
                            catch (Exception ex) when (ex.InnerException is System.Security.VerificationException)
                            {
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "Can happen when type argument violates T constraint"));
                            }
                            catch (Exception ex)
                            {
                                AddErrorIfNotNullOrEmpty(HandleException(ex, mb.DeclaringType.FullName, mb.Name, "General", true));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AddErrorIfNotNullOrEmpty(HandleException(e, "Unknown type", "Unknown method", "General error", true));
                    }
                }

                if (_errors.Count == 0)
                {
                    _logger.Info($"{nameof(PrepareMethodVerification)} Verifying {_assemblyLocation} ended without errors");
                }
            }
            catch (Exception e)
            {
                _logger.Error($"{nameof(PrepareMethodVerification)} Verifying {_assemblyLocation} ended with errors: {e.ToString()}");
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
            return _errors;
        }

        private static MethodBase MakeGenericMethod(MethodBase foundedMb, List<Type> genericMethodArguments)
        {
            foreach (var argument in foundedMb.GetGenericArguments())
            {
                var constraints = argument.GetGenericParameterConstraints().ToList();
                if (constraints.Any())
                {
                    genericMethodArguments.Add(constraints.First());
                }
                else
                {
                    genericMethodArguments.Add(typeof(object));
                }
            }

            return ((MethodInfo) foundedMb).MakeGenericMethod(genericMethodArguments.ToArray());
        }

        private Type MakeGenericType(Type existingType, List<Type> genericTypeArguments)
        {
            try
            {
                foreach (var argument in existingType.GetGenericArguments())
                {
                    var constraints = argument.GetGenericParameterConstraints().ToList();
                    if (constraints.Any())
                    {
                        var first = constraints.First();
                        if (first == typeof(ValueType))
                        {
                            genericTypeArguments.Add(typeof(int));
                        }
                        else
                        {
                            genericTypeArguments.Add(constraints.First());
                        }
                    }
                    else
                    {
                        genericTypeArguments.Add(typeof(object));
                    }
                }

                return existingType.MakeGenericType(genericTypeArguments.ToArray());
            }
            catch (TypeLoadException ex)
            {
                AddErrorIfNotNullOrEmpty(HandleException(ex, existingType.FullName, "", "Type argument violates T constraint"));
            }
            catch (ArgumentException ex)
            {
                AddErrorIfNotNullOrEmpty(HandleException(ex, existingType.FullName, "", "Type argument violates T constraint"));
            }
            catch (Exception ex)
            {
                AddErrorIfNotNullOrEmpty(HandleException(ex, existingType.FullName, "", "General", true));
            }

            return null;
        }

        private void AddErrorIfNotNullOrEmpty(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _errors.Add(error);
            }
        }

        private string HandleException(Exception ex, string typeName, string methodName, string errorCategory, bool printAnyway = false)
        {
            if (_exceptionsToConsider.Contains(ex.GetType()))
            {
                string error = $"{nameof(PrepareMethodVerification)} {errorCategory} {typeName}::{methodName} - {ex.GetType().Name}: {ex.Message}";
                _logger.Error(error);
                return error;
            }

            if (printAnyway)
            {
                _logger.Error($"{nameof(PrepareMethodVerification)} {errorCategory} {typeName}::{methodName} - {ex.GetType().Name}: {ex.Message}");
            }
            return string.Empty;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            foreach (string location in _referencesLocations)
            {
                string assemblyToLoad = Path.Combine(location, assemblyName.Name + ".dll");
                try
                {
                    if (File.Exists(assemblyToLoad))
                    {
                        return Assembly.LoadFile(assemblyToLoad);
                    }

                    assemblyToLoad = Path.Combine(location, assemblyName.Name + ".exe");
                    if (File.Exists(assemblyToLoad))
                    {
                        return Assembly.LoadFile(assemblyToLoad);
                    }
                }
                catch (BadImageFormatException)
                {
                    try
                    {
                        return Assembly.ReflectionOnlyLoadFrom(assemblyToLoad);
                    }
                    catch (PlatformNotSupportedException)
                    {
                    }
                }
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == assemblyName.Name)
                {
                    return asm;
                }
            }

            var gacAssembly = AssemblyUtils.GetAssemblyFromGac(_gacPaths, assemblyName.Name);
            if (gacAssembly != null)
            {
                return gacAssembly;
            }

            var nugetAssembly = AssemblyUtils.GetAssemblyFromNugetFolder(assemblyName.Name, assemblyName.Version, _isNetCore);
            if (nugetAssembly != null)
            {
                return nugetAssembly;
            }

            var any = LoadAny(assemblyName);

            if (any == null)
            {
                _logger.Error($"{nameof(PrepareMethodVerification)} Failed to resolve {args.Name}");
            }

            return any;
        }

        private Assembly LoadAny(AssemblyName assemblyName)
        {
            var allPaths = new List<string>();
            foreach (string location in _referencesLocations.Where(Directory.Exists))
            {
                foreach (string file in Directory.EnumerateFiles(location, "*.dll", SearchOption.AllDirectories))
                {
                    if (Path.GetFileNameWithoutExtension(file) == assemblyName.Name)
                    {
                        allPaths.Add(file);
                    }
                }
            }

            switch (allPaths.Count)
            {
                case 0:
                    return null;
                case 1:
                    return Assembly.LoadFile(allPaths[0]);
                default:
                    return Assembly.LoadFile(allPaths.Max());
            }
        }
    }
}
