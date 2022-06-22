#pragma once
#include "../../../../shared/src/native-src/dd_filesystem.hpp"
#include "../cor_profiler.h"
#include "../log.h"
#include "../util.h"
#include "instrumented_assembly_generator_consts.h"
#include "method_info.h"
#include <fstream>
#include <iomanip>
#include <random>
#include <utility>
#if !_WIN32
#include <unistd.h>
#endif

namespace instrumented_assembly_generator
{
template <typename T>
shared::WSTRING IntToHex(T i)
{
    std::stringstream stream;
    stream << "0x" << std::setfill('0') << std::setw(sizeof(T) * 2) << std::hex << i;
    return shared::ToWSTRING(stream.str());
}

inline fs::path instrumentedAssemblyGeneratorOutputFolder;

inline std::string AnsiStringFromGuid(const GUID& guid)
{
#ifdef _WIN32
    wchar_t szGuidW[40] = {0};
    char szGuidA[40] = {0};
    if (!StringFromGUID2(guid, szGuidW, 40))
    {
        Log::Warn("AnsiStringFromGuid: Failed in StringFromGUID2.");
    }
    WideCharToMultiByte(CP_ACP, 0, szGuidW, -1, szGuidA, 40, nullptr, nullptr);
    return szGuidA;
#else
    throw std::logic_error("InstrumentationVerification on a non-Windows OS not yet implemented");
#endif
}

// Helper method that removes all illegal chars from a file name
// https://stackoverflow.com/a/31976060
inline shared::WSTRING GetCleanedFileName(const shared::WSTRING& fileName)
{
    shared::WSTRING retFileName;
#if _WIN32
    const shared::WSTRING illegalChars = WStr("\\/:?\"<>|*");
#else
    const shared::WSTRING illegalChars = WStr("/");
#endif

    for (const wchar_t charInFileName : fileName)
    {
        const auto isIllegalChar = illegalChars.find(charInFileName) != shared::WSTRING::npos;
        if (!isIllegalChar)
        {
            retFileName += charInFileName;
        }
        else
        {
            constexpr auto replaceChar = WStr("%");
            retFileName += replaceChar;
        }
    }
    return shared::Trim(retFileName);
}

inline void CreateNecessaryFolders(const fs::path& baseInstrumentationFolder)
{
    fs::create_directories(baseInstrumentationFolder);
    fs::create_directories(baseInstrumentationFolder / OriginalModulesFolder);
    fs::create_directories(baseInstrumentationFolder / InstrumentedAssemblyGeneratorInputFolder);
}

inline fs::path GetInstrumentedAssemblyGeneratorCurrentProcessFolder()
{
    // todo: When to delete the previous output?
    try
    {
        if (!instrumentedAssemblyGeneratorOutputFolder.empty()) return instrumentedAssemblyGeneratorOutputFolder;

        const auto logsDirectory =
            fs::path(shared::GetDatadogLogFilePath<Log::NativeLoaderLoggerPolicy>("not_in_use")).parent_path();
        const auto instrumentedAssemblyGeneratorDir = logsDirectory / InstrumentedAssemblyGeneratorLogsFolder;

        const auto processName = shared::GetCurrentProcessName();
        const auto processId = shared::GetPID();
        if (!processName.empty() && processId > 0)
        {
            const auto time = shared::GetProcessStartTime();
            static auto processWithoutExtension = processName.substr(0, processName.find_last_of(L'.'));
            const auto fullName = processWithoutExtension + WStr("_") + shared::ToWSTRING(processId) + WStr("_") + time;

            const auto cleanedDirectoryName = GetCleanedFileName(fullName);
            const auto instrumentedAssemblyGeneratorAppDir = instrumentedAssemblyGeneratorDir / cleanedDirectoryName;
            CreateNecessaryFolders(instrumentedAssemblyGeneratorAppDir);
            instrumentedAssemblyGeneratorOutputFolder = instrumentedAssemblyGeneratorAppDir;
        }
    }
    catch (const std::exception& e)
    {
        Log::Error("GetInstrumentedAssemblyGeneratorCurrentProcessFolder: failed to get instrumented log path. ",
                   e.what());
    }
    catch (...)
    {
        Log::Error("GetInstrumentedAssemblyGeneratorCurrentProcessFolder: failed to get instrumented log path.");
    }
    return instrumentedAssemblyGeneratorOutputFolder;
}

// Check if we should use the instrumented assembly generator to generate files
inline bool IsInstrumentedAssemblyGeneratorEnabled()
{
    try
    {
        const auto isInstrumentedAssemblyGeneratorEnabled =
            shared::GetEnvironmentValue(cfg_instrumentation_verification_env);
        if (isInstrumentedAssemblyGeneratorEnabled == WStr("1") ||
            isInstrumentedAssemblyGeneratorEnabled == WStr("true"))
        {
#if _WIN32
            if (const auto path = GetInstrumentedAssemblyGeneratorCurrentProcessFolder(); !path.empty())
            {
                Log::Info("Instrumentation Verification log is enabled. Output folder is: ", path);
                return true;
            }
#else
            Log::Warn("Instrumentation Verification is currently only supported on Windows and will be disabled.");
#endif
        }
    }
    catch (...)
    {
    }
    return false;
}

inline std::tuple<HRESULT, shared::WSTRING, shared::WSTRING>
GetModuleNameAndMvid(const ComPtr<IMetaDataImport>& metadataImport)
{
    ULONG pchName;
    GUID pmvid;
    WCHAR szName[path_length_limit];
    auto hr = metadataImport->GetScopeProps(szName, path_length_limit, &pchName, &pmvid);
    if (FAILED(hr))
    {
        return {hr, nullptr, nullptr};
    }
    shared::WSTRING name = szName;
    shared::WSTRING mvid = shared::ToWSTRING(AnsiStringFromGuid(pmvid));
    return {hr, name, mvid};
}

inline bool ShouldSkipCopyModule(const shared::WSTRING& moduleName)
{
    return shared::WStringStartWithCaseInsensitive(moduleName, WStr("RefEmit_"));
}

inline void CopyOriginalModuleForInstrumentationVerification(const shared::WSTRING& modulePath)
{
    const fs::path toFolder = GetInstrumentedAssemblyGeneratorCurrentProcessFolder() / fs::path(OriginalModulesFolder);

    const auto fileName = fs::path(modulePath).filename();

    if (ShouldSkipCopyModule(shared::ToWSTRING(fileName.string())) == false)
    {
        try
        {
            copy_file(modulePath, toFolder / fileName);
        }
        catch (const std::exception& e)
        {
            Log::Error("CopyOriginalModuleForInstrumentationVerification: failed to copy module ", fileName, " to ",
                       toFolder, " Error: ", e.what());
        }
        catch (...)
        {
            Log::Error("CopyOriginalModuleForInstrumentationVerification: failed to copy module ", fileName, " to ",
                       toFolder);
        }
    }
}

inline void WriteTextToFile(const shared::WSTRING& fileName, const shared::WSTRING& stringStream)
{
    try
    {
        const auto instrumentedLogsDir = GetInstrumentedAssemblyGeneratorCurrentProcessFolder();
        const auto inputFolder = instrumentedLogsDir / InstrumentedAssemblyGeneratorInputFolder;
        std::basic_ofstream<WCHAR> outStream;
        outStream.exceptions(std::ofstream::badbit);
        outStream.open(inputFolder / fileName, std::ios::out | std::ios_base::app);
        outStream << stringStream;
        outStream.close();
    }
    catch (const std::ofstream::failure& e)
    {
        Log::Error("WriteTextToFile: failed to write text to a file: ", fileName, " - Error: ", e.what());
    }
    catch (...)
    {
        Log::Error("WriteTextToFile: failed to write text to a file: ", fileName);
    }
}

inline void WriteBytesToFile(const shared::WSTRING& fileName, const unsigned char* buffer, const ULONG size)
{
    try
    {
        const auto instrumentedLogsDir = GetInstrumentedAssemblyGeneratorCurrentProcessFolder();
        const auto inputFolder = instrumentedLogsDir / InstrumentedAssemblyGeneratorInputFolder;
        std::ofstream outBinStream;
        outBinStream.exceptions(std::ofstream::badbit);
        outBinStream.open(inputFolder / fileName, std::ios::out | std::ios::binary);
        outBinStream.write((char*) buffer, size);
        outBinStream.close();
    }
    catch (std::ofstream::failure& e)
    {
        Log::Error("WriteBytesToFile: failed to write bytes to file, full path was: ", fileName,
                   " - Error: ", e.what());
    }
    catch (...)
    {
        Log::Error("WriteBytesToFile: failed to write bytes to file, full path was: ", fileName);
    }
}

inline shared::WSTRING GetLocalsTypes(const ComPtr<IMetaDataImport>& metadataImport, LPCBYTE pbNewILMethodHeader)
{
    PCCOR_SIGNATURE localSig{nullptr};
    ULONG localSigLength;
    mdSignature localVarSigToken = 0;
    auto constexpr bitHeaderFlavor = 7;
    auto constexpr fatHeaderFlag = 3;
    auto constexpr tinyHeaderFlag = 2;
    auto constexpr dnlibTinyHeaderFlag = 6;
    // I don't know why dnlib use this value, it's not in the spec but maybe there is a reason.
    // actually in the spec this what they wrote "These 2 bits (2&3) will be one and only one of the following"
    auto const flag = *pbNewILMethodHeader & bitHeaderFlavor;
    if (flag == tinyHeaderFlag || flag == dnlibTinyHeaderFlag)
    {
        return shared::EmptyWStr;
    }
    if (flag == fatHeaderFlag)
    {
        pbNewILMethodHeader += 8;
        std::memcpy(&localVarSigToken, pbNewILMethodHeader, sizeof(mdSignature));
    }
    if (localVarSigToken == 0)
    {
        return shared::EmptyWStr;
    }

    metadataImport->GetSigFromToken(localVarSigToken, &localSig, &localSigLength);
    const auto localsSig = MemberSignature(localSig, localSigLength, 0);
    return localsSig.LocalsToString(metadataImport);
}

inline HRESULT WriteILChanges(ModuleID moduleId, mdMethodDef methodToken, LPCBYTE pbNewILMethodHeader, ULONG ilSize,
                              ICorProfilerInfo12* corProfilerInfo)
{
    HRESULT hr;
    try
    {
        ComPtr<IUnknown> metadataInterfaces;
        IfFailRet(corProfilerInfo->GetModuleMetaData(moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport,
                                                     metadataInterfaces.GetAddressOf()));

        auto metadataImport = metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);

        auto methodAndTypeInfo = MethodInfo::GetMethodInfo(metadataImport, methodToken);
        if (methodAndTypeInfo.token == 0 || !methodAndTypeInfo.methodSig.IsValid())
        {
            Log::Warn("InstrumentationVerificationCorProfilerInfo::WriteILChanges: fail in GetMethodInfo. Token or "
                      "methodSig is not valid. Method token is: ",
                      methodToken);
            return E_FAIL;
        }

        auto [result, moduleName, mvid] = GetModuleNameAndMvid(metadataImport);
        IfFailRet(result);

        auto const locals = GetLocalsTypes(metadataImport, pbNewILMethodHeader);

        shared::WSTRINGSTREAM headerFileStream;
        headerFileStream << mvid << FileNameSeparator << std::hex << methodAndTypeInfo.typeToken << FileNameSeparator
                         << std::hex << methodAndTypeInfo.token << FileNameSeparator << moduleName << FileNameSeparator
                         << methodAndTypeInfo.typeName << FileNameSeparator << methodAndTypeInfo.name
                         << FileNameSeparator << methodAndTypeInfo.methodSig.ReturnTypeName(metadataImport)
                         << FileNameSeparator << methodAndTypeInfo.methodSig.ArgumentsNames(metadataImport)
                         << FileNameSeparator << locals << FileNameSeparator << methodAndTypeInfo.methodSig.HasThis()
                         << std::endl;

        shared::WSTRINGSTREAM fileNameStream;
        fileNameStream << mvid << FileNameSeparator << std::hex << methodAndTypeInfo.typeToken << FileNameSeparator
                       << std::hex << methodAndTypeInfo.token << FileNameSeparator
                       << GetCleanedFileName(methodAndTypeInfo.name) << InstrumentedLogFileExtension;
        if (ilSize == 0)
        {
            hr = corProfilerInfo->GetILFunctionBody(moduleId, methodToken, &pbNewILMethodHeader, &ilSize);
            if (FAILED(hr) || ilSize == 0)
            {
                Log::Error("InstrumentationVerificationCorProfilerInfo::WriteILChanges: failed to get IL size. Token: ",
                           methodToken);
                return E_FAIL;
            }
        }

        WriteBytesToFile(BinaryFilePrefix + fileNameStream.str(), pbNewILMethodHeader, ilSize);
        WriteTextToFile(TextFilePrefix + fileNameStream.str(), headerFileStream.str());
    }
    catch (const std::exception& exception)
    {
        Log::Error("WriteILChanges: fail to write IL to disk. Token: ", methodToken, " Error: ", exception.what());
    }
    catch (...)
    {
        Log::Error("WriteILChanges: fail to write IL to disk. Token: ", methodToken);
    }
    return hr;
}

} // namespace instrumented_assembly_generator
