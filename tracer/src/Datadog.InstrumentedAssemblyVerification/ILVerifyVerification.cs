using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using ILVerify;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Datadog.InstrumentedAssemblyVerification
{
    internal class ILVerifyVerification : IVerification
    {
        // These are errors that we don't care much about,
        // or they already existed in the original assembly.
        private readonly List<int> _errorsToExclude = new()
        {
            (int) VerifierError.StackByRef,
            (int) VerifierError.StackUnexpected,
            (int) VerifierError.InitLocals,
            (int) VerifierError.ThrowOrCatchOnlyExceptionType,
            (int) ExceptionStringID.MissingMethod
        };

        private const string CoreLibAssemblyNetCore = "System.Private.CoreLib.dll";
        private const string CoreLibAssemblyNetFramework = "mscorlib.dll";
        private readonly string _assemblyLocation;
        private List<string> _methods;
        private readonly Verifier _verifier;
        private readonly InstrumentationVerificationLogger _logger;

        public ILVerifyVerification(string assemblyLocation, List<string> methods, InstrumentationVerificationLogger logger)
        {
            _logger = logger;
            _assemblyLocation = assemblyLocation;
            _methods = methods;

            _verifier =
                new Verifier(
                    new Resolver(_assemblyLocation),
                    new VerifierOptions { SanityChecks = true, IncludeMetadataTokensInErrorMessages = true });
        }

        public List<string> Verify()
        {
            _logger.Info($"{nameof(ILVerifyVerification)} Verifying {_assemblyLocation} starts");

            var peReader = new PEReader(File.OpenRead(_assemblyLocation));
            var reader = peReader.GetMetadataReader();

            var mscorlibReferenced = reader.AssemblyReferences.Any(ar => reader.GetString(reader.GetAssemblyReference(ar).Name) == "mscorlib");
            _verifier.SetSystemModuleName(new AssemblyName(mscorlibReferenced ? CoreLibAssemblyNetFramework : CoreLibAssemblyNetCore));

            // "GetModule" method is internal, but we need to work around it with Reflection.
            // Not sure why it's like that, as it is needed by anyone trying to consume ILVerify externally :/ 
            var module = (EcmaModule)
                _verifier.GetType()
                         .GetMethod("GetModule", BindingFlags.NonPublic | BindingFlags.Instance)?
                         .Invoke(_verifier, new object[] { peReader });

            if (module == null)
            {
                _logger.Error("Error(s) Verifying " + _assemblyLocation);
                throw new Exception("Failed to get EcmaModule from ILVerifier");
            }

            var errors = VerifyAssembly(peReader, module, _assemblyLocation);

            return errors;
        }

        private List<string> VerifyAssembly(PEReader peReader, EcmaModule module, string path)
        {
            var methodsErrors = VerifyMethods(peReader, module, path, out int verifiedMethodCounter);
            var typesErrors = VerifyTypes(peReader, out int verifiedTypeCounter);
            var errors = methodsErrors.Concat(typesErrors).ToList();
            if (errors.Count > 0)
            {
                _logger.Error($"{nameof(ILVerifyVerification)} Verifying {path} ended with errors");
            }
            else
            {
                _logger.Info($"{nameof(ILVerifyVerification)} Verifying {path} ended without errors");
            }

            _logger.Debug("--- Verification Stats: ---");
            _logger.Debug($"Types found and verified: {verifiedTypeCounter}");
            _logger.Debug($"Methods found and verified: {verifiedMethodCounter}");

            return errors;
        }

        private List<string> VerifyMethods(PEReader peReader, EcmaModule module, string path, out int verifiedMethodCounter)
        {
            var errors = new List<string>();
            verifiedMethodCounter = 0;
            string moduleName = Path.GetFileName(path);
            MetadataReader metadataReader = peReader.GetMetadataReader();
            var methodsWithoutParameters = _methods.Select(m => m.Substring(0, m.IndexOf('('))).ToList();
            foreach (var methodHandle in metadataReader.MethodDefinitions)
            {
                string methodName = null;
                try
                {
                    methodName = GetQualifiedMethodName(metadataReader, methodHandle);
                    // verify only instrumented methods (and all their overloads). 
                    if (!methodsWithoutParameters.Contains(methodName))
                    {
                        continue;
                    }

                    var results = _verifier.Verify(peReader, methodHandle);
                    foreach (var result in results)
                    {
                        if (!ShouldIgnoreVerificationResult(result))
                        {
                            string error = PrintVerifyMethodsResult(result, module, moduleName);
                            errors.Add(error);
                        }
                    }

                    verifiedMethodCounter++;
                }
                catch (AssemblyResolutionException)
                {
                    // Ignore this - it is expected and completely legitimate that some references cannot be resolved,
                    // as there may have been many assemblies which were not loaded at the time we captured the
                    // information.
                }
                catch (Exception e)
                {
                    _logger.Error($"{nameof(ILVerifyVerification)} Could not verify {methodName}{Environment.NewLine}{e.ToString()}");
                }
            }

            return errors;
        }

        private string PrintVerifyMethodsResult(VerificationResult result, EcmaModule module, string moduleName)
        {
            var sb = new StringBuilder();
            sb.Append($"{nameof(ILVerifyVerification)} [IL]: [");
            if (result.Code != VerifierError.None)
            {
                sb.Append(result.Code);
            }
            else
            {
                sb.Append(result.ExceptionID);
            }

            sb.Append("]: ");

            sb.Append("[");
            sb.Append(moduleName);
            sb.Append(" : ");

            MetadataReader metadataReader = module.MetadataReader;

            TypeDefinition typeDef = metadataReader.GetTypeDefinition(metadataReader.GetMethodDefinition(result.Method).GetDeclaringType());
            string typeNamespace = metadataReader.GetString(typeDef.Namespace);
            sb.Append(typeNamespace);
            sb.Append(".");
            string typeName = metadataReader.GetString(typeDef.Name);
            sb.Append(typeName);
            sb.Append("::");
            var method = (EcmaMethod) module.GetMethod(result.Method);
            sb.Append(PrintMethod(method));
            sb.Append("]");

            if (result.Code != VerifierError.None)
            {
                if (result.TryGetArgumentValue("Found", out string found))
                {
                    sb.Append("[found ");
                    sb.Append(found);
                    sb.Append("]");
                }

                if (result.TryGetArgumentValue("Expected", out string expected))
                {
                    sb.Append("[expected ");
                    sb.Append(expected);
                    sb.Append("]");
                }

                if (result.TryGetArgumentValue("Token", out int token))
                {
                    sb.Append("[token  0x");
                    sb.Append(token.ToString("X8"));
                    sb.Append("]");
                }
            }

            sb.Append(" ");
            sb.AppendLine(result.Args == null ?
                              result.Message :
                              String.Format(result.Message, result.Args));

            string error = sb.ToString();

            // We don't want to add offsets to 'errors' because it ill be different between original and instrumented assemblies, so we only log it to help with debugging
            _logger.Error(error + $"at [offset 0x{result.GetArgumentValue<int>("Offset"):X8}]");

            return error;
        }

        private static string PrintMethod(EcmaMethod method)
        {
            var sb = new StringBuilder();

            sb.Append(method.Name);
            sb.Append("(");
            try
            {
                if (method.Signature.Length > 0)
                {
                    bool first = true;
                    foreach (var parameter in method.Signature)
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            sb.Append(", ");
                        }

                        sb.Append(parameter.ToString());
                    }
                }
            }
            catch
            {
                sb.Append("Error while getting method signature");
            }

            sb.Append(")");
            return sb.ToString();
        }

        private List<string> VerifyTypes(PEReader peReader, out int verifiedTypeCounter)
        {
            var errors = new List<string>();
            verifiedTypeCounter = 0;
            MetadataReader metadataReader = peReader.GetMetadataReader();

            foreach (TypeDefinitionHandle typeHandle in metadataReader.TypeDefinitions)
            {
                // get fully qualified type name
#if DEBUG
                string className = GetQualifiedClassName(metadataReader, typeHandle);
#endif
                var results = _verifier.Verify(peReader, typeHandle);
                foreach (VerificationResult result in results)
                {
                    if (!ShouldIgnoreVerificationResult(result))
                    {
                        string error = $"{result.Message}: {result.Args}";
                        _logger.Error(error);
                        errors.Add(error);
                    }
                }

                verifiedTypeCounter++;
            }

            return errors;
        }

        /// <summary>
        /// This method returns the fully qualified class name.
        /// </summary>
        private string GetQualifiedClassName(MetadataReader metadataReader, TypeDefinitionHandle typeHandle)
        {
            var typeDef = metadataReader.GetTypeDefinition(typeHandle);
            string typeName = metadataReader.GetString(typeDef.Name);

            string namespaceName = metadataReader.GetString(typeDef.Namespace);
            string assemblyName = metadataReader.GetString(metadataReader.IsAssembly ? metadataReader.GetAssemblyDefinition().Name : metadataReader.GetModuleDefinition().Name);

            var builder = new StringBuilder();
            builder.Append($"[{assemblyName}]");
            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.Append($"{namespaceName}.");
            }

            builder.Append($"{typeName}");

            return builder.ToString();
        }

        /// <summary>
        /// This method returns the fully qualified method name by concatenating assembly, type and method name.
        /// This method exists to avoid additional assembly resolving, which might be triggered by calling
        /// MethodDesc.ToString().
        /// </summary>
        private string GetQualifiedMethodName(MetadataReader metadataReader, MethodDefinitionHandle methodHandle)
        {
            var methodDef = metadataReader.GetMethodDefinition(methodHandle);
            var typeDef = metadataReader.GetTypeDefinition(methodDef.GetDeclaringType());

            string methodName = metadataReader.GetString(metadataReader.GetMethodDefinition(methodHandle).Name);
            string typeName = metadataReader.GetString(typeDef.Name);
            string namespaceName = metadataReader.GetString(typeDef.Namespace);
            // string assemblyName = metadataReader.GetString(metadataReader.IsAssembly ? metadataReader.GetAssemblyDefinition().Name : metadataReader.GetModuleDefinition().Name);

            var builder = new StringBuilder();
            // builder.Append($"[{assemblyName}]");
            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.Append($"{namespaceName}.");
            }

            builder.Append($"{typeName}.{methodName}");

            return builder.ToString();
        }

        private bool ShouldIgnoreVerificationResult(VerificationResult result)
        {
            return _errorsToExclude.Contains((int) result.Code) ||
                   result.ExceptionID != null && _errorsToExclude.Contains((int) result.ExceptionID);
        }

        private class Resolver : IResolver
        {
            private readonly string _directory;

            public Resolver(string assemblyLocation)
            {
                _directory = Path.GetDirectoryName(assemblyLocation);
            }

            public PEReader Resolve(string simpleName)
            {
                // TODO: we can use "Runtime package store" to resolve modules that doesn't exist in original modules folder
                // https://docs.microsoft.com/en-us/dotnet/core/deploying/runtime-store
                // /usr/local/share/dotnet/store on macOS/Linux and C:/Program Files/dotnet/store on Windows
                string firstCandidate = Path.Combine(_directory, simpleName);
                string[] candidates = new string[] {
                    firstCandidate,
                    firstCandidate + ".dll",
                    firstCandidate + ".exe",
                    firstCandidate + ".so",
                };
                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return new PEReader(File.OpenRead(candidate));
                    }
                }

                throw new AssemblyResolutionException("Could not resolve assembly " + simpleName);
            }
        }
    }

    internal class AssemblyResolutionException : Exception
    {
        public AssemblyResolutionException(string message) : base(message)
        {
        }
    }
}
