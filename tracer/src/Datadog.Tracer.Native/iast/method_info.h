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
        WSTRING GetName();
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
        WSTRING _fullName = EmptyWStr;
        WSTRING _fullNameWithReturnType = EmptyWStr;
        WSTRING _memberName = EmptyWStr;

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
        WSTRING GetName();
        WSTRING GetMemberName();
        WSTRING GetFullName(bool includeReturnType = false);
        WSTRING GetTypeName();
        virtual SignatureInfo* GetSignature();
        ULONG GetParameterCount();
        CorElementType GetReturnCorType();

    private:
        std::atomic<int> _fullNameCounterLock;
    };

    class MethodSpec : public MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    protected:
        MemberRefInfo* genericMethod;
    public:
        MethodSpec(ModuleInfo* pModuleInfo, mdMethodSpec methodSpec);

        mdMethodSpec GetMethodSpecId();
        SignatureInfo* GetSignature() override;
        MemberRefInfo* GetGenericMethod();
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

    class MethodInfo : public MemberRefInfo
    {
        friend class ILRewriter;
        friend class ModuleInfo;
    private:
        DWORD _methodAttributes;

        int _isExcluded = -1;
        bool _isProcessed = false;
        bool _allowRestoreOnSecondJit = false;
        bool _disableInlining = false;
        bool _isWritten = false;

        LPCBYTE _pOriginalMehodIL = nullptr;
        DWORD _nOriginalMehodIL = 0;
        LPBYTE _pMethodIL = nullptr;
        DWORD _nMethodIL = 0;

    protected:
        ILRewriter* _rewriter = nullptr;
        std::string _applyMessage = "";
    private:
        // LPBYTE AllocBuffer(DWORD size);
        void FreeBuffer();
    public:
        MethodInfo(ModuleInfo* pModuleInfo, mdMethodDef methodDef);
        ~MethodInfo() override;

        WSTRING GetMethodName();
        WSTRING GetKey(FunctionID functionID = 0);
        mdMethodDef GetMethodDef();

        bool IsExcluded();
        bool IsProcessed();
        void SetProcessed();
        bool HasChanged();
        bool IsWritten();
        void DisableRestoreOnSecondJit();
        bool IsInlineEnabled();
        void DisableInlining();

        HRESULT GetILRewriter(ILRewriter** rewriter);
        HRESULT CommitILRewriter(const std::string& applyMessage = "");
        HRESULT GetMethodIL(LPCBYTE* ppMehodIL, ULONG* pnSize, bool original = false);
        
        HRESULT SetMethodIL(ULONG nSize, LPCBYTE pMehodIL, ICorProfilerFunctionControl* pFunctionControl = nullptr);
        HRESULT ApplyFinalInstrumentation(ICorProfilerFunctionControl* pFunctionControl = nullptr);

        void ReJITCompilationStarted();
        void ReJITCompilationFinished();

        void DumpIL(std::string message = "", ULONG pnMethodIL = 0, LPCBYTE pMethodIL = nullptr);
    };
}