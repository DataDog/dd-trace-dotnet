#pragma once
#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include <filesystem>

#include "Configuration.h"
#include "IExporter.h"
#include "ISamplesProvider.h"
#include "Sample.h"
#include "TagsHelper.h"


namespace fs = std::filesystem;

class MockConfiguration : public IConfiguration
{
public:
    ~MockConfiguration() override = default;
    MOCK_METHOD(bool, IsDebugLogEnabled, (), (const override));
    MOCK_METHOD(fs::path const&, GetLogDirectory, (), (const override));
    MOCK_METHOD(fs::path const&, GetProfilesOutputDirectory, (), (const override));
    MOCK_METHOD(bool, IsNativeFramesEnabled, (), (const override));
    MOCK_METHOD(bool, IsOperationalMetricsEnabled, (), (const override));
    MOCK_METHOD(std::chrono::seconds, GetUploadInterval, (), (const override));
    MOCK_METHOD(tags const&, GetUserTags, (), (const override));
    MOCK_METHOD(std::string const&, GetVersion, (), (const override));     // return DD_VERSION, Unspecified-Version if not specified
    MOCK_METHOD(std::string const&, GetEnvironment, (), (const override)); // return DD_ENV, Unspecified-Env if not specified
    MOCK_METHOD(std::string const&, GetHostname, (), (const override));        // return the machine hostname
    MOCK_METHOD(std::string const&, GetAgentUrl, (), (const override));
    MOCK_METHOD(std::string const&, GetAgentHost, (), (const override));
    MOCK_METHOD(int, GetAgentPort, (), (const override));
    MOCK_METHOD(std::string const&, GetSite, (), (const override));
    MOCK_METHOD(std::string const&, GetApiKey, (), (const override));
    MOCK_METHOD(std::string const&, GetServiceName, (), (const override));
    MOCK_METHOD(bool, IsFFLibddprofEnabled, (), (const override));
    MOCK_METHOD(bool, IsAgentless, (), (const override));
};

class MockExporter : public IExporter
{
public:
    MOCK_METHOD(void, Add, (Sample const& sample), (override));
    MOCK_METHOD(void, Export, (), (override));
};

class MockSampleProvider : public ISamplesProvider
{
public:
    MOCK_METHOD(std::list<Sample>, GetSamples, (), (override));
};

template <typename T, typename U, typename... Args>
std::pair<std::unique_ptr<T>, U&> CreateMockForUniquePtr(Args... args)
{
    std::unique_ptr<T> interf = std::make_unique<U>(args...);
    auto mock = static_cast<U*>(interf.get());
    return {std::move(interf), *mock};
}

std::tuple<std::unique_ptr<IConfiguration>, MockConfiguration&> CreateConfiguration();

std::tuple<std::shared_ptr<ISamplesProvider>, MockSampleProvider&> CreateSamplesProvider();

std::tuple<std::unique_ptr<IExporter>, MockExporter&> CreateExporter();

Sample CreateSample(std::initializer_list<std::pair<std::string, std::string>> callstack, std::initializer_list<std::pair<std::string, std::string>> labels, std::int64_t value);