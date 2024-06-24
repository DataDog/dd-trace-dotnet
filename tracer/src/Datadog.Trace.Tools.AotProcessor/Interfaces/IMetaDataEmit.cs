namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface IMetaDataEmit : IUnknown
{
    public static readonly new Guid Guid = new("BA3FEE4C-ECB9-4e41-83B7-183FA41CD859");

    HResult SetModuleProps(
        char* szName);           // [IN] If not NULL, the name of the module to set.

    HResult Save(
        char* szFile,                 // [IN] The filename to save to.
        int dwSaveFlags);      // [IN] Flags for the save.

    HResult SaveToStream(
        IntPtr pIStream,              // [IN] A writable stream to save to. IStream* pIStream
        int dwSaveFlags);      // [IN] Flags for the save.

    HResult GetSaveSize(
        CorSaveSize fSave,                  // [IN] cssAccurate or cssQuick.
        out int pdwSaveSize);     // [OUT] Put the size here.

    HResult DefineTypeDef(
        char* szTypeDef,              // [IN] Name of TypeDef
        int dwTypeDefFlags,         // [IN] CustomAttribute flags
        MdToken tkExtends,              // [IN] extends this TypeDef or typeref
        MdToken* rtkImplements,        // [IN] Implements interfaces
        MdTypeDef* ptd);             // [OUT] Put TypeDef token here

    HResult DefineNestedType(
        char* szTypeDef,                    // [IN] Name of TypeDef
        int dwTypeDefFlags,                 // [IN] CustomAttribute flags
        MdToken tkExtends,                  // [IN] extends this TypeDef or typeref
        MdToken* rtkImplements,             // [IN] Implements interfaces
        MdTypeDef tdEncloser,               // [IN] TypeDef token of the enclosing type.
        out MdTypeDef   ptd);                  // [OUT] Put TypeDef token here

    HResult SetHandler(                     // S_OK.
        IntPtr pUnk);                    // [IN] The new error handler. IUnknown*

    HResult DefineMethod(
        MdTypeDef td,                       // Parent TypeDef
        char* szName,                       // Name of member
        int dwMethodFlags,                  // Member attributes
        IntPtr  pvSigBlob,                   // [IN] point to a blob value of CLR signature
        int cbSigBlob,                      // [IN] count of bytes in the signature blob
        int ulCodeRVA,
        int dwImplFlags,
        MdMethodDef* pmd);                  // Put member token here

    HResult DefineMethodImpl(               // S_OK or error.
        MdTypeDef td,                       // [IN] The class implementing the method
        MdToken tkBody,                     // [IN] Method body - MethodDef or MethodRef
        MdToken tkDecl);                    // [IN] Method declaration - MethodDef or MethodRef

    HResult DefineTypeRefByName(            // S_OK or error.
        MdToken tkResolutionScope,          // [IN] ModuleRef, AssemblyRef or TypeRef.
        char* szName,                       // [IN] Name of the TypeRef.
        MdTypeRef* ptr);                    // [OUT] Put TypeRef token here.

    HResult DefineImportType(               // S_OK or error.
        IntPtr pAssemImport,                // [IN] Assembly containing the TypeDef.
        byte* pbHashValue,                  // [IN] Hash Blob for Assembly.
        int       cbHashValue,              // [IN] Count of bytes.
        IntPtr pImport,                     // [IN] Scope containing the TypeDef. IMetaDataImport* pImport
        MdTypeDef   tdImport,               // [IN] The imported TypeDef.
        IntPtr pAssemEmit,                  // [IN] Assembly into which the TypeDef is imported. IMetaDataAssemblyEmit* pAssemEmit
        out MdTypeRef ptr);                 // [OUT] Put TypeRef token here.

    HResult DefineMemberRef(                // S_OK or error
        MdToken tkImport,                   // [IN] ClassRef or ClassDef importing a member.
        char* szName,                       // [IN] member's name
        IntPtr pvSigBlob,                   // [IN] point to a blob value of CLR signature
        int cbSigBlob,                      // [IN] count of bytes in the signature blob
        MdMemberRef* pmr);                  // [OUT] memberref token

    HResult DefineImportMember(
        IntPtr pAssemImport,                // [IN] Assembly containing the Member. IMetaDataAssemblyImport* pAssemImport
        byte* pbHashValue,                  // [IN] Hash Blob for Assembly. IntPtr pbHashValue
        int       cbHashValue,              // [IN] Count of bytes.
        IntPtr pImport,                     // [IN] Import scope, with member. IMetaDataImport* pImport
        MdToken     mbMember,               // [IN] Member in import scope.
        IntPtr pAssemEmit,                  // [IN] Assembly into which the Member is imported. IMetaDataAssemblyEmit* pAssemEmit
        MdToken     tkParent,               // [IN] Classref or classdef in emit scope.
        out MdMemberRef pmr);               // [OUT] Put member ref here.

    HResult DefineEvent(
        MdTypeDef td,                       // [IN] the class/interface on which the event is being defined
        char* szEvent,                      // [IN] Name of the event
        int dwEventFlags,                   // [IN] CorEventAttr
        MdToken tkEventType,                // [IN] a reference (mdTypeRef or MdTypeRef) to the Event class
        MdMethodDef MdAddOn,                // [IN] required add method
        MdMethodDef MdRemoveOn,             // [IN] required remove method
        MdMethodDef MdFire,                 // [IN] optional fire method
        MdMethodDef* rmdOtherMethods,       // [IN] optional array of other methods associate with the event
        MdEvent* pmdEvent);                 // [OUT] output event token

    HResult SetClassLayout(
        MdTypeDef td,                       // [IN] typedef
        int dwPackSize,                     // [IN] packing size specified as 1, 2, 4, 8, or 16
        COR_FIELD_OFFSET* rFieldOffsets,    // [IN] array of layout specification
        int ulClassSize);                   // [IN] size of the class

    HResult DeleteClassLayout(
        MdTypeDef td);                      // [IN] typedef whose layout is to be deleted.

    HResult SetFieldMarshal(
        MdToken tk,                     // [IN] given a fieldDef or paramDef token
        byte* pvNativeType,       // [IN] native type specification byte* pvNativeType
        int cbNativeType);     // [IN] count of bytes of pvNativeType

    HResult DeleteFieldMarshal(
        MdToken tk);               // [IN] given a fieldDef or paramDef token

    HResult DefinePermissionSet(
        MdToken tk,                     // [IN] the object to be decorated.
        int dwAction,               // [IN] CorDeclSecurity.
        IntPtr pvPermission,          // [IN] permission blob.
        int       cbPermission,           // [IN] count of bytes of pvPermission.
        out MdPermission ppm);            // [OUT] returned permission token.

    HResult SetRVA(
        MdMethodDef Md,                     // [IN] Method for which to set offset
        int ulRVA);            // [IN] The offset

    HResult GetTokenFromSig(
        IntPtr pvSig,              // [IN] Signature to define.
        int cbSig,                  // [IN] Size of signature data.
        MdSignature* pmsig);           // [OUT] returned signature token.

    HResult DefineModuleRef(
        char* szName,                 // [IN] DLL name
        MdModuleRef* pmur);            // [OUT] returned

    // <TODO>@FUTURE:  This should go away once everyone starts using SetMemberRefProps.</TODO>
    HResult SetParent(
        MdMemberRef mr,                     // [IN] Token for the ref to be fixed up.
        MdToken tk);               // [IN] The ref parent.

    HResult GetTokenFromTypeSpec(
        byte* pvSig,              // [IN] TypeSpec Signature to define.
        int cbSig,                  // [IN] Size of signature data.
        out MdTypeSpec ptypespec);        // [OUT] returned TypeSpec token.

    HResult SaveToMemory(
        IntPtr pbData,                // [OUT] Location to write data.
        int cbData);           // [IN] Max size of data buffer.

    HResult DefineUserString(            // Return code.
        char* szString,                   // [IN] User literal string.
        int cchString,              // [IN] Length of string.
        MdString* pstk);            // [OUT] String token.

    HResult DeleteToken(                 // Return code.
        MdToken tkObj);            // [IN] The token to be deleted

    HResult SetMethodProps(
        MdMethodDef Md,                     // [IN] The MethodDef.
        int dwMethodFlags,          // [IN] Method attributes.
        int ulCodeRVA,              // [IN] Code RVA.
        int dwImplFlags);      // [IN] Impl flags.

    HResult SetTypeDefProps(
        MdTypeDef td,                     // [IN] The TypeDef.
        int dwTypeDefFlags,         // [IN] TypeDef flags.
        MdToken tkExtends,              // [IN] Base TypeDef or TypeRef.
        MdToken* rtkImplements);  // [IN] Implemented interfaces.

    HResult SetEventProps(
        MdEvent ev,                         // [IN] The event token.
        int dwEventFlags,                   // [IN] CorEventAttr.
        MdToken tkEventType,                // [IN] A reference (mdTypeRef or MdTypeRef) to the Event class.
        MdMethodDef MdAddOn,                // [IN] Add method.
        MdMethodDef MdRemoveOn,             // [IN] Remove method.
        MdMethodDef MdFire,                 // [IN] Fire method.
        MdMethodDef* rmdOtherMethods);      // [IN] Array of other methods associate with the event.

    HResult SetPermissionSetProps(
        MdToken tk,                     // [IN] The object to be decorated.
        int dwAction,               // [IN] CorDeclSecurity.
        IntPtr pvPermission,          // [IN] Permission blob.
        int cbPermission,           // [IN] Count of bytes of pvPermission.
        out MdPermission ppm);            // [OUT] Permission token.

    HResult DefinePinvokeMap(
        MdToken tk,                     // [IN] FieldDef or MethodDef.
        int dwMappingFlags,         // [IN] Flags used for mapping.
        char* szImportName,           // [IN] Import name.
        MdModuleRef mrImportDLL);      // [IN] ModuleRef token for the target DLL.

    HResult SetPinvokeMap(
        MdToken tk,                     // [IN] FieldDef or MethodDef.
        int dwMappingFlags,         // [IN] Flags used for mapping.
        char* szImportName,           // [IN] Import name.
        MdModuleRef mrImportDLL);      // [IN] ModuleRef token for the target DLL.

    HResult DeletePinvokeMap(
        MdToken tk);               // [IN] FieldDef or MethodDef.

    // New CustomAttribute functions.
    HResult DefineCustomAttribute(      // Return code.
        MdToken tkOwner,                // [IN] The object to put the value on.
        MdToken tkCtor,                 // [IN] Constructor of the CustomAttribute type (MemberRef/MethodDef).
        IntPtr pCustomAttribute,        // [IN] The custom value data.
        int       cbCustomAttribute,    // [IN] The custom value data length.
        MdCustomAttribute* pcv);        // [OUT] The custom value token value on return.

    HResult SetCustomAttributeValue(    // Return code.
        MdCustomAttribute pcv,          // [IN] The custom value token whose value to replace.
        IntPtr pCustomAttribute,        // [IN] The custom value data.
        int       cbCustomAttribute);   // [IN] The custom value data length.

    HResult DefineField(
        MdTypeDef td,                     // Parent TypeDef
        char* szName,                 // Name of member
        int dwFieldFlags,           // Member attributes
        IntPtr pvSigBlob,          // [IN] point to a blob value of CLR signature
        int cbSigBlob,              // [IN] count of bytes in the signature blob
        int dwCPlusTypeFlag,        // [IN] flag for value type. selected ELEMENT_TYPE_*
        IntPtr pValue,                // [IN] constant value
        int       cchValue,               // [IN] size of constant value (string, in wide chars).
        MdFieldDef* pmd);             // [OUT] Put member token here

    HResult DefineProperty(
        MdTypeDef td,                     // [IN] the class/interface on which the property is being defined
        char* szProperty,             // [IN] Name of the property
        int dwPropFlags,            // [IN] CorPropertyAttr
        byte* pvSig,              // [IN] the required type signature
        int cbSig,                  // [IN] the size of the type signature blob
        int dwCPlusTypeFlag,        // [IN] flag for value type. selected ELEMENT_TYPE_*
        IntPtr pValue,                // [IN] constant value
        int       cchValue,               // [IN] size of constant value (string, in wide chars).
        MdMethodDef MdSetter,               // [IN] optional setter of the property
        MdMethodDef MdGetter,               // [IN] optional getter of the property
        MdMethodDef* rmdOtherMethods,      // [IN] an optional array of other methods
        out MdProperty  pmdProp);         // [OUT] output property token

    HResult DefineParam(
        MdMethodDef Md,                     // [IN] Owning method
        int ulParamSeq,             // [IN] Which param
        char* szName,                 // [IN] Optional param name
        int dwParamFlags,           // [IN] Optional param flags
        int dwCPlusTypeFlag,        // [IN] flag for value type. selected ELEMENT_TYPE_*
        IntPtr pValue,                // [IN] constant value
        int       cchValue,               // [IN] size of constant value (string, in wide chars).
        MdParamDef* ppd);             // [OUT] Put param token here

    HResult SetFieldProps(
        MdFieldDef fd,                     // [IN] The FieldDef.
        int dwFieldFlags,           // [IN] Field attributes.
        int dwCPlusTypeFlag,        // [IN] Flag for the value type, selected ELEMENT_TYPE_*
        IntPtr pValue,                // [IN] Constant value.
        int       cchValue);         // [IN] size of constant value (string, in wide chars).

    HResult SetPropertyProps(
        MdProperty pr,                     // [IN] Property token.
        int dwPropFlags,            // [IN] CorPropertyAttr.
        int dwCPlusTypeFlag,        // [IN] Flag for value type, selected ELEMENT_TYPE_*
        IntPtr pValue,                // [IN] Constant value.
        int       cchValue,               // [IN] size of constant value (string, in wide chars).
        MdMethodDef MdSetter,               // [IN] Setter of the property.
        MdMethodDef MdGetter,               // [IN] Getter of the property.
        MdMethodDef* rmdOtherMethods);      // [IN] Array of other methods.

    HResult SetParamProps(
        MdParamDef pd,                     // [IN] Param token.
        char* szName,                 // [IN] Param name.
        int dwParamFlags,           // [IN] Param flags.
        int dwCPlusTypeFlag,        // [IN] Flag for value type. selected ELEMENT_TYPE_*.
        IntPtr pValue,                // [OUT] Constant value.
        int       cchValue);         // [IN] size of constant value (string, in wide chars).

    // Specialized Custom Attributes for security.
    HResult DefineSecurityAttributeSet(  // Return code.
        MdToken tkObj,                  // [IN] Class or method requiring security attributes.
        COR_SECATTR* rSecAttrs,            // [IN] Array of security attribute descriptions. COR_SECATTR rSecAttrs[]
        int cSecAttrs,              // [IN] Count of elements in above array.
        out int pulErrorAttr);    // [OUT] On error, index of attribute causing problem.

    HResult ApplyEditAndContinue(
        IntPtr pImport);         // [IN] Metadata from the delta PE. known* pImport

    HResult TranslateSigWithScope(
        IntPtr pAssemImport, // [IN] importing assembly interface IMetaDataAssemblyImport* pAssemImport
        IntPtr pbHashValue,           // [IN] Hash Blob for Assembly.
        int       cbHashValue,            // [IN] Count of bytes.
        IntPtr import,            // [IN] importing interface IMetaDataImport* import
        byte* pbSigBlob,          // [IN] signature in the importing scope
        int cbSigBlob,              // [IN] count of bytes of signature
        IntPtr pAssemEmit,  // [IN] emit assembly interface IMetaDataAssemblyEmit *pAssemEmit
        IntPtr emit,                    // [IN] emit interface IMetaDataEmit* emit
        byte* pvTranslatedSig,          // [OUT] buffer to hold translated signature PCOR_SIGNATURE pvTranslatedSig
        int cbTranslatedSigMax,
        out int pcbTranslatedSig);      // [OUT] count of bytes in the translated signature

    HResult SetMethodImplFlags(
        MdMethodDef Md,                     // [IN] Method for which to set ImplFlags
        int dwImplFlags);

    HResult SetFieldRVA(
        MdFieldDef fd,                     // [IN] Field for which to set offset
        int ulRVA);            // [IN] The offset

    HResult Merge(
        IntPtr pImport,           // [IN] The scope to be merged. IMetaDataImport* pImport
        IntPtr pHostMapToken,         // [IN] Host IMapToken interface to receive token remap notification IMapToken* pHostMapToken
        IntPtr pHandler);        // [IN] An object to receive to receive error notification. IUnknown* pHandler

    HResult MergeEnd();             // S_OK or error.
}
