#pragma once
#include <atomic>
#include "../../../../shared/src/native-src/pal.h"
#include <map>
#include <set>
using namespace shared;

namespace iast
{
    class ModuleInfo;
    class SignatureInfo;
    class ILRewriter;
    class FunctionControlWrapper;
    class Aspect;
    class SpotInfo;

    class TypeInfo
    {
    public:
        TypeInfo(ModuleInfo* pModuleInfo, mdTypeDef typeDef);
        virtual ~TypeInfo();
    protected:
        ModuleInfo* _module;
        WSTRING _name = EmptyWStr;
        mdTypeDef _id = 0;
    public:
        mdTypeDef GetTypeDef();
        WSTRING& GetName();
    };

    class MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    public:
        MemberRefInfo(ModuleInfo* pModuleInfo, mdMemberRef memberRef);
        virtual ~MemberRefInfo();
    protected:
        ModuleInfo* _module = nullptr;
        WSTRING _name = EmptyWStr;

        mdTypeDef _typeDef = 0;
        TypeInfo* _typeInfo = nullptr;
        mdMemberRef _id = 0;
        DWORD _methodAttributes = 0;
        SignatureInfo* _pSignature = nullptr;
        PCCOR_SIGNATURE _pSig = nullptr;
        ULONG _nSig = 0;
    public:
        mdMemberRef GetMemberId();

        TypeInfo* GetTypeInfo();
        mdTypeDef GetTypeDef();

        ModuleInfo* GetModuleInfo();
        WSTRING& GetName();
        WSTRING GetFullName();
        WSTRING GetFullyQualifiedName();
        WSTRING& GetTypeName();
        virtual SignatureInfo* GetSignature();
        ULONG GetParameterCount();
        CorElementType GetReturnCorType();
        virtual std::vector<WSTRING> GetCustomAttributes();
    };

    class MethodSpec : public MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    protected:
        MemberRefInfo* genericMethod;
    public:
        MethodSpec(ModuleInfo* pModuleInfo, mdMethodSpec methodSpec);

        SignatureInfo* GetSignature() override;
        MemberRefInfo* GetGenericMethod();

        mdMethodSpec GetMethodSpecId();
        SignatureInfo* GetMethodSpecSignature();
    };

    class FieldInfo : public MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    protected:
        DWORD _attributes;
    public:
        FieldInfo(ModuleInfo* pModuleInfo, mdFieldDef fieldDef);

        mdFieldDef GetFieldDef();
    };

    class PropertyInfo : public MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    protected:
        mdMethodDef _getter;
        mdMethodDef _setter;
    public:
        PropertyInfo(ModuleInfo* pModuleInfo, mdProperty mdProperty);

        inline mdProperty GetPropertyId() { return _id; }
        inline mdMethodDef GetGetterId() { return _getter; }
        inline mdMethodDef GetSetterId() { return _setter; }
    };

    class MethodInfo : public MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    private:
        DWORD _methodAttributes;

        bool _isExcluded = false;
        bool _isProcessed = false;
        bool _allowRestoreOnSecondJit = false;
        bool _disableInlining = false;
        bool _isWritten = false;
        bool _isInstrumented = false;

        LPCBYTE _pOriginalMehodIL = nullptr;
        ULONG _nOriginalMehodIL = 0;
        LPBYTE _pMethodIL = nullptr;
        ULONG _nMethodIL = 0;

    protected:
        ILRewriter* _rewriter = nullptr;
    private:
        void FreeBuffer();
    public:
        MethodInfo(ModuleInfo* pModuleInfo, mdMethodDef methodDef);
        ~MethodInfo() override;

        WSTRING GetKey(FunctionID functionID = 0);
        mdMethodDef GetMethodDef();

        bool IsExcluded();
        bool IsProcessed();
        void SetProcessed();
        bool IsInstrumented();
        void SetInstrumented(bool instrumented);
        bool HasChanges();
        bool IsWritten();
        void DisableRestoreOnSecondJit();
        bool IsInlineEnabled();
        void DisableInlining();

        HRESULT GetILRewriter(ILRewriter** rewriter, ICorProfilerInfo* pCorProfilerInfo = nullptr);
        HRESULT CommitILRewriter(bool abort = false);
        HRESULT GetMethodIL(LPCBYTE* ppMehodIL, ULONG* pnSize, bool original = false);
        
        HRESULT SetMethodIL(ULONG nSize, LPCBYTE pMehodIL, ICorProfilerFunctionControl* pFunctionControl = nullptr);
        HRESULT ApplyFinalInstrumentation(ICorProfilerFunctionControl* pFunctionControl = nullptr);

        void ReJITCompilationStarted();
        void ReJITCompilationFinished();

        bool IsPropertyAccessor();
        std::vector<WSTRING> GetCustomAttributes() override;
    };
}