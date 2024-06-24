using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;

using Mono;
using Mono.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;

using RVA = System.UInt32;
using RID = System.UInt32;
using CodedRID = System.UInt32;
using StringIndex = System.UInt32;
using BlobIndex = System.UInt32;
using GuidIndex = System.UInt32;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Mono.Cecil.Mono.Cecil {
	using ModuleRow = Row<StringIndex, GuidIndex>;
	using TypeRefRow = Row<CodedRID, StringIndex, StringIndex>;
	using TypeDefRow = Row<TypeAttributes, StringIndex, StringIndex, CodedRID, RID, RID>;
	using FieldRow = Row<FieldAttributes, StringIndex, BlobIndex>;
	using MethodRow = Row<RVA, MethodImplAttributes, MethodAttributes, StringIndex, BlobIndex, RID>;
	using ParamRow = Row<ParameterAttributes, ushort, StringIndex>;
	using InterfaceImplRow = Row<uint, CodedRID>;
	using MemberRefRow = Row<CodedRID, StringIndex, BlobIndex>;
	using ConstantRow = Row<ElementType, CodedRID, BlobIndex>;
	using CustomAttributeRow = Row<CodedRID, CodedRID, BlobIndex>;
	using FieldMarshalRow = Row<CodedRID, BlobIndex>;
	using DeclSecurityRow = Row<SecurityAction, CodedRID, BlobIndex>;
	using ClassLayoutRow = Row<ushort, uint, RID>;
	using FieldLayoutRow = Row<uint, RID>;
	using EventMapRow = Row<RID, RID>;
	using EventRow = Row<EventAttributes, StringIndex, CodedRID>;
	using PropertyMapRow = Row<RID, RID>;
	using PropertyRow = Row<PropertyAttributes, StringIndex, BlobIndex>;
	using MethodSemanticsRow = Row<MethodSemanticsAttributes, RID, CodedRID>;
	using MethodImplRow = Row<RID, CodedRID, CodedRID>;
	using ImplMapRow = Row<PInvokeAttributes, CodedRID, StringIndex, RID>;
	using FieldRVARow = Row<RVA, RID>;
	using AssemblyRow = Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>;
	using AssemblyRefRow = Row<ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint, uint>;
	using FileRow = Row<FileAttributes, StringIndex, BlobIndex>;
	using ExportedTypeRow = Row<TypeAttributes, uint, StringIndex, StringIndex, CodedRID>;
	using ManifestResourceRow = Row<uint, ManifestResourceAttributes, StringIndex, CodedRID>;
	using NestedClassRow = Row<RID, RID>;
	using GenericParamRow = Row<ushort, GenericParameterAttributes, CodedRID, StringIndex>;
	using MethodSpecRow = Row<CodedRID, BlobIndex>;
	using GenericParamConstraintRow = Row<RID, CodedRID>;
	using DocumentRow = Row<BlobIndex, GuidIndex, BlobIndex, GuidIndex>;
	using MethodDebugInformationRow = Row<RID, BlobIndex>;
	using LocalScopeRow = Row<RID, RID, RID, RID, uint, uint>;
	using LocalVariableRow = Row<VariableAttributes, ushort, StringIndex>;
	using LocalConstantRow = Row<StringIndex, BlobIndex>;
	using ImportScopeRow = Row<RID, BlobIndex>;
	using StateMachineMethodRow = Row<RID, RID>;
	using CustomDebugInformationRow = Row<CodedRID, GuidIndex, BlobIndex>;

	class RawMetadataBuilder : MetadataBuilder {
		public RawMetadataBuilder (ModuleDefinition module, string fq_name, uint timestamp, ISymbolWriterProvider symbol_writer_provider)
			: base (module, fq_name, timestamp, symbol_writer_provider)
		{

		}

		public RawMetadataBuilder (ModuleDefinition module, PortablePdbWriterProvider writer_provider)
			: base (module, writer_provider)
		{
		}

		private bool buildRaw = false;

		List<TypeDefinition> GetAllTypesSorted ()
		{
			var res = new List<TypeDefinition> (module.Types);
			foreach (var type in module.Types) {
				if (type.HasNestedTypes)
					res.AddRange (type.NestedTypes);
			}

			res = res.OrderBy (i => i.MetadataToken.ToUInt32 ()).ToList ();
			return res;
		}

		List<TypeReference> GetAllTypeRefsSorted ()
		{
			return  module.GetTypeReferences().OrderBy (i => i.MetadataToken.ToUInt32 ()).ToList ();
		}

		List<MemberReference> GetAllMemberRefsSorted ()
		{
			return module.GetMemberReferences().OrderBy (i => i.MetadataToken.ToUInt32 ()).ToList ();
		}


		protected override void AttachTokens () 
		{
			// All tokens should be already assigned
		}

		protected override void AddTypes ()
		{
			foreach(var typeRef in GetAllTypeRefsSorted ())
				GetTypeRefToken (typeRef);

			foreach (var typeRef in GetAllMemberRefsSorted ())
				GetMemberRefToken (typeRef);

			foreach (var type in GetAllTypesSorted ())
				AddType (type);
		}

		protected override void AddNestedTypes (TypeDefinition type)
		{
			// Nested types are written in add order
		}
		protected override MetadataToken AddTypeReference (TypeReference type, TypeRefRow row)
		{
			type.token = new MetadataToken (TokenType.TypeRef, type_ref_table.AddRow (row));

			var token = type.token;
			type_ref_map.Add (row, token);
			return token;
		}

		protected override SignatureWriter GetMethodSignature (IMethodSignature method)
		{
			if (method.RawSignature != null) {
				return new SignatureWriter (method.RawSignature);
			}
			return base.GetMethodSignature (method);
		}

		protected override SignatureWriter GetFieldSignature (FieldReference field)
		{
			if (field is FieldDefinition fieldDef && fieldDef.RawSignature != null) {
				return new SignatureWriter (fieldDef.RawSignature);
			}
			return base.GetFieldSignature (field);
		}

	}

}
