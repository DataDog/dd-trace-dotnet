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

            // 0. Internal members are only accessible if the target build grants InternalsVisibleTo to the consumer
            var consumerName = reader.GetString(reader.GetAssemblyDefinition().Name);
            var targetFriends = GetInternalsVisibleTo(target.Reader);
            foreach (var friend in GetInternalsVisibleTo(compileTime.Reader))
            {
                if (friend.Split(',')[0].Trim() == consumerName && !targetFriends.Contains(friend))
                {
                    violations.Add($"InternalsVisibleTo(\"{friend}\") is missing");
                }
            }

            // 1. Type references (including vendored polyfills such as [Datadog.Trace]System.Span`1)
            foreach (var handle in reader.TypeReferences)
            {
                if (!Names.IsDatadogTraceScoped(reader, handle))
                {
                    continue;
                }

                // Attribute types are checked too: they can also be used in IL (e.g. typeof(AvoidCoverageAttribute))
                var typeName = Names.Of(reader, handle);
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
                    violations.Add($"Type '{typeName}' changed from {compileTimeKind} to {targetKind}");
                }

                var compileTimeConstraints = GetConstraints(compileTime.Reader, compileTime.Reader.GetTypeDefinition(compileTimeTypeDef).GetGenericParameters());
                if (!compileTimeConstraints.IsSupersetOf(GetConstraints(target.Reader, target.Reader.GetTypeDefinition(typeDef).GetGenericParameters())))
                {
                    violations.Add($"Type '{typeName}' has additional generic constraints");
                }
            }

            // 2. Method and field references on Datadog.Trace types, matched on their full signature
            foreach (var handle in reader.MemberReferences)
            {
                var memberRef = reader.GetMemberReference(handle);
                if (GetDatadogTraceType(reader, memberRef.Parent) is not { } typeName)
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

                if (FindMember(target.Reader, typeDef, name, signature) is not { } targetMember)
                {
                    violations.Add($"Member {description} does not exist");
                    continue;
                }

                if (!compileTime.Types.TryGetValue(typeName, out var compileTimeTypeDef)
                 || FindMember(compileTime.Reader, compileTimeTypeDef, name, signature) is not { } compileTimeMember)
                {
                    continue;
                }

                if (targetMember.Access < compileTimeMember.Access)
                {
                    violations.Add($"Member {description} is less accessible");
                }

                if (targetMember.IsStatic != compileTimeMember.IsStatic)
                {
                    // Field signatures don't encode static-ness, but ldfld/ldsfld can't bind to the other kind
                    violations.Add($"Member {description} is {(targetMember.IsStatic ? "static" : "an instance member")}");
                }

                if (!compileTimeMember.Constraints.IsSupersetOf(targetMember.Constraints))
                {
                    // Type arguments that satisfied the compile-time constraints may not satisfy the extra ones
                    violations.Add($"Member {description} has additional generic constraints");
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

                // 4. Overrides of Datadog.Trace base methods: if the base method is no longer virtual, or is sealed, the
                // override either fails to load or silently stops being called
                if (GetDatadogTraceType(reader, typeDef.BaseType) is not { } baseClassName
                 || !target.Types.TryGetValue(baseClassName, out var targetBaseClass)
                 || !compileTime.Types.TryGetValue(baseClassName, out var compileTimeBaseClass))
                {
                    continue;
                }

                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if ((method.Attributes & MethodAttributes.Virtual) == 0 || (method.Attributes & MethodAttributes.NewSlot) != 0)
                    {
                        continue;
                    }

                    var name = reader.GetString(method.Name);
                    var signature = Signatures.Of(method.DecodeSignature(SignatureProvider.Instance, null));
                    if (FindMember(compileTime.Reader, compileTimeBaseClass, name, signature) is { IsOverridable: true }
                     && FindMember(target.Reader, targetBaseClass, name, signature) is not { IsOverridable: true })
                    {
                        violations.Add($"Type '{Names.Of(reader, handle)}' overrides '{baseClassName}::{name}' with signature '{signature}', which can no longer be overridden");
                    }
                }
            }

            return new CheckResult(typeReferenceCount, memberReferenceCount, violations);
        }

        /// <summary>
        /// Finds a method or field by name and signature on the type or its base types within the same assembly.
        /// Constructors aren't inherited, so they're only looked up on the type itself.
        /// </summary>
        /// <returns><c>null</c> if not found</returns>
        private static MemberMatch? FindMember(MetadataReader reader, TypeDefinitionHandle typeHandle, string name, string signature)
        {
            var isConstructor = name is ".ctor" or ".cctor";
            for (TypeDefinitionHandle? current = typeHandle; current is { } handle; current = isConstructor ? null : GetBaseTypeDefinition(reader, handle))
            {
                var typeDef = reader.GetTypeDefinition(handle);
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if (reader.StringComparer.Equals(method.Name, name)
                     && Signatures.Of(method.DecodeSignature(SignatureProvider.Instance, null)) == signature)
                    {
                        var access = (method.Attributes & MethodAttributes.MemberAccessMask) switch
                        {
                            MethodAttributes.Public or MethodAttributes.Assembly or MethodAttributes.FamORAssem => 2,
                            MethodAttributes.Family or MethodAttributes.FamANDAssem => 1,
                            _ => 0,
                        };
                        var isOverridable = (method.Attributes & MethodAttributes.Virtual) != 0 && (method.Attributes & MethodAttributes.Final) == 0;
                        return new MemberMatch(access, (method.Attributes & MethodAttributes.Static) != 0, isOverridable, GetConstraints(reader, method.GetGenericParameters()));
                    }
                }

                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if (reader.StringComparer.Equals(field.Name, name)
                     && field.DecodeSignature(SignatureProvider.Instance, null) == signature)
                    {
                        var access = (field.Attributes & FieldAttributes.FieldAccessMask) switch
                        {
                            FieldAttributes.Public or FieldAttributes.Assembly or FieldAttributes.FamORAssem => 2,
                            FieldAttributes.Family or FieldAttributes.FamANDAssem => 1,
                            _ => 0,
                        };
                        return new MemberMatch(access, (field.Attributes & FieldAttributes.Static) != 0, IsOverridable: false, []);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the generic constraints as "index: constraint" strings, e.g. "0: ReferenceTypeConstraint" or "0: System.IDisposable"
        /// </summary>
        private static HashSet<string> GetConstraints(MetadataReader reader, GenericParameterHandleCollection parameters)
        {
            var constraints = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameterHandle in parameters)
            {
                var parameter = reader.GetGenericParameter(parameterHandle);
                var special = parameter.Attributes & GenericParameterAttributes.SpecialConstraintMask;
                if (special != 0)
                {
                    constraints.Add($"{parameter.Index}: {special}");
                }

                foreach (var constraintHandle in parameter.GetConstraints())
                {
                    constraints.Add($"{parameter.Index}: {RenderType(reader, reader.GetGenericParameterConstraint(constraintHandle).Type)}");
                }
            }

            return constraints;
        }

        private static string RenderType(MetadataReader reader, EntityHandle type)
            => type.Kind switch
            {
                HandleKind.TypeDefinition => SignatureProvider.Instance.GetTypeFromDefinition(reader, (TypeDefinitionHandle)type, 0),
                HandleKind.TypeReference => SignatureProvider.Instance.GetTypeFromReference(reader, (TypeReferenceHandle)type, 0),
                _ => SignatureProvider.Instance.GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)type, 0),
            };

        private static HashSet<string> GetInternalsVisibleTo(MetadataReader reader)
        {
            var friends = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attributeHandle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(attributeHandle);
                var attributeType = attribute.Constructor.Kind switch
                {
                    HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
                    HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
                    _ => default,
                };

                var attributeTypeName = attributeType.Kind switch
                {
                    HandleKind.TypeReference => Names.Of(reader, (TypeReferenceHandle)attributeType),
                    HandleKind.TypeDefinition => Names.Of(reader, (TypeDefinitionHandle)attributeType),
                    _ => null,
                };

                if (attributeTypeName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
                {
                    var value = reader.GetBlobReader(attribute.Value);
                    value.ReadUInt16(); // prolog
                    if (value.ReadSerializedString() is { } friend)
                    {
                        friends.Add(friend);
                    }
                }
            }

            return friends;
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
                "System.Enum" => $"enum of {GetEnumUnderlyingType(reader, typeDef)}", // consumers inline enum constants as the underlying type
                _ => "class",
            };
        }

        private static string? GetEnumUnderlyingType(MetadataReader reader, TypeDefinition typeDef)
        {
            foreach (var fieldHandle in typeDef.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                if ((field.Attributes & FieldAttributes.Static) == 0)
                {
                    // The only instance field of an enum is value__, which has the underlying type
                    return field.DecodeSignature(SignatureProvider.Instance, null);
                }
            }

            return null;
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
        /// For an interface, this includes the members of the interfaces it inherits, because an implementing type
        /// compiled before the interface inherited them won't implement them. Inherited interfaces from other
        /// assemblies are included as "interface Name", as their members aren't available here.
        /// </summary>
        private static HashSet<string> GetUnimplementedAbstractMembers(MetadataReader reader, TypeDefinitionHandle typeHandle)
        {
            var implemented = new HashSet<string>(StringComparer.Ordinal);
            var unimplemented = new HashSet<string>(StringComparer.Ordinal);
            var typeDefinition = reader.GetTypeDefinition(typeHandle);
            if ((typeDefinition.Attributes & TypeAttributes.Interface) != 0)
            {
                // C# lists every inherited interface (including indirect ones) on the interface itself
                foreach (var interfaceImplementation in typeDefinition.GetInterfaceImplementations())
                {
                    var inherited = GetGenericTypeOrSelf(reader, reader.GetInterfaceImplementation(interfaceImplementation).Interface);
                    if (inherited.Kind == HandleKind.TypeDefinition)
                    {
                        unimplemented.UnionWith(GetUnimplementedAbstractMembers(reader, (TypeDefinitionHandle)inherited));
                    }
                    else
                    {
                        unimplemented.Add($"interface {RenderType(reader, inherited)}");
                    }
                }
            }

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

    // Access: 2 = accessible from anywhere, 1 = only from derived types, 0 = private (as for AccessLevel)
    private sealed record MemberMatch(int Access, bool IsStatic, bool IsOverridable, HashSet<string> Constraints);

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
