// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#ifdef _WINDOWS
#include <wtypes.h>
#endif

#include <vector>

#include "RefCountingObject.h"
#include "StackFrameCodeKind.h"
#include "shared/src/native-src/string.h"

struct ManagedTypeInfo;

struct ManagedTypeInfoMutable
{
public:
    ManagedTypeInfoMutable();
    ~ManagedTypeInfoMutable();

    const ManagedTypeInfo* ConvertToNewImmutable(void);
    inline const shared::WSTRING* GetTypeName(void) const
    {
        return _typeName;
    }
    inline const shared::WSTRING* GetAssemblyName(void) const
    {
        return _assemblyName;
    }

    void CopyFrom(const ManagedTypeInfo& typeInfoImmutable);
    shared::WSTRING* GetTypeNameForModifying(void);
    void CreateNewTypeName(WCHAR* namespaceNameChars, std::int32_t namespaceCharCount, WCHAR* typeNameChars, std::int32_t typeCharCount);
    void AttachEnclosedTypeName(WCHAR* typeNameChars, std::int32_t typeCharCount);
    void UseTypeName(const shared::WSTRING* name);
    void CreateNewAssemblyName(WCHAR* nameChars, std::int32_t nameCharCount);
    void UseAssemblyName(const shared::WSTRING* name);

private:
    const shared::WSTRING* _typeName;
    const shared::WSTRING* _assemblyName;
};


struct ManagedTypeInfo : public RefCountingObject
{
public:
    ManagedTypeInfo(const shared::WSTRING* typeName, const shared::WSTRING* assemblyName) :
        _typeName{typeName}, _assemblyName{assemblyName}
    {
    }
    ManagedTypeInfo() = delete;
    ~ManagedTypeInfo() override;
    inline const shared::WSTRING* GetTypeName(void) const
    {
        return _typeName;
    }
    inline const shared::WSTRING* GetAssemblyName(void) const
    {
        return _assemblyName;
    }

private:
    const shared::WSTRING* _typeName;
    const shared::WSTRING* _assemblyName;
};


class StackFrameInfo : public RefCountingObject
{
public:
    explicit StackFrameInfo(const StackFrameCodeKind codeKind) :
        StackFrameInfo(codeKind, nullptr, nullptr, nullptr)
    {
    }

    ~StackFrameInfo() override;

private:
    StackFrameInfo(const StackFrameCodeKind codeKind,
                   const shared::WSTRING* functionName,
                   const ManagedTypeInfo* pContainingTypeInfo,
                   const shared::WSTRING** pManagedMethodFlags);

public:
    inline StackFrameCodeKind GetCodeKind(void) const
    {
        return _codeKind;
    }
    inline const shared::WSTRING* GetFunctionName(void) const
    {
        return _pFunctionName;
    }
    inline const shared::WSTRING** GetManagedMethodFlags(void) const
    {
        return _ppManagedMethodFlags;
    }
    const shared::WSTRING* GetContainingTypeName(void) const;
    const shared::WSTRING* GetContainingAssemblyName(void) const;
    shared::WSTRING* GetFunctionNameForModifying(void);
    void CreateNewFunctionName(WCHAR* nameChars, std::int32_t nameCharCount);
    void UseFunctionName(const shared::WSTRING* name);
    void SetContainingTypeInfo(const ManagedTypeInfo* pTypeInfo);
    void SetManagedMethodFlags(const std::vector<const shared::WSTRING*>& methodFlagMoikers);
    void ToDisplayString(shared::WSTRING* output) const;

private:
    StackFrameCodeKind _codeKind;
    const shared::WSTRING* _pFunctionName;
    const ManagedTypeInfo* _pContainingTypeInfo;
    const shared::WSTRING** _ppManagedMethodFlags;
};
