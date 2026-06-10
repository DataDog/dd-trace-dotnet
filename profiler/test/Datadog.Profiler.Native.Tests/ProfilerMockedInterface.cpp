#include "ProfilerMockedInterface.h"

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
        sample->AddLabel(StringLabel{name, value});
    }

    sample->SetValue(value);

    return sample;
}