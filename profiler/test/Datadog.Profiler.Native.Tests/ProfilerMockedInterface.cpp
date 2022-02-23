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


Sample CreateSample(std::initializer_list<std::pair<std::string, std::string>> callstack, std::initializer_list<std::pair<std::string, std::string>> labels, std::int64_t value)
{
    Sample sample{};

    for (auto const& frame : callstack)
    {
        sample.AddFrame(frame.first, frame.second);
    }

    for (auto const& [name, value] : labels)
    {
        sample.AddLabel({name, value});
    }

    sample.SetValue(value);

    return sample;
}