#include "IntegrationLoader.h"

using json = nlohmann::json;

std::vector<integration> IntegrationLoader::load_integrations_from_file(const std::wstring& file_path)
{
    std::vector<integration> integrations;

    try
    {
        std::ifstream stream;
        stream.open(file_path);
        integrations = load_integrations_from_stream(stream);
        stream.close();
    }
    catch (...)
    {
        LOG_APPEND(L"failed to load integrations");
    }

    return integrations;
}

std::vector<integration> IntegrationLoader::load_integrations_from_stream(std::istream& stream)
{
    std::vector<integration> integrations;

    try
    {
        json j;
        // parse the stream
        stream >> j;

        for (auto& el : j)
        {
            auto i = IntegrationLoader::integration_from_json(el);
            if (i.second)
            {
                integrations.push_back(i.first);
            }
        }

        LOG_APPEND(L"loaded integrations: " << j.dump().c_str());
    }
    catch (const json::parse_error& e)
    {
        LOG_APPEND(L"invalid integrations: " << e.what());
    }
    catch (const json::type_error& e)
    {
        LOG_APPEND(L"invalid integrations: " << e.what());
    }
    catch (...)
    {
        LOG_APPEND(L"failed to load integrations");
    }

    return integrations;
}

std::pair<integration, bool> IntegrationLoader::integration_from_json(const json::value_type& src)
{
    if (!src.is_object())
    {
        return { {}, false };
    }

    // first get the name, which is required
    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
    std::wstring name = converter.from_bytes(src.value("name", ""));
    if (name.empty())
    {
        LOG_APPEND(L"integration name is missing for integration: " << src.dump().c_str());
        return { {}, false };
    }

    std::vector<method_replacement> replacements;
    auto arr = src.value("method_replacements", json::array());
    if (arr.is_array())
    {
        for (auto& el : arr)
        {
            auto mr = method_replacement_from_json(el);
            if (mr.second)
            {
                replacements.push_back(mr.first);
            }
        }
    }
    return { integration(IntegrationType_Custom, name, replacements), true };
}


std::pair<method_replacement, bool> IntegrationLoader::method_replacement_from_json(const json::value_type& src)
{
    if (!src.is_object())
    {
        return { {}, false };
    }

    auto caller = IntegrationLoader::method_reference_from_json(src["caller"]);
    auto target = IntegrationLoader::method_reference_from_json(src["target"]);
    auto wrapper = IntegrationLoader::method_reference_from_json(src["wrapper"]);
    return { method_replacement(caller, target, wrapper), true };
}

method_reference IntegrationLoader::method_reference_from_json(const json::value_type& src)
{
    if (!src.is_object())
    {
        return {};
    }

    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
    std::wstring assembly = converter.from_bytes(src.value("assembly", ""));
    std::wstring type = converter.from_bytes(src.value("type", ""));
    std::wstring method = converter.from_bytes(src.value("method", ""));
    return method_reference(assembly, type, method, {});
}