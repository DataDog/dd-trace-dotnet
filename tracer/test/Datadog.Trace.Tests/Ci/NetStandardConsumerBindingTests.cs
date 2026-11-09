// <copyright file="NetStandardConsumerBindingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

/// <summary>
/// Datadog.Trace.MSBuild and Datadog.Trace.Coverage.collector always run their netstandard2.0 build, which is compiled
/// against Datadog.Trace's netstandard2.0 build. At runtime they bind to whichever Datadog.Trace build the host loaded
/// (net461 on .NET Framework MSBuild/vstest, netcoreapp3.1 or net6.0 on .NET Core), so any reference that only exists in
/// the netstandard2.0 build (an #if'd member, a vendored polyfill type bound via InternalsVisibleTo, a different overload)
/// fails with MissingMethodException/TypeLoadException. These tests check every such reference against the Datadog.Trace
/// build loaded by the test. Each test TFM loads a different build, so running across all test TFMs covers every build,
/// including any target framework added to Datadog.Trace in future.
/// </summary>
public class NetStandardConsumerBindingTests
{
    private const string DatadogTrace = "Datadog.Trace";
    private const string CompileTimeTfm = "netstandard2.0";

    // Both consumers reference well over this many Datadog.Trace types and members. Finding fewer means the scan is broken.
    private const int MinimumExpectedReferences = 5;

    private static readonly string[] Consumers = ["Datadog.Trace.Coverage.collector", "Datadog.Trace.MSBuild"];

    public static IEnumerable<object[]> ConsumerNames() => Consumers.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(ConsumerNames))]
    public void ConsumerReferencesBindAgainstLoadedBuild(string consumer)
    {
        var tfm = EnvironmentTools.GetTracerTargetFrameworkDirectory();
        var result = Check(consumer, typeof(Tracer).Assembly.Location);

        result.TypeReferenceCount.Should().BeGreaterThan(MinimumExpectedReferences, $"{consumer} should reference Datadog.Trace types");
        result.MemberReferenceCount.Should().BeGreaterThan(MinimumExpectedReferences, $"{consumer} should reference Datadog.Trace members");
        string.Join(Environment.NewLine, result.Violations).Should().BeEmpty(
            $"{consumer} (compiled against Datadog.Trace {CompileTimeTfm}) must only reference APIs that also exist in Datadog.Trace {tfm}. "
          + "Avoid #if'd members/polyfills of Datadog.Trace in this assembly");
    }

    [Theory]
    [MemberData(nameof(ConsumerNames))]
    public void ConsumerReferencesBindAgainstCompileTimeBuild(string consumer)
    {
        // Positive control: the build the consumer was compiled against must always pass, otherwise the checker is wrong
        var result = Check(consumer, GetArtifactPath(DatadogTrace, CompileTimeTfm));

        result.TypeReferenceCount.Should().BeGreaterThan(MinimumExpectedReferences);
        result.MemberReferenceCount.Should().BeGreaterThan(MinimumExpectedReferences);
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void ReportsViolationsWhenReferencesDoNotBind()
    {
        // Negative control: an assembly that defines none of the referenced types must produce violations
        var result = Check(Consumers[0], GetArtifactPath(Consumers[1], CompileTimeTfm));

        result.Violations.Should().NotBeEmpty();
    }

    private static CheckResult Check(string consumer, string targetPath)
    {
        using var consumerAssembly = new LoadedAssembly(GetArtifactPath(consumer, CompileTimeTfm));
        using var compileTimeAssembly = new LoadedAssembly(GetArtifactPath(DatadogTrace, CompileTimeTfm));
        using var targetAssembly = new LoadedAssembly(targetPath);
        return BindingChecker.Check(consumerAssembly, compileTimeAssembly, targetAssembly);
    }

    private static string GetArtifactPath(string project, string tfm)
    {
        var path = Path.Combine(
            EnvironmentTools.GetSolutionDirectory(),
            "artifacts",
            "bin",
            project,
            $"{EnvironmentTools.GetBuildConfiguration().ToLowerInvariant()}_{tfm}",
            project + ".dll");

        File.Exists(path).Should().BeTrue($"{path} should exist. Build tracer/src first (e.g. the CompileManagedSrc Nuke target)");
        return path;
    }

    private static class BindingChecker
    {
        public static CheckResult Check(LoadedAssembly consumer, LoadedAssembly compileTime, LoadedAssembly target)
        {
            var reader = consumer.Reader;
            var violations = new List<string>();
            var typeReferenceCount = 0;
            var memberReferenceCount = 0;

            // 1. Type references (including vendored polyfills such as [Datadog.Trace]System.Span`1)
            foreach (var handle in reader.TypeReferences)
            {
                if (!Names.IsDatadogTraceScoped(reader, handle))
                {
                    continue;
                }

                var typeName = Names.Of(reader, handle);
                if (IsAttribute(compileTime, typeName))
                {
                    // Custom attributes are only resolved via reflection, so they can't fail to bind at runtime.
                    // Known gap: typeof(SomeDatadogAttribute) in IL would still fail, but that's very unlikely here.
                    continue;
                }

                typeReferenceCount++;
                if (!target.Types.TryGetValue(typeName, out var typeDef))
                {
                    violations.Add($"Type '{typeName}' does not exist");
                    continue;
                }

                if (!compileTime.Types.TryGetValue(typeName, out var compileTimeTypeDef))
                {
                    continue;
                }

                if (AccessLevel(target.Reader.GetTypeDefinition(typeDef).Attributes) < AccessLevel(compileTime.Reader.GetTypeDefinition(compileTimeTypeDef).Attributes))
                {
                    violations.Add($"Type '{typeName}' is less accessible");
                }

                var compileTimeKind = GetTypeKind(compileTime.Reader, compileTimeTypeDef);
                var targetKind = GetTypeKind(target.Reader, typeDef);
                if (compileTimeKind != targetKind)
                {
                    violations.Add($"Type '{typeName}' is a {targetKind}, not a {compileTimeKind}");
                }
            }

            // 2. Method and field references on Datadog.Trace types, matched on their full signature
            foreach (var handle in reader.MemberReferences)
            {
                var memberRef = reader.GetMemberReference(handle);
                if (GetDatadogTraceType(reader, memberRef.Parent) is not { } typeName || IsAttribute(compileTime, typeName))
                {
                    continue;
                }

                memberReferenceCount++;
                var name = reader.GetString(memberRef.Name);
                var signature = memberRef.GetKind() == MemberReferenceKind.Method
                                    ? Signatures.Of(memberRef.DecodeMethodSignature(SignatureProvider.Instance, null))
                                    : memberRef.DecodeFieldSignature(SignatureProvider.Instance, null);
                var description = $"'{typeName}::{name}' with signature '{signature}'";

                if (!target.Types.TryGetValue(typeName, out var typeDef))
                {
                    // Already reported as a missing type
                    continue;
                }

                var targetAccess = FindMember(target.Reader, typeDef, name, signature);
                if (targetAccess is null)
                {
                    violations.Add($"Member {description} does not exist");
                }
                else if (compileTime.Types.TryGetValue(typeName, out var compileTimeTypeDef)
                      && targetAccess < FindMember(compileTime.Reader, compileTimeTypeDef, name, signature))
                {
                    violations.Add($"Member {description} is less accessible");
                }
            }

            // 3. Types deriving from/implementing Datadog.Trace types must implement every abstract member of the runtime
            // build. The consumer only overrides what was abstract at compile time, so anything else fails to load.
            foreach (var handle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(handle);
                var implemented = typeDef.GetInterfaceImplementations()
                                         .Select(i => reader.GetInterfaceImplementation(i).Interface)
                                         .Append(typeDef.BaseType);

                foreach (var baseType in implemented)
                {
                    if (GetDatadogTraceType(reader, baseType) is not { } baseName
                     || !target.Types.TryGetValue(baseName, out var targetBase)
                     || !compileTime.Types.TryGetValue(baseName, out var compileTimeBase))
                    {
                        continue;
                    }

                    if ((target.Reader.GetTypeDefinition(targetBase).Attributes & TypeAttributes.Sealed) != 0
                     && (compileTime.Reader.GetTypeDefinition(compileTimeBase).Attributes & TypeAttributes.Sealed) == 0)
                    {
                        violations.Add($"Type '{Names.Of(reader, handle)}' derives from '{baseName}', which is sealed");
                    }

                    var compileTimeAbstract = GetUnimplementedAbstractMembers(compileTime.Reader, compileTimeBase);
                    foreach (var member in GetUnimplementedAbstractMembers(target.Reader, targetBase))
                    {
                        if (!compileTimeAbstract.Contains(member))
                        {
                            violations.Add($"Type '{Names.Of(reader, handle)}' does not implement abstract member '{member}' of '{baseName}'");
                        }
                    }
                }
            }

            return new CheckResult(typeReferenceCount, memberReferenceCount, violations);
        }

        /// <summary>
        /// Finds a method or field by name and signature on the type or its base types within the same assembly.
        /// </summary>
        /// <returns><c>null</c> if not found, otherwise its <see cref="AccessLevel(TypeAttributes)"/></returns>
        private static int? FindMember(MetadataReader reader, TypeDefinitionHandle typeHandle, string name, string signature)
        {
            for (TypeDefinitionHandle? current = typeHandle; current is { } handle; current = GetBaseTypeDefinition(reader, handle))
            {
                var typeDef = reader.GetTypeDefinition(handle);
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if (reader.StringComparer.Equals(method.Name, name)
                     && Signatures.Of(method.DecodeSignature(SignatureProvider.Instance, null)) == signature)
                    {
                        return (method.Attributes & MethodAttributes.MemberAccessMask) switch
                        {
                            MethodAttributes.Public or MethodAttributes.Assembly or MethodAttributes.FamORAssem => 2,
                            MethodAttributes.Family or MethodAttributes.FamANDAssem => 1,
                            _ => 0,
                        };
                    }
                }

                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if (reader.StringComparer.Equals(field.Name, name)
                     && field.DecodeSignature(SignatureProvider.Instance, null) == signature)
                    {
                        return (field.Attributes & FieldAttributes.FieldAccessMask) switch
                        {
                            FieldAttributes.Public or FieldAttributes.Assembly or FieldAttributes.FamORAssem => 2,
                            FieldAttributes.Family or FieldAttributes.FamANDAssem => 1,
                            _ => 0,
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// How accessible a type or member is to a friend (InternalsVisibleTo) assembly: 2 = from anywhere,
        /// 1 = only from derived types (protected), 0 = not at all (private)
        /// </summary>
        private static int AccessLevel(TypeAttributes attributes)
            => (attributes & TypeAttributes.VisibilityMask) switch
            {
                TypeAttributes.NotPublic or TypeAttributes.Public or TypeAttributes.NestedPublic
                    or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem => 2,
                TypeAttributes.NestedFamily or TypeAttributes.NestedFamANDAssem => 1,
                _ => 0,
            };

        /// <summary>
        /// The runtime rejects references that expect a different kind of type (e.g. valuetype vs class)
        /// </summary>
        private static string GetTypeKind(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            if ((typeDef.Attributes & TypeAttributes.Interface) != 0)
            {
                return "interface";
            }

            var baseType = typeDef.BaseType;
            var baseName = !baseType.IsNil && baseType.Kind == HandleKind.TypeReference ? Names.Of(reader, (TypeReferenceHandle)baseType) : null;
            return baseName switch
            {
                "System.ValueType" => "struct",
                "System.Enum" => "enum",
                _ => "class",
            };
        }

        /// <summary>
        /// Gets the base type if it is defined in the same assembly, including generic instantiations of such types
        /// </summary>
        private static TypeDefinitionHandle? GetBaseTypeDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var baseType = reader.GetTypeDefinition(handle).BaseType;
            if (baseType.IsNil)
            {
                return null;
            }

            baseType = GetGenericTypeOrSelf(reader, baseType);
            return baseType.Kind == HandleKind.TypeDefinition ? (TypeDefinitionHandle)baseType : null;
        }

        /// <summary>
        /// For a generic instantiation TypeSpec (e.g. Base&lt;int&gt;), gets the generic type (Base`1); otherwise returns the handle
        /// </summary>
        private static EntityHandle GetGenericTypeOrSelf(MetadataReader reader, EntityHandle handle)
        {
            if (handle.Kind != HandleKind.TypeSpecification)
            {
                return handle;
            }

            var blob = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
            if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
            {
                return handle;
            }

            blob.ReadSignatureTypeCode(); // CLASS or VALUETYPE
            return blob.ReadTypeHandle();
        }

        /// <summary>
        /// Gets the abstract methods (as "name signature") that a type deriving from the given type must implement:
        /// abstract methods on the type or its base types within the same assembly that no more-derived type overrides.
        /// </summary>
        private static HashSet<string> GetUnimplementedAbstractMembers(MetadataReader reader, TypeDefinitionHandle typeHandle)
        {
            var implemented = new HashSet<string>(StringComparer.Ordinal);
            var unimplemented = new HashSet<string>(StringComparer.Ordinal);
            for (TypeDefinitionHandle? current = typeHandle; current is { } handle; current = GetBaseTypeDefinition(reader, handle))
            {
                var typeDef = reader.GetTypeDefinition(handle);
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    var member = $"{reader.GetString(method.Name)} {Signatures.Of(method.DecodeSignature(SignatureProvider.Instance, null))}";
                    if ((method.Attributes & MethodAttributes.Abstract) == 0)
                    {
                        implemented.Add(member);
                    }
                    else if (!implemented.Contains(member))
                    {
                        unimplemented.Add(member);
                    }
                }
            }

            return unimplemented;
        }

        private static bool IsAttribute(LoadedAssembly assembly, string typeName)
        {
            var reader = assembly.Reader;
            if (!assembly.Types.TryGetValue(typeName, out var handle))
            {
                return false;
            }

            while (true)
            {
                var baseType = reader.GetTypeDefinition(handle).BaseType;
                if (baseType.IsNil)
                {
                    return false;
                }

                switch (baseType.Kind)
                {
                    case HandleKind.TypeDefinition:
                        handle = (TypeDefinitionHandle)baseType;
                        continue;
                    case HandleKind.TypeReference:
                        return Names.Of(reader, (TypeReferenceHandle)baseType) == "System.Attribute";
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets the name of the Datadog.Trace type referenced by a TypeRef, or by a generic instantiation TypeSpec
        /// </summary>
        private static string? GetDatadogTraceType(MetadataReader reader, EntityHandle handle)
        {
            handle = GetGenericTypeOrSelf(reader, handle);
            return handle.Kind == HandleKind.TypeReference && Names.IsDatadogTraceScoped(reader, (TypeReferenceHandle)handle)
                       ? Names.Of(reader, (TypeReferenceHandle)handle)
                       : null;
        }
    }

    private static class Names
    {
        public static string Of(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            var declaringType = typeDef.GetDeclaringType();
            return declaringType.IsNil
                       ? Qualify(reader, typeDef.Namespace, typeDef.Name)
                       : Of(reader, declaringType) + "/" + reader.GetString(typeDef.Name);
        }

        public static string Of(MetadataReader reader, TypeReferenceHandle handle)
        {
            var typeRef = reader.GetTypeReference(handle);
            return typeRef.ResolutionScope.Kind == HandleKind.TypeReference
                       ? Of(reader, (TypeReferenceHandle)typeRef.ResolutionScope) + "/" + reader.GetString(typeRef.Name)
                       : Qualify(reader, typeRef.Namespace, typeRef.Name);
        }

        public static bool IsDatadogTrace(MetadataReader reader)
            => reader.IsAssembly && reader.StringComparer.Equals(reader.GetAssemblyDefinition().Name, DatadogTrace);

        public static bool IsDatadogTraceScoped(MetadataReader reader, TypeReferenceHandle handle)
        {
            while (true)
            {
                var scope = reader.GetTypeReference(handle).ResolutionScope;
                switch (scope.Kind)
                {
                    case HandleKind.TypeReference: // nested type
                        handle = (TypeReferenceHandle)scope;
                        continue;
                    case HandleKind.AssemblyReference:
                        return reader.StringComparer.Equals(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name, DatadogTrace);
                    default:
                        return false;
                }
            }
        }

        private static string Qualify(MetadataReader reader, StringHandle ns, StringHandle name)
            => ns.IsNil || reader.GetString(ns).Length == 0
                   ? reader.GetString(name)
                   : reader.GetString(ns) + "." + reader.GetString(name);
    }

    private static class Signatures
    {
        public static string Of(MethodSignature<string> signature)
            => $"{(signature.Header.IsInstance ? "instance " : "static ")}{signature.ReturnType} `{signature.GenericParameterCount}({string.Join(", ", signature.ParameterTypes)})";
    }

    private sealed record CheckResult(int TypeReferenceCount, int MemberReferenceCount, List<string> Violations);

    private sealed class LoadedAssembly : IDisposable
    {
        private readonly PEReader _peReader;

        public LoadedAssembly(string path)
        {
            _peReader = new PEReader(File.OpenRead(path));
            Reader = _peReader.GetMetadataReader();
            foreach (var handle in Reader.TypeDefinitions)
            {
                Types[Names.Of(Reader, handle)] = handle;
            }
        }

        public MetadataReader Reader { get; }

        public Dictionary<string, TypeDefinitionHandle> Types { get; } = new(StringComparer.Ordinal);

        public void Dispose() => _peReader.Dispose();
    }

    /// <summary>
    /// Renders types as strings that ignore which non-Datadog assembly a type comes from, so the same type compares equal
    /// whether it was referenced via System.Runtime, System.Private.CoreLib or netstandard. Types owned by Datadog.Trace
    /// get a "[DD]" prefix, because e.g. a vendored System.Span`1 polyfill is a different type from the BCL one.
    /// </summary>
    private sealed class SignatureProvider : ISignatureTypeProvider<string, object?>
    {
        public const string DatadogTracePrefix = "[DD]";

        public static readonly SignatureProvider Instance = new();

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            => Names.IsDatadogTrace(reader) ? DatadogTracePrefix + Names.Of(reader, handle) : Names.Of(reader, handle);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            => Names.IsDatadogTraceScoped(reader, handle) ? DatadogTracePrefix + Names.Of(reader, handle) : Names.Of(reader, handle);

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";

        public string GetByReferenceType(string elementType) => elementType + "&";

        public string GetPointerType(string elementType) => elementType + "*";

        public string GetPinnedType(string elementType) => elementType + " pinned";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => $"{genericType}<{string.Join(", ", typeArguments)}>";

        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
            => $"{unmodifiedType} {(isRequired ? "modreq" : "modopt")}({modifier})";

        public string GetFunctionPointerType(MethodSignature<string> signature) => $"method {Signatures.Of(signature)}";
    }
}
