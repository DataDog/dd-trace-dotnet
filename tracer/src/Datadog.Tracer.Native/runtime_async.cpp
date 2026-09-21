#include "runtime_async.h"

namespace trace
{

namespace
{
    // The effective return signature for a runtime-async method declaring Task or ValueTask: the
    // body leaves nothing on the stack at `ret`, exactly like a void method.
    //
    // This has static storage duration on purpose. A TypeSignature only borrows its blob, and the
    // one we hand back outlives this function by a long way - it is held across the whole of
    // TracerMethodRewriter::Rewrite. A function-local array would dangle.
    //
    // It is also a bare ELEMENT_TYPE_VOID with no PTR/PINNED/BYREF prefix, because
    // TypeSignature::GetElementTypeAndFlags ORs a flag per prefix and callers such as
    // CallTargetTokens::ModifyLocalSig compare the result to TypeFlagVoid for equality.
    constexpr COR_SIGNATURE kVoidReturnSignature[] = {ELEMENT_TYPE_VOID};

    // Task and Task`1 are classes, ValueTask and ValueTask`1 are structs, so a task-like return
    // always pairs one specific name with one specific element type. Matching on the pair rather
    // than on the name alone costs nothing and buys two things: a customer type that merely borrows
    // one of these names cannot be rewritten as though it were the real thing (it is declined,
    // which is the safe outcome), and a disagreement between the element type we parsed and the
    // name we resolved is caught rather than acted on.
    bool IsTaskLike(const shared::WSTRING& name, bool isGenericInst, bool isValueTypeShape)
    {
        if (isGenericInst)
        {
            return isValueTypeShape ? name == SystemThreadingTasksValueTaskGeneric
                                    : name == SystemThreadingTasksTaskGeneric;
        }

        return isValueTypeShape ? name == SystemThreadingTasksValueTask : name == SystemThreadingTasksTask;
    }
} // namespace

HRESULT ParseTaskLikeReturnShape(const TypeSignature& declared, mdToken& openTypeToken, bool& isGenericInst,
                                 bool& isValueTypeShape, TypeSignature& typeArg)
{
    openTypeToken = mdTokenNil;
    isGenericInst = false;
    isValueTypeShape = false;
    typeArg = {};

    if (declared.pbBase == nullptr || declared.length == 0)
    {
        return E_FAIL;
    }

    PCCOR_SIGNATURE const start = &declared.pbBase[declared.offset];
    PCCOR_SIGNATURE const end = start + declared.length;
    PCCOR_SIGNATURE pbCur = start;

    unsigned char elementType;
    if (!ParseByte(pbCur, end, &elementType))
    {
        return E_FAIL;
    }

    if (elementType == ELEMENT_TYPE_GENERICINST)
    {
        unsigned char genericElementType;
        if (!ParseByte(pbCur, end, &genericElementType))
        {
            return E_FAIL;
        }

        if (genericElementType != ELEMENT_TYPE_CLASS && genericElementType != ELEMENT_TYPE_VALUETYPE)
        {
            return E_FAIL;
        }

        // Using the unbounded CorSigUncompressToken overload because MethodSignature::TryParse has
        // already validated this blob, so the token cannot run past `end`; otherwise we should use
        // ParseTypeDefOrRefEncoded, which is bounds-checked like the ParseByte/ParseNumber above.
        const auto tokenLength = CorSigUncompressToken(pbCur, &openTypeToken);
        if (tokenLength == static_cast<ULONG>(-1))
        {
            return E_FAIL;
        }
        pbCur += tokenLength;

        unsigned genericArgCount = 0;
        if (!ParseNumber(pbCur, end, &genericArgCount))
        {
            return E_FAIL;
        }

        // Task`1 and ValueTask`1 take exactly one argument. Anything else is not a shape we know.
        if (genericArgCount != 1)
        {
            return E_FAIL;
        }

        PCCOR_SIGNATURE const typeArgStart = pbCur;
        if (!ParseType(pbCur, end))
        {
            return E_FAIL;
        }

        isGenericInst = true;
        isValueTypeShape = genericElementType == ELEMENT_TYPE_VALUETYPE;
        typeArg = TypeSignature{declared.offset + static_cast<ULONG>(typeArgStart - start),
                                static_cast<ULONG>(pbCur - typeArgStart), declared.pbBase};
        return S_OK;
    }

    if (elementType == ELEMENT_TYPE_CLASS || elementType == ELEMENT_TYPE_VALUETYPE)
    {
        // Unbounded overload; see the note on the GENERICINST branch above.
        const auto tokenLength = CorSigUncompressToken(pbCur, &openTypeToken);
        if (tokenLength == static_cast<ULONG>(-1))
        {
            return E_FAIL;
        }

        isValueTypeShape = elementType == ELEMENT_TYPE_VALUETYPE;
        return S_OK;
    }

    return E_FAIL;
}

HRESULT GetRuntimeAsyncEffectiveReturnType(const TypeSignature& declared,
                                           const ComPtr<IMetaDataImport2>& metadata_import, TypeSignature& effective)
{
    effective = {};

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = false;
    bool isValueTypeShape = false;
    TypeSignature typeArg{};

    const auto hr = ParseTaskLikeReturnShape(declared, openTypeToken, isGenericInst, isValueTypeShape, typeArg);
    if (FAILED(hr))
    {
        return hr;
    }

    const auto typeInfo = GetTypeInfo(metadata_import, openTypeToken);
    if (!typeInfo.IsValid())
    {
        return E_FAIL;
    }

    if (!IsTaskLike(typeInfo.name, isGenericInst, isValueTypeShape))
    {
        return E_FAIL;
    }

    // Task<T>/ValueTask<T> leave the type argument on the stack at `ret`; Task/ValueTask leave
    // nothing, exactly like a void method.
    effective = isGenericInst
                    ? typeArg
                    : TypeSignature{0, static_cast<ULONG>(sizeof(kVoidReturnSignature)), kVoidReturnSignature};
    return S_OK;
}

} // namespace trace
