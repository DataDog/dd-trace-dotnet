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

std::tuple<std::unique_ptr<ISsiManager>, MockSsiManager&> CreateSsiManager()
{
    std::unique_ptr<ISsiManager> manager = std::make_unique<MockSsiManager>();
    auto managerPtr = static_cast<MockSsiManager*>(manager.get());
    return {std::move(manager), *managerPtr};
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

std::shared_ptr<Sample> CreateSample(std::string_view runtimeId, const std::vector<std::pair<std::string, std::string>>& callstack, const std::vector<std::pair<std::string, std::string>>& labels, std::int64_t value)
{
    // For now sample contains only one value `value`.
    // If we change the number of values, do not forget to change this.
    Sample::ValuesCount = 1;
    auto sample = std::make_shared<Sample>(runtimeId);

    for (auto& [module, frame] : callstack)
    {
        sample->AddFrame({module, frame, "", 0});
    }

    for (auto const& [name, value] : labels)
    {
        sample->AddLabel(Label{name, value});
    }

    sample->SetValue(value);

    return sample;
}