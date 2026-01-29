// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "DebugInfoStore.h"
#include "ProfilerMockedInterface.h"

#include "shared/src/native-src/dd_filesystem.hpp"
#include "shared/src/native-src/string.h"

#ifdef _WINDOWS
#include "..\Datadog.Profiler.Native.Windows\SymPdbParser.h"
#include <metahost.h>
#include <atlbase.h>
#endif

using ::testing::Return;
using ::testing::ReturnRef;

namespace
{
    // Helper function to get the current process executable path (cross-platform)
    bool GetCurrentProcessPath(std::string& outPath)
    {
#ifdef _WINDOWS
        char buffer[MAX_PATH];
        DWORD len = GetModuleFileNameA(nullptr, buffer, MAX_PATH);
        if (len == 0 || len == MAX_PATH)
        {
            return false;
        }
        outPath = std::string(buffer);
#else
        char buffer[PATH_MAX];
        ssize_t len = readlink("/proc/self/exe", buffer, sizeof(buffer) - 1);
        if (len == -1)
        {
            return false;
        }
        buffer[len] = '\0';
        outPath = std::string(buffer);
#endif
        return true;
    }

    // Helper function to build path to a sample PDB based on process location
    // Returns the PDB path and module path (via out parameter)
    fs::path GetSamplePdbPath(const std::string& sampleName, const std::string& targetFramework, fs::path& outModulePath)
    {
        std::string processPath;
        if (!GetCurrentProcessPath(processPath))
        {
            return fs::path();
        }

        // Build path relative to the process location: ../../src/Demos/{sampleName}/{targetFramework}/{sampleName}.pdb
        fs::path pdbPath = fs::path(processPath).parent_path() / ".." / ".." / "src" / "Demos" / sampleName / targetFramework / (sampleName + ".pdb");

        // Module is in the same directory with .exe extension
        outModulePath = pdbPath.parent_path() / (sampleName + ".exe");

        return pdbPath;
    }
} // anonymous namespace

#ifdef _WINDOWS

// This test validates that SymPdbParser can parse symbols from a .NET Framework PDB (Windows PDB format).
// .NET Framework (net48) produces Windows PDB files that cannot be parsed as Portable PDBs.
TEST(DebugInfoStoreTest, ParseModuleDebugInfo_NetFramework)
{
    // Get paths to the sample PDB and module
    fs::path modulePath;
    fs::path pdbPath = GetSamplePdbPath("Samples.Computer01", "net48", modulePath);

    if (pdbPath.empty())
    {
        GTEST_SKIP() << "Failed to get current process path";
        return;
    }

    std::error_code ec;
    if (!fs::exists(pdbPath, ec) || !fs::exists(modulePath, ec))
    {
        GTEST_SKIP() << "Samples.Computer01.pdb (net48) not found. Build the Samples.Computer01 project for net48 first.";
        return;
    }

    // Create a minimal mock configuration
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsDebugInfoEnabled()).WillRepeatedly(Return(true));

    // Initialize COM
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    bool comInitialized = SUCCEEDED(hr);

    // Get IMetaDataImport from the module file for Windows PDB parsing
    CComPtr<ICLRMetaHost> pMetaHost;
    hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (void**)&pMetaHost);
    ASSERT_TRUE(SUCCEEDED(hr)) << "Failed to create CLRMetaHost";

    // Get .NET Framework 4.0 runtime for metadata access
    CComPtr<ICLRRuntimeInfo> pRuntimeInfo;
    hr = pMetaHost->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, (void**)&pRuntimeInfo);
    ASSERT_TRUE(SUCCEEDED(hr)) << "Failed to get .NET Framework 4.0 runtime";

    CComPtr<IMetaDataDispenser> pMetaDataDispenser;
    hr = pRuntimeInfo->GetInterface(CLSID_CorMetaDataDispenser, IID_IMetaDataDispenser, (void**)&pMetaDataDispenser);
    ASSERT_TRUE(SUCCEEDED(hr)) << "Failed to get IMetaDataDispenser";

    // Convert module path to wide string
    int len = MultiByteToWideChar(CP_UTF8, 0, modulePath.string().c_str(), -1, nullptr, 0);
    std::wstring wModulePath(len, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, modulePath.string().c_str(), -1, &wModulePath[0], len);

    // Open the module to get metadata
    CComPtr<IMetaDataImport> pMetaDataImport;
    hr = pMetaDataDispenser->OpenScope(wModulePath.c_str(), ofRead, IID_IMetaDataImport, (IUnknown**)&pMetaDataImport);
    ASSERT_TRUE(SUCCEEDED(hr)) << "Failed to open module metadata";

    // Parse the PDB
    ModuleDebugInfo moduleInfo;
    SymParser parser(pMetaDataImport, &moduleInfo);
    bool success = parser.LoadPdbFile(pdbPath.string(), modulePath.string());

    // COM cleanup is automatic via CComPtr
    if (comInitialized)
    {
        CoUninitialize();
    }

    // Validate that symbols were loaded
    // For .NET Framework PDB (Windows PDB format), the LoadingState should be Windows
    ASSERT_EQ(moduleInfo.LoadingState, SymbolLoadingState::Windows)
        << "Expected Windows PDB format for .NET Framework compilation (net48)";

    // Validate that debug info was populated
    ASSERT_FALSE(moduleInfo.Files.empty()) << "Expected at least one source file in debug info";
    ASSERT_FALSE(moduleInfo.RidToDebugInfo.empty()) << "Expected RID to debug info mapping for Windows PDB";

    // The first entry should be the empty string placeholder
    ASSERT_EQ(moduleInfo.Files[0], DebugInfoStore::NoFileFound);

    // Validate that we have actual source files loaded
    ASSERT_GT(moduleInfo.Files.size(), 1) << "Expected more than just the placeholder entry";

    // At least one file should reference a .cs source file
    bool foundCsFile = false;
    for (const auto& file : moduleInfo.Files)
    {
        if (file.find(".cs") != std::string::npos)
        {
            foundCsFile = true;
            break;
        }
    }
    ASSERT_TRUE(foundCsFile) << "Expected at least one .cs source file in debug info";

    // Validate that we have RID mappings
    ASSERT_GT(moduleInfo.RidToDebugInfo.size(), 1) << "Expected RID to debug info mappings";

    // Validate that at least some entries have valid line numbers
    bool foundValidLineNumber = false;
    for (const auto& debugInfo : moduleInfo.RidToDebugInfo)
    {
        if (debugInfo.StartLine > 0 && debugInfo.File != DebugInfoStore::NoFileFound)
        {
            foundValidLineNumber = true;
            break;
        }
    }
    ASSERT_TRUE(foundValidLineNumber) << "Expected at least one RVA with valid line number and source file";
}

#endif // _WINDOWS


// Additional test to verify that .NET Core/5+ PDBs are Portable format
TEST(DebugInfoStoreTest, ParseModuleDebugInfo_NetCorePortable)
{
    // Get paths to the sample PDB and module
    fs::path modulePath;
    fs::path pdbPath = GetSamplePdbPath("Samples.BuggyBits", "net10.0", modulePath);
    if (pdbPath.empty())
    {
        GTEST_SKIP() << "Failed to get current process path";
        return;
    }

    std::error_code ec;
    if (!fs::exists(pdbPath, ec) || !fs::exists(modulePath, ec))
    {
        GTEST_SKIP() << "Samples.BuggyBits.pdb (net10.0) not found. This is expected if net10.0 is not compiled.";
        return;
    }

    ModuleDebugInfo moduleInfo;

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsDebugInfoEnabled()).WillRepeatedly(Return(true));

    DebugInfoStore debugInfoStore(nullptr, configuration.get());

    // For Portable PDB, we don't need IMetaDataImport, so pass 0 as moduleId
    debugInfoStore.ParseModuleDebugInfo(0, pdbPath.string(), modulePath.string(), moduleInfo);

    // For .NET Core/5+ PDB (Portable PDB format), the LoadingState should be Portable
    ASSERT_EQ(moduleInfo.LoadingState, SymbolLoadingState::Portable)
        << "Expected Portable PDB format for .NET Core/5+ compilation (net10.0)";

    // Portable PDBs use RID-based lookup
    ASSERT_FALSE(moduleInfo.RidToDebugInfo.empty()) << "Expected RID to debug info mapping for Portable PDB";
    ASSERT_FALSE(moduleInfo.Files.empty()) << "Expected source files in debug info";
}
