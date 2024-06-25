using Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface IMetaDataImport2 : IMetaDataImport
{
    public static readonly new Guid Guid = new("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");

    HResult EnumGenericParams(
        HCORENUM* phEnum,                       // [IN|OUT] Pointer to the enum.
        MdToken tk,                             // [IN] TypeDef or MethodDef whose generic parameters are requested
        out MdGenericParam* rGenericParams,     // [OUT] Put GenericParams here.
        uint cMax,                              // [IN] Max GenericParams to put.
        out uint pcGenericParams);              // [OUT] Put # put here.

    HResult GetGenericParamProps(
        MdGenericParam gp,                      // [IN] GenericParam
        out uint pulParamSeq,                   // [OUT] Index of the type parameter
        out int pdwParamFlags,                  // [OUT] Flags, for future use (e.g. variance)
        MdToken* ptOwner,                       // [OUT] Owner (TypeDef or MethodDef)
        out int reserved,                       // [OUT] For future use (e.g. non-type parameters)
        char* wzname,                           // [OUT] Put name here
        uint cchName,                           // [IN] Size of buffer
        out uint pchName);                      // [OUT] Put size of name (wide chars) here.

    HResult GetMethodSpecProps(
        MdMethodSpec mi,                        // [IN] The method instantiation
        MdToken* tkParent,                   // [OUT] MethodDef or MemberRef
        IntPtr* ppvSigBlob,                   // [OUT] point to the blob value of meta data
        uint* pcbSigBlob);                   // [OUT] actual size of signature blob

    HResult EnumGenericParamConstraints(
        HCORENUM* phEnum,                       // [IN|OUT] Pointer to the enum.
        MdGenericParam tk,                      // [IN] GenericParam whose constraints are requested
        out MdGenericParamConstraint* rGenericParamConstraints,    // [OUT] Put GenericParamConstraints here.
        uint cMax,                              // [IN] Max GenericParamConstraints to put.
        out uint pcGenericParamConstraints);    // [OUT] Put # put here.

    HResult GetGenericParamConstraintProps(
        MdGenericParamConstraint gpc,           // [IN] GenericParamConstraint
        MdGenericParam* ptGenericParam,         // [OUT] GenericParam that is constrained
        out MdToken ptkConstraintType);         // [OUT] TypeDef/Ref/Spec constraint

    HResult GetPEKind(
        out int pdwPEKind,                      // [OUT] The kind of PE (0 - not a PE)
        out int pdwMAchine);                    // [OUT] Machine as defined in NT header

    HResult GetVersionString(
        char* pwzBuf,                           // [OUT] Put version string here.
        int ccBufSize,                          // [IN] size of the buffer, in wide chars
        out int pccBufSize);                    // [OUT] Size of the version string, wide chars, including terminating nul.

    HResult EnumMethodSpecs(
        HCORENUM* phEnum,                       // [IN|OUT] Pointer to the enum.
        MdToken tk,                             // [IN] MethodDef or MemberRef whose MethodSpecs are requested
        out MdMethodSpec* rMethodSpecs,         // [OUT] Put MethodSpecs here.
        uint cMax,                              // [IN] Max tokens to put.
        out uint pcMethodSpecs);                // [OUT] Put actual count here.
}
