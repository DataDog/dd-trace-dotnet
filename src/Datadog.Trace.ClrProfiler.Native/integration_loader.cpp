#include "integration_loader.h"

#include <exception>
#include <stdexcept>

#include "environment_variables.h"
#include "logger.h"
#include "util.h"

namespace trace
{

using json = nlohmann::json;

void LoadIntegrationsFromEnvironment(std::vector<IntegrationMethod>& integrationMethods, const bool isCallTargetEnabled,
                                     const bool isNetstandardEnabled,
                                     const std::vector<WSTRING>& disabledIntegrationNames)
{
    for (const WSTRING& filePath : GetEnvironmentValues(environment::integrations_path))
    {
        Logger::Debug("Loading integrations from file: ", filePath);
        LoadIntegrationsFromFile(filePath, integrationMethods, isCallTargetEnabled, isNetstandardEnabled,
                                 disabledIntegrationNames);
    }
}

void LoadIntegrationsFromFile(const WSTRING& file_path, std::vector<IntegrationMethod>& integrationMethods,
                              const bool isCallTargetEnabled, const bool isNetstandardEnabled,
                              const std::vector<WSTRING>& disabledIntegrationNames)
{
    try
    {
        std::ifstream stream(ToString(file_path));

        if (static_cast<bool>(stream))
        {
            LoadIntegrationsFromStream(stream, integrationMethods, isCallTargetEnabled, isNetstandardEnabled,
                                       disabledIntegrationNames);
        }
        else
        {
            Logger::Warn("Failed to load integrations from file ", file_path);
        }

        stream.close();
    }
    catch (...)
    {
        auto ex = std::current_exception();
        try
        {
            if (ex)
            {
                std::rethrow_exception(ex);
            }
        }
        catch (const std::exception& ex)
        {
            Logger::Warn("Failed to load integrations: ", ex.what());
        }
    }
}

void LoadIntegrationsFromStream(std::istream& stream, std::vector<IntegrationMethod>& integrationMethods,
                                const bool isCallTargetEnabled, const bool isNetstandardEnabled,
                                const std::vector<WSTRING>& disabledIntegrationNames)
{
    try
    {
        json j;
        // parse the stream
        stream >> j;

        integrationMethods.reserve(j.size());

        for (const auto& el : j)
        {
            IntegrationFromJson(el, integrationMethods, isCallTargetEnabled, isNetstandardEnabled,
                                disabledIntegrationNames);
        }

    }
    catch (const json::parse_error& e)
    {
        Logger::Warn("Invalid integrations:", e.what());
    }
    catch (const json::type_error& e)
    {
        Logger::Warn("Invalid integrations:", e.what());
    }
    catch (...)
    {
        auto ex = std::current_exception();
        try
        {
            if (ex)
            {
                std::rethrow_exception(ex);
            }
        }
        catch (const std::exception& ex)
        {
            Logger::Warn("Failed to load integrations: ", ex.what());
        }
    }
}

namespace
{

    void IntegrationFromJson(const json::value_type& src, std::vector<IntegrationMethod>& integrationMethods,
                             const bool isCallTargetEnabled, const bool isNetstandardEnabled,
                             const std::vector<WSTRING>& disabledIntegrationNames)
    {
        if (!src.is_object())
        {
            return;
        }

        // first get the name, which is required
        const WSTRING name = ToWSTRING(src.value("name", ""));
        if (name.empty())
        {
            Logger::Warn("Integration name is missing for integration: ", src.dump());
            return;
        }

        // check if the integration is disabled
        for (const WSTRING& disabledName : disabledIntegrationNames)
        {
            if (name == disabledName)
            {
                return;
            }
        }

        auto arr = src.value("method_replacements", json::array());
        if (arr.is_array())
        {
            for (const auto& el : arr)
            {
                MethodReplacementFromJson(el, name, integrationMethods, isCallTargetEnabled, isNetstandardEnabled);
            }
        }
    }

    void MethodReplacementFromJson(const json::value_type& src, const WSTRING& integrationName,
                                   std::vector<IntegrationMethod>& integrationMethods,
                                   const bool isCallTargetEnabled, const bool isNetstandardEnabled)
    {
        if (src.is_object())
        {
            const auto wrapperAction = ToWSTRING(src.value("wrapper", json::object()).value("action", ""));
            if (isCallTargetEnabled && wrapperAction != WStr("CallTargetModification"))
            {
                return;
            }

            if (isCallTargetEnabled)
            {
                const MethodReference wrapper =
                    MethodReferenceFromJson(src.value("wrapper", json::object()), false, true);

                const MethodReference target =
                    MethodReferenceFromJson(src.value("target", json::object()), true, false);

                integrationMethods.push_back({integrationName, {{}, target, wrapper}});
            }
            else
            {
                const MethodReference target =
                    MethodReferenceFromJson(src.value("target", json::object()), true, false);

                // temporarily skip the calls into netstandard.dll that were added in
                // https://github.com/DataDog/dd-trace-dotnet/pull/753.
                // users can opt-in to the additional instrumentation by setting environment
                // variable DD_TRACE_NETSTANDARD_ENABLED
                if (!isNetstandardEnabled && target.assembly.name == WStr("netstandard"))
                {
                    return;
                }

                const MethodReference wrapper =
                    MethodReferenceFromJson(src.value("wrapper", json::object()), false, true);

                const MethodReference caller =
                    MethodReferenceFromJson(src.value("caller", json::object()), false, false);

                integrationMethods.push_back({integrationName, {caller, target, wrapper}});
            }
        }
    }

    MethodReference MethodReferenceFromJson(const json::value_type& src, const bool is_target_method,
                                            const bool is_wrapper_method)
    {
        if (!src.is_object())
        {
            return {};
        }

        const auto assembly = ToWSTRING(src.value("assembly", ""));
        const auto type = ToWSTRING(src.value("type", ""));
        const auto method = ToWSTRING(src.value("method", ""));
        auto raw_signature = src.value("signature", json::array());

        const auto eoj = src.end();
        USHORT min_major = 0;
        USHORT min_minor = 0;
        USHORT min_patch = 0;
        USHORT max_major = USHRT_MAX;
        USHORT max_minor = USHRT_MAX;
        USHORT max_patch = USHRT_MAX;
        std::vector<WSTRING> signature_type_array;
        WSTRING action = EmptyWStr;

        if (is_target_method)
        {
            // these fields only exist in the target definition

            if (src.find("minimum_major") != eoj)
            {
                min_major = src["minimum_major"].get<USHORT>();
            }
            if (src.find("minimum_minor") != eoj)
            {
                min_minor = src["minimum_minor"].get<USHORT>();
            }
            if (src.find("minimum_patch") != eoj)
            {
                min_patch = src["minimum_patch"].get<USHORT>();
            }
            if (src.find("maximum_major") != eoj)
            {
                max_major = src["maximum_major"].get<USHORT>();
            }
            if (src.find("maximum_minor") != eoj)
            {
                max_minor = src["maximum_minor"].get<USHORT>();
            }
            if (src.find("maximum_patch") != eoj)
            {
                max_patch = src["maximum_patch"].get<USHORT>();
            }

            if (src.find("signature_types") != eoj)
            {
                // c++ is unable to handle null values in this array
                // we would need to write out own parsing here for null values
                auto sig_types = src["signature_types"].get<std::vector<std::string>>();
                signature_type_array = std::vector<WSTRING>(sig_types.size());
                for (auto i = sig_types.size() - 1; i < sig_types.size(); i--)
                {
                    signature_type_array[i] = ToWSTRING(sig_types[i]);
                }
            }
        }
        else if (is_wrapper_method)
        {
            action = ToWSTRING(src.value("action", ""));
        }

        std::vector<BYTE> signature;
        if (raw_signature.is_array())
        {
            for (auto& el : raw_signature)
            {
                if (el.is_number_unsigned())
                {
                    signature.push_back(BYTE(el.get<BYTE>()));
                }
            }
        }
        else if (raw_signature.is_string())
        {
            // load as a hex string
            std::string str = raw_signature;
            bool flip = false;
            char prev = 0;
            for (auto& c : str)
            {
                BYTE b = 0;
                if ('0' <= c && c <= '9')
                {
                    b = c - '0';
                }
                else if ('a' <= c && c <= 'f')
                {
                    b = c - 'a' + 10;
                }
                else if ('A' <= c && c <= 'F')
                {
                    b = c - 'A' + 10;
                }
                else
                {
                    // skip any non-hex character
                    continue;
                }
                if (flip)
                {
                    signature.push_back((prev << 4) + b);
                }
                flip = !flip;
                prev = b;
            }
        }
        return MethodReference(assembly, type, method, action, Version(min_major, min_minor, min_patch, 0),
                               Version(max_major, max_minor, max_patch, USHRT_MAX), signature, signature_type_array);
    }

} // namespace

} // namespace trace
