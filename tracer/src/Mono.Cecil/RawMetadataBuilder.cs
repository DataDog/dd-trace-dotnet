using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using System.Collections.Generic;
using System.Linq;
using BlobIndex = System.UInt32;
using CodedRID = System.UInt32;
using GuidIndex = System.UInt32;
using RID = System.UInt32;
using RVA = System.UInt32;
using StringIndex = System.UInt32;

namespace Mono.Cecil.Mono.Cecil {
	using TypeRefRow = Row<CodedRID, StringIndex, StringIndex>;

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
			var res = new List<TypeDefinition> (module.MetadataSystem.Types);
			foreach (var type in module.Types) {
				if (type.HasNestedTypes)
					res.AddRange (type.NestedTypes);
			}

			res = res.OrderBy (i => i.MetadataToken.ToUInt32 ()).ToList ();
			
			foreach(var type in res) {
				var fields = type.Fields.OrderBy(i => i.MetadataToken.ToUInt32()).ToList();
				if(fields.Count > 0) {
					type.fields_range.Start = fields [0].MetadataToken.RID;
					type.fields_range.Length = (uint)fields.Count;
				}
				var methods = type.Methods.OrderBy (i => i.MetadataToken.ToUInt32 ()).ToList ();
				if (methods.Count > 0) {
					type.methods_range.Start = methods [0].MetadataToken.RID;
					type.methods_range.Length = (uint)methods.Count;
				}

			}
			return res;
		}

		List<FieldDefinition> GetAllFieldsSorted ()
		{
			var res = new List<FieldDefinition> (module.MetadataSystem.Fields);
			res = res.OrderBy (i => i.MetadataToken.ToUInt32 ()).ToList ();
			return res;
		}

		List<MethodDefinition> GetAllMethodsSorted ()
		{
			var res = new List<MethodDefinition> (module.MetadataSystem.Methods);
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

		List<KeyValuePair<MetadataToken, byte[]>> GetAllStandaloneSigsSorted ()
		{ 
			return module.MetadataSystem.StandAloneSigs.OrderBy (i => i.Key.ToUInt32 ()).ToList();
		}
		List<KeyValuePair<RVA, string>> GetAllUserStringsSorted ()
		{ 
			return module.MetadataSystem.UserStrings.OrderBy (i => (uint)i.Key).ToList();
		}

		List<KeyValuePair<RVA, byte []>> GetAllBlobsSorted ()
		{
			return module.Image.BlobHeap.Blobs.OrderBy (i => (uint)i.Key).ToList ();
		}

		protected override void AttachTokens () 
		{
			// All tokens should be already assigned
		}

		protected override void BuildAssembly ()
		{
			foreach (var blob in GetAllBlobsSorted ())
				GetBlobIndex (blob.Value);

			base.BuildAssembly ();
		}

		protected override void AddTypes ()
		{
			foreach (var sig in GetAllStandaloneSigsSorted ()) {
				var rva = GetBlobIndex (new SignatureWriter (sig.Value));
				AddStandAloneSignature (rva);
			}

			foreach (var sig in GetAllUserStringsSorted ())
				user_string_heap.GetStringIndex (sig.Value);

			foreach (var typeRef in GetAllTypeRefsSorted ())
				GetTypeRefToken (typeRef);

			foreach (var typeRef in GetAllMemberRefsSorted ())
				GetMemberRefToken (typeRef);

			foreach (var field in GetAllFieldsSorted ())
				AddField (field);

			foreach (var method in GetAllMethodsSorted ())
				AddMethod (method);

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
