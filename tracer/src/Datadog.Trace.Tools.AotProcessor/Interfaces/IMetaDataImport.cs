namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface IMetaDataImport : IUnknown
{
    public static readonly new Guid Guid = new("7DAC8207-D3AE-4c75-9B67-92801A497D44");

    void CloseEnum(HCORENUM hEnum);
    HResult CountEnum(HCORENUM hEnum, uint* pulCount);
    HResult ResetEnum(HCORENUM hEnum, uint ulPos);
    HResult EnumTypeDefs(HCORENUM* phEnum, MdTypeDef* rTypeDefs, uint cMax, uint* pcTypeDefs);
    HResult EnumInterfaceImpls(HCORENUM* phEnum, MdTypeDef td, MdInterfaceImpl* rImpls, uint cMax, uint* pcImpls);
    HResult EnumTypeRefs(HCORENUM* phEnum, MdTypeRef* rTypeRefs, uint cMax, uint* pcTypeRefs);

    HResult FindTypeDefByName(
        char* szTypeDef,              // [IN] Name of the Type.
        MdToken tkEnclosingClass,       // [IN] TypeDef/TypeRef for Enclosing class.
        MdTypeDef* ptd);             // [OUT] Put the TypeDef token here.

    HResult GetScopeProps(
        char* szName,                 // [OUT] Put the name here.
        uint cchName,                // [IN] Size of name buffer in wide chars.
        out uint pchName,               // [OUT] Put size of name (wide chars) here.
        out Guid pmvid);           // [OUT, OPTIONAL] Put MVID here.

    HResult GetModuleFromScope(
        out MdModule pmd);             // [OUT] Put mdModule token here.

    HResult GetTypeDefProps(
        MdTypeDef td,                   // [IN] TypeDef token for inquiry.
        char* szTypeDef,                // [OUT] Put name here.
        uint cchTypeDef,                // [IN] size of name buffer in wide chars.
        uint* pchTypeDef,               // [OUT] put size of name (wide chars) here.
        int* pdwTypeDefFlags,           // [OUT] Put flags here.
        MdToken* ptkExtends);           // [OUT] Put base class TypeDef/TypeRef here.

    HResult GetInterfaceImplProps(
        MdInterfaceImpl iiImpl,             // [IN] InterfaceImpl token.
        out MdTypeDef pClass,                // [OUT] Put implementing class token here.
        out MdToken ptkIface);        // [OUT] Put implemented interface token here.

    HResult GetTypeRefProps(
        MdTypeRef tr,                       // [IN] TypeRef token.
        MdToken* ptkResolutionScope,     // [OUT] Resolution scope, ModuleRef or AssemblyRef.
        char* szName,                       // [OUT] Name of the TypeRef.
        uint cchName,                       // [IN] Size of buffer.
        uint* pchName);                  // [OUT] Size of Name.

    HResult ResolveTypeRef(MdTypeRef tr, in Guid riid, void** ppIScope, out MdTypeDef ptd);

    HResult EnumMembers(                 // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        MdToken* rMembers,             // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumMembersWithName(         // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        char* szName,                 // [IN] Limit results to those with this name.
        MdToken* rMembers,             // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumMethods(                 // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        MdMethodDef* rMethods,             // [OUT] Put MethodDefs here.
        uint cMax,                   // [IN] Max MethodDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumMethodsWithName(         // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        char* szName,                 // [IN] Limit results to those with this name.
        MdMethodDef* rMethods,             // [OU] Put MethodDefs here.
        uint cMax,                   // [IN] Max MethodDefs to put.
        uint* pcTokens);        // [OUT] Put # put here.

    HResult EnumFields(                  // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        MdFieldDef* rFields,              // [OUT] Put FieldDefs here.
        uint cMax,                   // [IN] Max FieldDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumFieldsWithName(          // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        char* szName,                 // [IN] Limit results to those with this name.
        MdFieldDef* rFields,              // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumParams(                  // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdMethodDef mb,                     // [IN] MethodDef to scope the enumeration.
        MdParamDef* rParams,              // [OUT] Put ParamDefs here.
        uint cMax,                   // [IN] Max ParamDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumMemberRefs(              // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdToken tkParent,               // [IN] Parent token to scope the enumeration.
        MdMemberRef* rMemberRefs,          // [OUT] Put MemberRefs here.
        uint cMax,                   // [IN] Max MemberRefs to put.
        uint* pcTokens);        // [OUT] Put # put here.

    HResult EnumMethodImpls(             // S_OK, S_FALSE, or error
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef td,                     // [IN] TypeDef to scope the enumeration.
        MdToken* rMethodBody,          // [OUT] Put Method Body tokens here.
        MdToken* rMethodDecl,          // [OUT] Put Method Declaration tokens here.
        uint cMax,                   // [IN] Max tokens to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult EnumPermissionSets(          // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdToken tk,                     // [IN] if !NIL, token to scope the enumeration.
        int dwActions,              // [IN] if !0, return only these actions.
        MdPermission* rPermission,         // [OUT] Put Permissions here.
        uint cMax,                   // [IN] Max Permissions to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult FindMember(
        MdTypeDef td,                     // [IN] given typedef
        char* szName,                 // [IN] member name
        nint* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        out MdToken pmb);             // [OUT] matching memberdef

    HResult FindMethod(
        MdTypeDef td,                     // [IN] given typedef
        char* szName,                 // [IN] member name
        nint* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        MdMethodDef* pmb);             // [OUT] matching memberdef

    HResult FindField(
        MdTypeDef td,                     // [IN] given typedef
        char* szName,                 // [IN] member name
        nint* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        out MdFieldDef pmb);             // [OUT] matching memberdef

    HResult FindMemberRef(
        MdTypeRef td,                     // [IN] given typeRef
        char* szName,                 // [IN] member name
        nint* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        out MdMemberRef pmr);             // [OUT] matching memberref

    HResult GetMethodProps(
        MdMethodDef mb,                     // The method for which to get props.
        MdToken* pClass,                // Put method's class here.
        char* szMethod,               // Put method's name here.
        uint cchMethod,              // Size of szMethod buffer in wide chars.
        uint* pchMethod,             // Put actual size here
        int* pdwAttr,               // Put flags here.
        IntPtr* ppvSigBlob,        // [OUT] point to the blob value of meta data
        uint* pcbSigBlob,            // [OUT] actual size of signature blob
        uint* pulCodeRVA,            // [OUT] codeRVA
        int* pdwImplFlags);    // [OUT] Impl. Flags

    HResult GetMemberRefProps(
        MdMemberRef mr,                     // [IN] given memberref
        MdToken* ptk,                   // [OUT] Put classref or classdef here.
        char* szMember,               // [OUT] buffer to fill for member's name
        uint cchMember,              // [IN] the count of char of szMember
        uint* pchMember,             // [OUT] actual count of char in member name
        IntPtr* ppvSigBlob,        // [OUT] point to meta data blob value
        uint* pbSig);           // [OUT] actual size of signature blob

    HResult EnumProperties(              // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef td,                     // [IN] TypeDef to scope the enumeration.
        MdProperty* rProperties,          // [OUT] Put Properties here.
        uint cMax,                   // [IN] Max properties to put.
        uint* pcProperties);    // [OUT] Put # put here.

    HResult EnumEvents(                  // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef td,                     // [IN] TypeDef to scope the enumeration.
        MdEvent* rEvents,              // [OUT] Put events here.
        uint cMax,                   // [IN] Max events to put.
        out uint pcEvents);        // [OUT] Put # put here.

    HResult GetEventProps(                  // S_OK, S_FALSE, or error.
        MdEvent ev,                         // [IN] event token
        MdTypeDef* pClass,                  // [OUT] typedef containing the event declarion.
        char* szEvent,                      // [OUT] Event name
        uint cchEvent,                      // [IN] the count of wchar of szEvent
        uint* pchEvent,                     // [OUT] actual count of wchar for event's name
        int* pdwEventFlags,                 // [OUT] Event flags.
        MdToken* ptkEventType,              // [OUT] EventType class
        out MdMethodDef pmdAddOn,           // [OUT] AddOn method of the event
        out MdMethodDef pmdRemoveOn,        // [OUT] RemoveOn method of the event
        out MdMethodDef pmdFire,            // [OUT] Fire method of the event
        out MdMethodDef* rmdOtherMethod,    // [OUT] other method of the event
        uint cMax,                          // [IN] size of rmdOtherMethod
        out uint pcOtherMethod);            // [OUT] total number of other method of this event

    HResult EnumMethodSemantics(            // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                   // [IN|OUT] Pointer to the enum.
        MdMethodDef mb,                     // [IN] MethodDef to scope the enumeration.
        out MdToken* rEventProp,            // [OUT] Put Event/Property here.
        uint cMax,                          // [IN] Max properties to put.
        out uint pcEventProp);              // [OUT] Put # put here.

    HResult GetMethodSemantics(             // S_OK, S_FALSE, or error.
        MdMethodDef mb,                     // [IN] method token
        MdToken tkEventProp,                // [IN] event/property token.
        out int pdwSemanticsFlags);         // [OUT] the role flags for the method/propevent pair

    HResult GetClassLayout(
        MdTypeDef td,                     // [IN] give typedef
        out int pdwPackSize,           // [OUT] 1, 2, 4, 8, or 16
        COR_FIELD_OFFSET* rFieldOffset,    // [OUT] field offset array
        uint cMax,                   // [IN] size of the array
        out uint pcFieldOffset,         // [OUT] needed array size
        out uint pulClassSize);        // [OUT] the size of the class

    HResult GetFieldMarshal(
        MdToken tk,                     // [IN] given a field's memberdef
        out nint* ppvNativeType,     // [OUT] native type of this field
        out uint pcbNativeType);   // [OUT] the count of bytes of *ppvNativeType

    HResult GetRVA(
        MdToken tk,                     // Member for which to set offset
        uint* pulCodeRVA,            // The offset
        int* pdwImplFlags);    // the implementation flags

    HResult GetPermissionSetProps(
        MdPermission pm,                    // [IN] the permission token.
        out int pdwAction,             // [OUT] CorDeclSecurity.
        out void* ppvPermission,        // [OUT] permission blob.
        out uint pcbPermission);   // [OUT] count of bytes of pvPermission.

    HResult GetSigFromToken(
        MdSignature mdSig,                  // [IN] Signature token.
        out nint* ppvSig,            // [OUT] return pointer to token.
        out uint pcbSig);          // [OUT] return size of signature.

    HResult GetModuleRefProps(
        MdModuleRef mur,                    // [IN] moduleref token.
        char* szName,                 // [OUT] buffer to fill with the moduleref name.
        uint cchName,                // [IN] size of szName in wide characters.
        out uint pchName);         // [OUT] actual count of characters in the name.

    HResult EnumModuleRefs(
        HCORENUM* phEnum,                // [IN|OUT] pointer to the enum.
        MdModuleRef* rModuleRefs,          // [OUT] put modulerefs here.
        uint cmax,                   // [IN] max memberrefs to put.
        out uint pcModuleRefs);    // [OUT] put # put here.

    HResult GetTypeSpecFromToken(
        MdTypeSpec typespec,                // [IN] TypeSpec token.
        out nint* ppvSig,            // [OUT] return pointer to TypeSpec signature
        out uint pcbSig);          // [OUT] return size of signature.

    HResult GetNameFromToken(            // Not Recommended! May be removed!
        MdToken tk,                     // [IN] Token to get name from.  Must have a name.
        out byte* pszUtf8NamePtr);  // [OUT] Return pointer to UTF8 name in heap.

    HResult EnumUnresolvedMethods(       // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdToken* rMethods,             // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens);        // [OUT] Put # put here.

    HResult GetUserString(
        MdString stk,                    // [IN] String token.
        char* szString,               // [OUT] Copy of string.
        uint cchString,              // [IN] Max chars of room in szString.
        uint* pchString);       // [OUT] How many chars in actual string.

    HResult GetPinvokeMap(
        MdToken tk,                     // [IN] FieldDef or MethodDef.
        out int pdwMappingFlags,       // [OUT] Flags used for mapping.
        char* szImportName,           // [OUT] Import name.
        uint cchImportName,          // [IN] Size of the name buffer.
        out uint pchImportName,         // [OUT] Actual number of characters stored.
        out MdModuleRef pmrImportDLL);    // [OUT] ModuleRef token for the target DLL.

    HResult EnumSignatures(
        HCORENUM* phEnum,                // [IN|OUT] pointer to the enum.
        MdSignature* rSignatures,          // [OUT] put signatures here.
        uint cmax,                   // [IN] max signatures to put.
        out uint pcSignatures);    // [OUT] put # put here.

    HResult EnumTypeSpecs(
        HCORENUM* phEnum,                // [IN|OUT] pointer to the enum.
        MdTypeSpec* rTypeSpecs,           // [OUT] put TypeSpecs here.
        uint cmax,                   // [IN] max TypeSpecs to put.
        out uint pcTypeSpecs);     // [OUT] put # put here.

    HResult EnumUserStrings(
        HCORENUM* phEnum,                // [IN/OUT] pointer to the enum.
        MdString* rStrings,             // [OUT] put Strings here.
        uint cmax,                   // [IN] max Strings to put.
        out uint pcStrings);       // [OUT] put # put here.

    HResult GetParamForMethodIndex(
        MdMethodDef md,                     // [IN] Method token.
        uint ulParamSeq,             // [IN] Parameter sequence.
        out MdParamDef ppd);             // [IN] Put Param token here.

    HResult EnumCustomAttributes(
        HCORENUM* phEnum,                // [IN, OUT] COR enumerator.
        MdToken tk,                     // [IN] Token to scope the enumeration, 0 for all.
        MdToken tkType,                 // [IN] Type of interest, 0 for all.
        MdCustomAttribute* rCustomAttributes, // [OUT] Put custom attribute tokens here.
        uint cMax,                   // [IN] Size of rCustomAttributes.
        uint* pcCustomAttributes);  // [OUT, OPTIONAL] Put count of token values here.

    HResult GetCustomAttributeProps(
        MdCustomAttribute cv,               // [IN] CustomAttribute token.
        MdToken* ptkObj,                // [OUT, OPTIONAL] Put object token here.
        MdToken* ptkType,               // [OUT, OPTIONAL] Put AttrType token here.
        IntPtr* ppBlob,               // [OUT, OPTIONAL] Put pointer to data here.
        uint* pcbSize);         // [OUT, OPTIONAL] Put size of date here.

    HResult FindTypeRef(
        MdToken tkResolutionScope,      // [IN] ModuleRef, AssemblyRef or TypeRef.
        char* szName,                 // [IN] TypeRef Name.
        MdTypeRef* ptr);             // [OUT] matching TypeRef.

    HResult GetMemberProps(
        MdToken mb,                     // The member for which to get props.
        MdToken* pClass,              // Put member's class here.
        char* szMember,                 // Put member's name here.
        uint cchMember,                 // Size of szMember buffer in wide chars.
        uint* pchMember,                // Put actual size here
        int* pdwAttr,                   // Put flags here.
        IntPtr* ppvSigBlob,             // [OUT] point to the blob value of meta data
        uint* pcbSigBlob,               // [OUT] actual size of signature blob
        uint* pulCodeRVA,               // [OUT] codeRVA
        int* pdwImplFlags,              // [OUT] Impl. Flags
        int* pdwCPlusTypeFlag,          // [OUT] flag for value type. selected ELEMENT_TYPE_*
        char* ppValue,                  // [OUT] constant value
        uint* pcchValue);               // [OUT] size of constant string in chars, 0 for non-strings.

    HResult GetFieldProps(
        MdFieldDef mb,                     // The field for which to get props.
        MdTypeDef* pClass,                // Put field's class here.
        char* szField,                // Put field's name here.
        uint cchField,               // Size of szField buffer in wide chars.
        uint* pchField,              // Put actual size here
        int* pdwAttr,               // Put flags here.
        out nint* ppvSigBlob,        // [OUT] point to the blob value of meta data
        out uint pcbSigBlob,            // [OUT] actual size of signature blob
        out int pdwCPlusTypeFlag,      // [OUT] flag for value type. selected ELEMENT_TYPE_*
        out byte ppValue,             // [OUT] constant value
        out uint pcchValue);       // [OUT] size of constant string in chars, 0 for non-strings.

    HResult GetPropertyProps(            // S_OK, S_FALSE, or error.
        MdProperty prop,                   // [IN] property token
        MdTypeDef* pClass,                // [OUT] typedef containing the property declarion.
        char* szProperty,             // [OUT] Property name
        uint cchProperty,            // [IN] the count of wchar of szProperty
        uint* pchProperty,           // [OUT] actual count of wchar for property name
        int* pdwPropFlags,          // [OUT] property flags.
        IntPtr* ppvSig,            // [OUT] property type. pointing to meta data internal blob
        uint* pbSig,                 // [OUT] count of bytes in *ppvSig
        int* pdwCPlusTypeFlag,      // [OUT] flag for value type. selected ELEMENT_TYPE_*
        IntPtr* ppDefaultValue,      // [OUT] constant value
        uint* pcchDefaultValue,      // [OUT] size of constant string in chars, 0 for non-strings.
        MdMethodDef* pmdSetter,             // [OUT] setter method of the property
        MdMethodDef* pmdGetter,             // [OUT] getter method of the property
        MdMethodDef* rmdOtherMethod,       // [OUT] other method of the property
        uint cMax,                   // [IN] size of rmdOtherMethod
        uint* pcOtherMethod);   // [OUT] total number of other method of this property

    HResult GetParamProps(
        MdParamDef tk,                     // [IN]The Parameter.
        out MdMethodDef pmd,                   // [OUT] Parent Method token.
        out uint pulSequence,           // [OUT] Parameter sequence.
        char* szName,                 // [OUT] Put name here.
        uint cchName,                // [OUT] Size of name buffer.
        out uint pchName,               // [OUT] Put actual size of name here.
        out int pdwAttr,               // [OUT] Put flags here.
        out int pdwCPlusTypeFlag,      // [OUT] Flag for value type. selected ELEMENT_TYPE_*.
        out byte ppValue,             // [OUT] Constant value.
        out uint pcchValue);       // [OUT] size of constant string in chars, 0 for non-strings.

    HResult GetCustomAttributeByName(
        MdToken tkObj,                  // [IN] Object with Custom Attribute.
        char* szName,                 // [IN] Name of desired Custom Attribute.
        out void* ppData,               // [OUT] Put pointer to data here.
        out uint pcbData);         // [OUT] Put size of data here.

    bool IsValidToken(
        MdToken tk);               // [IN] Given token.

    HResult GetNestedClassProps(
        MdTypeDef tdNestedClass,          // [IN] NestedClass token.
        MdTypeDef* ptdEnclosingClass); // [OUT] EnclosingClass token.

    HResult GetNativeCallConvFromSig(
        void* pvSig,                 // [IN] Pointer to signature.
        uint cbSig,                  // [IN] Count of signature bytes.
        out uint pCallConv);       // [OUT] Put calling conv here (see CorPinvokemap).

    HResult IsGlobal(
        MdToken pd,                     // [IN] Type, Field, or Method token.
        out int pbGlobal);        // [OUT] Put 1 if global, 0 otherwise.
}
