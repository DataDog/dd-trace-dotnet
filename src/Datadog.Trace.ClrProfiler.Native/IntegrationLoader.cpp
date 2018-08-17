#include "IntegrationLoader.h"

using json = nlohmann::json;

std::vector<integration> IntegrationLoader::load_integrations_from_file(const std::wstring file_path)
{
    std::vector<integration> integrations;

    try
    {
        json j;

        // pipe in the file path
        std::ifstream i;
        i.open(file_path);
        i >> j;
        i.close();

        for (auto& el : j)
        {
            auto i = IntegrationLoader::integration_from_json(el);
            if (i.second)
            {
                integrations.push_back(i.first);
            }
        }

        LOG_APPEND(L"loaded integrations (" << file_path.c_str() << L"): " << j.dump().c_str());
    }
    catch (const json::parse_error& e)
    {
        LOG_APPEND(L"invalid integration file (" << file_path.c_str() << L"): " << e.what());
    }
    catch (const json::type_error& e)
    {
        LOG_APPEND(L"invalid integration file (" << file_path.c_str() << L"): " << e.what());
    }
    catch (...)
    {
        LOG_APPEND(L"failed to load integration file (" << file_path.c_str() << L")");
    }

    return integrations;
}

std::pair<integration, bool> IntegrationLoader::integration_from_json(const json::value_type& src)
{
    if (!src.is_object())
    {
        return { {}, false };
    }

    std::vector<method_replacement> replacements;
    if (src["method_replacements"].is_array())
    {
        for (auto& el : src["method_replacements"])
        {
            auto mr = method_replacement_from_json(el);
            if (mr.second)
            {
                replacements.push_back(mr.first);
            }
        }
    }
    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
    std::wstring name = converter.from_bytes(src.value("name", ""));
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