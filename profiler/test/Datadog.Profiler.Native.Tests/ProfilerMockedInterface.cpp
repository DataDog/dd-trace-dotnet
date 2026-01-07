#include "ProfilerMockedInterface.h"
#include "SymbolsStore.h"
#include <sstream>
#include "ServiceWrapper.hpp"

std::tuple<std::unique_ptr<IConfiguration>, MockConfiguration&> CreateConfiguration()
{
    std::unique_ptr<IConfiguration> configuration = std::make_unique<MockConfiguration>();
    auto configurationPtr = static_cast<MockConfiguration*>(configuration.get());

    return {std::move(configuration), *configurationPtr};
}

std::tuple<std::shared_ptr<ISamplesProvider>, MockSampleProvider&> CreateSamplesProvider()
{
    std::shared_ptr<ISamplesProvider> samplesProvider = std::make_shared<MockSampleProvider>();
    auto samplesProviderPtr = static_cast<MockSampleProvider*>(samplesProvider.get());
    return {samplesProvider, *samplesProviderPtr};
}

std::tuple<std::unique_ptr<IExporter>, MockExporter&> CreateExporter()
{
    std::unique_ptr<IExporter> exporter = std::make_unique<MockExporter>();
    auto exporterPtr = static_cast<MockExporter*>(exporter.get());

    return {std::move(exporter), *exporterPtr};
}

std::tuple<std::unique_ptr<ISamplesCollector>, MockSamplesCollector&> CreateSamplesCollector()
{
    std::unique_ptr<ISamplesCollector> collector = std::make_unique<MockSamplesCollector>();
    auto collectorPtr = static_cast<MockSamplesCollector*>(collector.get());

    return {std::move(collector), *collectorPtr};
}

std::tuple<std::unique_ptr<ISsiManager>, MockSsiManager&> CreateSsiManager()
{
    std::unique_ptr<ISsiManager> manager = std::make_unique<MockSsiManager>();
    auto managerPtr = static_cast<MockSsiManager*>(manager.get());
    return {std::move(manager), *managerPtr};
}

std::vector<std::pair<ddog_prof_FunctionId2, ddog_prof_MappingId2>> CreateCallstack(int depth, libdatadog::SymbolsStore* symbolsStore)
{
    std::vector<std::pair<ddog_prof_FunctionId2, ddog_prof_MappingId2>> result;

    for (auto i = 0; i < depth; i++)
    {
        std::ostringstream oss;
        oss << "frame_" << i;
        result.push_back(std::make_pair(symbolsStore->InternFunction(oss.str(), "").value(), symbolsStore->InternMapping(oss.str()).value()));
    }

    return result;
}

std::shared_ptr<Sample> CreateSample(libdatadog::SymbolsStore* symbolsStore, std::string_view runtimeId, const std::vector<std::pair<ddog_prof_FunctionId2, ddog_prof_MappingId2>>& callstack, const std::vector<std::pair<ddog_prof_StringId2, std::string>>& labels, std::int64_t value)
{
    // For now sample contains only one value `value`.
    // If we change the number of values, do not forget to change this.
    Sample::ValuesCount = 1;
    auto sample = std::make_shared<Sample>(runtimeId, symbolsStore);

    for (auto& [frame, module] : callstack)
    {
        sample->AddFrame({module, frame, 0});
    }

    for (auto const& [name, value] : labels)
    {
        sample->AddLabel(StringLabel{name, value});
    }

    sample->SetValue(value);

    return sample;
}