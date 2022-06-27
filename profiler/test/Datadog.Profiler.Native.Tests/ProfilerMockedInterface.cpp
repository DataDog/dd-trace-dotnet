#include "ProfilerMockedInterface.h"

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

std::vector<std::pair<std::string, std::string>> CreateCallstack(int depth)
{
    std::vector<std::pair<std::string, std::string>> result;

    for (auto i = 0; i < depth; i++)
    {
        std::ostringstream oss;
        oss << "frame_" << i;
        result.push_back(std::make_pair("module", oss.str()));
    }

    return result;
}
