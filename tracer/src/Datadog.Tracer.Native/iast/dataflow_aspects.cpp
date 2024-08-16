#include "iast_util.h"
#include "dataflow_aspects.h"
#include "module_info.h"
#include "method_info.h"
#include "dataflow_il_rewriter.h"
#include "dataflow.h"
#include "../cor_profiler.h"
#include "signature_info.h"
#include "dataflow_il_analysis.h"
#include "signature_info.h"
#include "signature_types.h"
#include "aspect_filter.h"
#include "aspect.h"

namespace iast
{
    bool IsReplace(AspectBehavior behavior)
    {
        return behavior == AspectBehavior::MethodReplace || behavior == AspectBehavior::CtorReplace;
    }
    AspectBehavior ParseAspectApplication(const WSTRING& subject)
    {
        if (subject == WStr("AspectCtorReplace")) { return AspectBehavior::CtorReplace; }
        if (subject == WStr("AspectMethodReplace")) { return AspectBehavior::MethodReplace; }
        if (subject == WStr("AspectMethodInsertAfter")) { return AspectBehavior::InsertAfter; }
        if (subject == WStr("AspectMethodInsertBefore")) { return AspectBehavior::InsertBefore; }
        return AspectBehavior::Unknown;
    }

    DataflowAspectFilterValue ParseAspectFilterValue(const WSTRING& filter)
    {
        if (filter == WStr("StringOptimization")) { return DataflowAspectFilterValue::StringOptimization; }
        if (filter == WStr("StringLiterals")) {return DataflowAspectFilterValue::StringLiterals; }
        if (filter == WStr("StringLiterals_Any")) { return DataflowAspectFilterValue::StringLiterals_Any; }
        if (filter == WStr("StringLiteral_0")) { return DataflowAspectFilterValue::StringLiteral_0; }
        if (filter == WStr("StringLiteral_1")) { return DataflowAspectFilterValue::StringLiteral_1; }
        return DataflowAspectFilterValue::None;
    }
    std::vector<DataflowAspectFilterValue> ParseAspectFilterValues(const WSTRING& filter)
    {
        std::vector<DataflowAspectFilterValue> res;
        auto parts = Split(TrimEnd(TrimStart(filter, WStr("[")), WStr("]")), WStr(","));
        for(auto part : parts)
        {
            res.push_back(ParseAspectFilterValue(Trim(part)));
        }
        return res;
    }

    //------------------------------------
    VersionInfo currentVersion = GetVersionInfo(GetDatadogVersion());

    DataflowAspectClass::DataflowAspectClass(Dataflow* dataflow, const WSTRING& aspectsAssembly, const WSTRING& line)
    {
        this->_dataflow = dataflow;
        this->_aspectsAssembly = aspectsAssembly;
        this->_line = line;
        size_t offset = 0;
        auto pos0 = IndexOf(line, WStr("[AspectClass("), &offset);
        if (pos0 == std::string::npos) { return; }
        pos0 = offset;
        auto pos1 = IndexOf(line, WStr(")] "), &offset);
        if (pos1 == std::string::npos) 
        {
            //Check for version limitation
            pos1 = IndexOf(line, WStr(");V"), &offset);
            if (pos1 == std::string::npos) { return; }
            auto pos2 = IndexOf(line, WStr("] "), &offset);
            if (pos2 == std::string::npos) { return; }
            auto versionTxt = shared::ToString(line.substr(pos1 + 3, pos2 - pos1 - 3));
            auto version = GetVersionInfo(versionTxt);
            if (Compare(currentVersion, version) < 0)
            {
                return; // Current version is lower than minimum required
            }
        }
        auto params = line.substr(pos0, pos1 - pos0);

        auto parts = SplitParams(params);
        int part = -1;
        if ((int)parts.size() > ++part) //Target Assemblies
        {
            _assemblies = Split(parts[part], WStr(","));
        }
        if ((int)parts.size() > ++part) // AspectFilter
        {
            _filters = ParseAspectFilterValues(parts[part]);
        }
        if ((int)parts.size() > ++part && parts[part] != WStr("DEFAULT")) // Aspect Type
        {
            _aspectType = ParseAspectType(shared::ToString(parts[part]));
        }
        if ((int)parts.size() > ++part && parts[part] != WStr("DEFAULT")) //VulnerabilityTypes
        {
            _vulnerabilityTypes = ParseVulnerabilityTypes(shared::ToString(parts[part]));
        }
        _aspectTypeName = Trim(line.substr(offset));
        _isValid = true;
    }

    bool DataflowAspectClass::IsValid()
    {
        return _isValid;
    }

    bool DataflowAspectClass::IsTargetModule(ModuleInfo* module)
    {
        return Contains(_assemblies, module->_name);
    }

    WSTRING DataflowAspectClass::ToString()
    {
        return _line;
    }

    //------------------------------------

    DataflowAspect::DataflowAspect(DataflowAspectClass* aspectClass, const WSTRING& line)
    {
        this->_aspectClass = aspectClass;
        this->_line = line;
        size_t offset = 0;
        auto pos0 = IndexOf(line, WStr("["), &offset);
        if (pos0 == std::string::npos) { return; }
        pos0 = offset;
        auto pos1 = IndexOf(line, WStr("("), &offset);
        if (pos1 == std::string::npos) { return; }
        auto aspectAttribute = line.substr(pos0, pos1 - pos0);
        _behavior = ParseAspectApplication(aspectAttribute);
        if (_behavior == AspectBehavior::Unknown) { return; }

        pos0 = offset;
        pos1 = IndexOf(line, WStr(")] "), &offset);
        if (pos1 == std::string::npos)
        {
            // Check for version limitation
            pos1 = IndexOf(line, WStr(");V"), &offset);
            if (pos1 == std::string::npos) { return; }
            auto pos2 = IndexOf(line, WStr("] "), &offset);
            if (pos2 == std::string::npos) { return; }
            auto versionTxt = shared::ToString(line.substr(pos1 + 3, pos2 - pos1 - 3));
            auto version = GetVersionInfo(versionTxt);
            if (Compare(currentVersion, version) < 0)
            {
                return; // Current version is lower than minimum required
            }
        }
        auto params = line.substr(pos0, pos1 - pos0);
        auto parts = SplitParams(params);

        int part = -1;
        if ((int)parts.size() > ++part) // TargetMethod
        {
            WSTRING assembliesPart;
            _targetMethod = parts[part];
            SplitType(_targetMethod, &assembliesPart, &_targetMethodType, &_targetMethodName, &_targetMethodParams);
            if (assembliesPart.length() > 0)
            {
                _targetMethodAssemblies = Split(assembliesPart, WStr(","));
            }
        }
        if ((int)parts.size() > ++part) // TargetType
        {
            WSTRING assembliesPart, targetParams;

            SplitType(parts[part], &assembliesPart, &_targetType);
            if (assembliesPart.length() > 0)
            {
                _targetTypeAssemblies = Split(assembliesPart, WStr(","));
            }
        }
        if ((int)parts.size() > ++part) // Param shift
        {
            _paramShift = ConvertToIntVector(parts[part]);
            if (_paramShift.size() == 0) { _paramShift.push_back(0); }
        }
        if ((int)parts.size() > ++part) // Box param
        {
            _boxParam = ConvertToBoolVector(parts[part]);
            if (_boxParam.size() == 0) { _boxParam.push_back(false); }
        }
        if ((int)parts.size() > ++part) // Skip if static string
        {
            _filters = ParseAspectFilterValues(parts[part]);
        }
        if ((int)parts.size() > ++part && parts[part] != WStr("DEFAULT")) // Aspect type
        {
            _aspectType = ParseAspectType(shared::ToString(parts[part]));
        }
        if ((int)parts.size() > ++part && parts[part] != WStr("DEFAULT")) // Vulnerability types
        {
            _vulnerabilityTypes = ParseVulnerabilityTypes(shared::ToString(parts[part]));
        }

        auto aspectMethod = Trim(line.substr(offset));
        pos0 = IndexOf(aspectMethod, WStr("("));
        _aspectMethodName = aspectMethod.substr(0, pos0);
        _aspectMethodParams = aspectMethod.substr(pos0);

        _isVirtual = _targetType.length() > 0 && _targetMethodType != _targetType;
        _isGeneric = Contains(_aspectMethodParams, WStr("!!"));
        _isValid = true;
    }

    bool DataflowAspect::IsValid()
    {
        return _isValid;
    }
    bool DataflowAspect::IsVirtual()
    {
        return _isVirtual;
    }
    bool DataflowAspect::IsGeneric()
    {
        return _isGeneric;
    }

    bool DataflowAspect::IsTargetModule(ModuleInfo* module)
    {
        if (Contains(_targetMethodAssemblies, module->_name)) { return true; }
        if (_aspectClass) { return _aspectClass->IsTargetModule(module); }
        return false;
    }

    AspectType DataflowAspect::GetAspectType()
    {
        if (_aspectType != AspectType::None) { return _aspectType; }
        return _aspectClass->_aspectType;
    }
    std::vector<VulnerabilityType> DataflowAspect::GetVulnerabilityTypes()
    {
        if (_vulnerabilityTypes.size() > 0) { return _vulnerabilityTypes; }
        return _aspectClass->_vulnerabilityTypes;
    }

    DataflowAspectReference* DataflowAspect::GetAspectReference(ModuleAspects* moduleAspects)
    {
        HRESULT hr = S_OK;
        //look for Target method in module refs
        auto module = moduleAspects->_module;
        mdTypeRef targetMethodTypeRef = 0; 
        mdMemberRef targetMethodRef = 0;
        mdTypeRef targetTypeRef = 0; 
        std::vector<mdTypeRef> paramTypeRefs; 
        std::vector<mdMemberRef> targetMethodRefCandidates;
        hr = module->FindTypeRefByName(_targetMethodType.c_str(), &targetMethodTypeRef);
        if (SUCCEEDED(hr))
        {
            module->FindMemberRefsByName(targetMethodTypeRef, _targetMethodName.c_str(), targetMethodRefCandidates);
        }
        else if (this->IsTargetModule(module))
        {
            hr = module->GetTypeDef(_targetMethodType.c_str(), &targetMethodTypeRef);
            if (SUCCEEDED(hr))
            {
                auto methods = module->GetMethods(targetMethodTypeRef, _targetMethodName.c_str());
                for (auto method : methods)
                {
                    targetMethodRefCandidates.push_back(method->GetMethodDef());
                }
            }
        }
        if (SUCCEEDED(hr))
        {
            MemberRefInfo* targetMemberRefInfo = nullptr;
            //Look for our method based upon the signature representation
            for (auto candidate : targetMethodRefCandidates)
            {
                if (auto memberRefInfo = module->GetMemberRefInfo(candidate))
                {
                    if (auto sig = memberRefInfo->GetSignature())
                    {
                        auto sigRepresentation = sig->GetParamsRepresentation();
                        if (sigRepresentation == _targetMethodParams)
                        {
                            targetMemberRefInfo = memberRefInfo;
                            targetMethodRef = candidate;
                            paramTypeRefs.clear();
                            for (unsigned int x = 0; x < _paramShift.size(); x++)
                            {
                                int paramTypeRef = 0;
                                if (_boxParam[x])
                                {
                                    int paramIndex = ((int)sig->_params.size() - (_paramShift[x] + 1));
                                    if (paramIndex >= 0)
                                    {
                                        paramTypeRef = sig->_params[paramIndex]->GetToken();
                                    }
                                    else // Instance param (first, but not counted in signature)
                                    {
                                        paramTypeRef = memberRefInfo->GetTypeDef();
                                    }
                                }
                                paramTypeRefs.push_back(paramTypeRef);
                            }
                            break;
                        }
                    }
                }
            }

            if (targetMethodRef != 0 && _isVirtual)
            {
                //Look for virtual target typeRef
                if (FAILED(module->FindTypeRefByName(_targetType.c_str(), &targetTypeRef)))
                {
                    targetMethodRef = 0;
                }
            }

            if (targetMethodRef != 0)
            {
                return new DataflowAspectReference(moduleAspects, this, targetMethodRef, targetTypeRef, paramTypeRefs);
            }
        }
        return nullptr;
    }

    WSTRING DataflowAspect::ToString()
    {
        return _line;
    }

    //------------------------------

    DataflowAspectReference::DataflowAspectReference(ModuleAspects* moduleAspects, DataflowAspect* aspect, mdMemberRef method, mdTypeRef type, const std::vector<mdTypeRef>& paramType)
    {
        this->_moduleAspects = moduleAspects;
        this->_module = moduleAspects->_module;
        this->_aspect = aspect;
        this->_targetMethodRef = method;
        this->_targetTypeRef = type;
        this->_targetParamTypeToken = paramType;
        if (this->_targetParamTypeToken.size() == 0) { this->_targetParamTypeToken.push_back(0); }

        std::vector<DataflowAspectFilterValue> filterValues;
        AddRange(filterValues, aspect->_aspectClass->_filters);
        AddRange(filterValues, aspect->_filters);
        for (auto filterValue : filterValues)
        {
            auto filter = moduleAspects->GetFilter(filterValue);
            if (filter == nullptr)
            {
                continue;
            }
            this->_filters.push_back(filter);
        }
    }

    std::string DataflowAspectReference::GetAspectTypeName()
    {
        return shared::ToString(_aspect->_aspectClass->_aspectTypeName);
    }
    std::string DataflowAspectReference::GetAspectMethodName()
    {
        return shared::ToString(_aspect->_aspectMethodName);
    }
    AspectType DataflowAspectReference::GetAspectType()
    {
        return _aspect->GetAspectType();
    }
    std::vector<VulnerabilityType> DataflowAspectReference::GetVulnerabilityTypes()
    {
        return _aspect->GetVulnerabilityTypes();
    }

    mdToken DataflowAspectReference::GetAspectMemberRef(MethodSpec* methodSpec)
    {
        if (_aspectMemberRef == 0)
        {
            // Import aspect
            _aspectMemberRef = _module->DefineMemberRef(_aspect->_aspectClass->_aspectsAssembly,
                                                        _aspect->_aspectClass->_aspectTypeName,
                                                        _aspect->_aspectMethodName, _aspect->_aspectMethodParams);
        }

        if (_aspect->IsGeneric() && _aspectMemberRef != 0 && methodSpec != nullptr)
        {
            // Retrieve aspect method spec
            auto it = _aspectMethodSpecs.find(methodSpec->GetMethodSpecId());
            if (it == _aspectMethodSpecs.end())
            {
                // Create the MethodSpec
                auto aspectMethodSpec = _module->DefineMethodSpec(_aspectMemberRef, methodSpec->GetMethodSpecSignature());
                _aspectMethodSpecs[methodSpec->GetMethodSpecId()] = aspectMethodSpec;
                return aspectMethodSpec;
            }

            return it->second;
        }

        return _aspectMemberRef;
    }

    struct InstructionProcessInfo
    {
        InstructionProcessInfo(ILInstr* instruction, int paramIndex, AspectBehavior behavior)
        {
            this->instruction = instruction;
            this->paramIndex = paramIndex;
            this->behavior = behavior;
        }
        ILInstr* instruction;
        int paramIndex;
        AspectBehavior behavior;
    };

    bool DataflowAspectReference::ApplyFilters(DataflowContext& context)
    {
        static bool aspectFilterEnabled = true; //HdivConfig::Instance.GetEnabled("hdiv.net.ast.profiler.aspect.filter.enabled"_W, true);
        if (aspectFilterEnabled && _filters.size() > 0)
        {
            for (auto filter : _filters)
            {
                if (!filter->AllowInstruction(context))
                {
                    return false;
                }
            }
        }
        return true;
    }

    bool DataflowAspectReference::IsReinstrumentation(mdMemberRef method)
    {
        return _aspectMemberRef == method;
    }
    bool DataflowAspectReference::IsTargetMethod(mdMemberRef method)
    {
        return _targetMethodRef == method;
    }

    bool DataflowAspectReference::Apply(DataflowContext& context)
    {
        ILRewriter* processor = context.rewriter;
        ILInstr* instruction = context.instruction;
        mdMemberRef operand = instruction->m_Arg32;
        auto method = processor->GetMethodInfo();
        auto module = method->GetModuleInfo();
        if (IsReinstrumentation(operand) && method->IsWritten())
        {
            context.aborted = true;
            return true;
        }


        //Check if we must process this instruction (usually a call or newObj)
        bool process = false;
        std::vector<InstructionProcessInfo> instructionsToProcess;

        MethodSpec* methodSpec = nullptr; 
        if (TypeFromToken(operand) == mdtMethodSpec && !IsTargetMethod(operand))
        {
            methodSpec = module->GetMethodSpec(operand);
            if (methodSpec != nullptr && methodSpec->GetGenericMethod() != nullptr)
            {
                operand = methodSpec->GetGenericMethod()->GetMemberId();
            }
        }
        if (IsTargetMethod(operand))
        {
            //Aditional filter conditions
            process = true;
            if (instruction->m_opcode == CEE_CALLVIRT && instruction->m_pPrev->m_opcode == CEE_CONSTRAINED)
            {
                process = false;
            }
            else if (_aspect->IsVirtual())
            {
                auto c = processor->StackAnalysis()->GetUnresolvedInstructionsCount();
                process = false;
                auto thisInstructions = processor->StackAnalysis()->LocateCallParamInstructions(instruction, 0); //Get first param (this)
                if (thisInstructions.size() > 0)
                {
                    auto thisInstruction = thisInstructions.front();
                    auto type = thisInstruction->InferTypeToken();
                    if (module->AreSameTypes(type, _targetTypeRef)) //Disambiguation for on the fly imported methods
                    {
                        process = true;
                    }
                }
            }
            if (process)
            {
                process = ApplyFilters(context);
            }
            if (process)
            {
                for (unsigned int x = 0; x < _aspect->_paramShift.size(); x++)
                {
                    if (IsReplace(_aspect->_behavior) || _aspect->_paramShift[x] == 0)
                    {
                        instructionsToProcess.push_back(InstructionProcessInfo(instruction, x, _aspect->_behavior));
                    }
                    else if (!IsReplace(_aspect->_behavior) && _aspect->_paramShift[x] > 0)
                    {
                        //Locate call param insertion points
                        auto instructionInfo = processor->StackAnalysis()->GetInstruction(instruction);
                        auto methodSig = instructionInfo->GetArgumentSignature();
                        int paramCount = methodSig->GetEffectiveParamCount();
                        for (auto iInfo : processor->StackAnalysis()->LocateCallParamInstructions(instruction, paramCount - _aspect->_paramShift[x] - 1)) //Locate param load instruction
                        {
                            instructionsToProcess.push_back(InstructionProcessInfo(iInfo->_instruction, x, AspectBehavior::InsertAfter)); //Insert after the target param load always
                        }
                        if (instructionsToProcess.size() == 0)
                        {
                            trace::Logger::Info("Param instruction not found");
                        }
                    }
                }
            }
        }

        if (instructionsToProcess.size() > 0)
        {
            for (auto instructionToProcess : instructionsToProcess)
            {
                ILInstr* aspectInstruction = nullptr;

                //Replace call function with aspect

                mdToken memberRef = GetAspectMemberRef(methodSpec);
                if (memberRef == 0) { continue; } //Disabled Spot
                if (instructionToProcess.behavior == AspectBehavior::InsertBefore)
                {
                    aspectInstruction = processor->NewILInstr(CEE_CALL, memberRef, true);
                    processor->InsertBefore(instructionToProcess.instruction, aspectInstruction);
                }
                else if (instructionToProcess.behavior == AspectBehavior::InsertAfter)
                {
                    aspectInstruction = processor->NewILInstr(CEE_CALL, memberRef, true);
                    auto inserted = processor->InsertAfter(instructionToProcess.instruction, aspectInstruction);

                    if (_aspect->_boxParam[instructionToProcess.paramIndex])
                    {
                        // Retrieve type of the valueType in target argument
                        auto paramType = _targetParamTypeToken[instructionToProcess.paramIndex];
                        processor->InsertBefore(aspectInstruction, processor->NewILInstr(CEE_BOX, paramType, true));
                        inserted = processor->InsertAfter(aspectInstruction, processor->NewILInstr(CEE_UNBOX_ANY, paramType, true));
                    }

                    if (instructionToProcess.instruction == instruction)
                    {
                        context.instruction = inserted;
                    }
                }
                else //Replace
                {
                    if (_aspect->_paramShift.size() > 0)
                    {
                        for (unsigned int x = 0; x < _aspect->_paramShift.size(); x++)
                        {
                            if (!_aspect->_boxParam[x])
                            {
                                continue;
                            }

                            auto instructionInfo = processor->StackAnalysis()->GetInstruction(instructionToProcess.instruction);
                            auto methodSig = instructionInfo->GetArgumentSignature();
                            int paramCount = methodSig->GetEffectiveParamCount();
                            for (auto iInfo : processor->StackAnalysis()->LocateCallParamInstructions(
                                     instruction,
                                     paramCount - _aspect->_paramShift[x] - 1)) // Locate param load instruction
                            {
                                auto paramType = _targetParamTypeToken[x];
                                processor->InsertAfter(iInfo->_instruction, processor->NewILInstr(CEE_BOX, paramType, true));

                                // Figure out if param is byref
                                if (iInfo->IsArgument())
                                {
                                    auto sig = method->GetSignature();
                                    auto param = sig->_params[iInfo->_instruction->m_Arg32];
                                    if (param->IsByRef())
                                    {
                                        processor->InsertAfter(iInfo->_instruction, processor->NewILInstr(CEE_LDOBJ, paramType, true));
                                    }
                                }

                                iInfo->ConvertToNonAddressLoad();
                            }
                        }
                    }

                    aspectInstruction = instructionToProcess.instruction;
                    instructionToProcess.instruction->m_opcode = CEE_CALL;
                    instructionToProcess.instruction->m_Arg32 = memberRef;
                }
            }
        }

        return process;
    }
}
