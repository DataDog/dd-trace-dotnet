
#include "integration.h"
#include "logger.h"

#ifdef _WIN32
#include <regex>
#else
#include <re2/re2.h>
#endif
#include <sstream>

#include <unordered_map>
#include <mutex>
#include "../../../shared/src/native-src/util.h"

namespace trace
{

std::mutex m_assemblyReferenceCacheMutex;
std::unordered_map<shared::WSTRING, std::unique_ptr<AssemblyReference>> m_assemblyReferenceCache;


AssemblyReference::AssemblyReference(const shared::WSTRING& str) :
    name(GetNameFromAssemblyReferenceString(str)),
    version(GetVersionFromAssemblyReferenceString(str)),
    locale(GetLocaleFromAssemblyReferenceString(str)),
    public_key(GetPublicKeyFromAssemblyReferenceString(str))
{
}

AssemblyReference* AssemblyReference::GetFromCache(const shared::WSTRING& str)
{
    std::lock_guard<std::mutex> guard(m_assemblyReferenceCacheMutex);
    auto findRes = m_assemblyReferenceCache.find(str);
    if (findRes != m_assemblyReferenceCache.end())
    {
        return findRes->second.get();
    }
    AssemblyReference* aref = new AssemblyReference(str);
    m_assemblyReferenceCache[str] = std::unique_ptr<AssemblyReference>(aref);
    return aref;
}

std::vector<IntegrationDefinition> GetIntegrationsFromTraceMethodsConfiguration(const TypeReference integration_type,
                                                                                const shared::WSTRING& configuration_string)
{
    std::vector<IntegrationDefinition> integrationDefinitions;

    auto dd_trace_methods_type = shared::Split(configuration_string, ';');

    for (const shared::WSTRING& trace_method_type : dd_trace_methods_type)
    {
        shared::WSTRING type_name = shared::EmptyWStr;
        shared::WSTRING method_definitions = shared::EmptyWStr;

        // [] are required. Is either "*" or comma-separated values
        // We don't know the assembly name, only the type name
        auto firstOpenBracket = trace_method_type.find_first_of('[');
        if (firstOpenBracket != std::string::npos)
        {
            auto firstCloseBracket = trace_method_type.find_first_of(']', firstOpenBracket + 1);
            auto secondOpenBracket = trace_method_type.find_first_of('[', firstOpenBracket + 1);
            if (firstCloseBracket != std::string::npos &&
                (secondOpenBracket == std::string::npos || firstCloseBracket < secondOpenBracket))
            {
                auto length = firstCloseBracket - firstOpenBracket - 1;
                method_definitions = trace_method_type.substr(firstOpenBracket + 1, length);
            }
        }

        if (method_definitions.empty())
        {
            continue;
        }

        type_name = trace_method_type.substr(0, firstOpenBracket);
        auto method_definitions_array = shared::Split(method_definitions, ',');
        for (const shared::WSTRING& method_definition : method_definitions_array)
        {
            std::vector<shared::WSTRING> signatureTypes;
            integrationDefinitions.push_back(IntegrationDefinition(
                MethodReference(tracemethodintegration_assemblyname, type_name, method_definition, Version(0, 0, 0, 0),
                                Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX), signatureTypes),
                integration_type, false, false));

            if (Logger::IsDebugEnabled())
            {
                if (method_definition == tracemethodintegration_wildcardmethodname)
                {
                    Logger::Debug("GetIntegrationsFromTraceMethodsConfiguration:  * Target: ", type_name,
                                    ".* -- All methods except .ctor, .cctor, Equals, Finalize, GetHashCode, ToString,"
                                    " and property getters/setters will automatically be instrumented.");
                }
                else
                {
                    Logger::Debug("GetIntegrationsFromTraceMethodsConfiguration:  * Target: ", type_name, ".",
                                    method_definition, "(", signatureTypes.size(), ")");
                }
            }
        }
    }

    return integrationDefinitions;
}

namespace
{

    shared::WSTRING GetNameFromAssemblyReferenceString(const shared::WSTRING& wstr)
    {
        shared::WSTRING name = wstr;

        auto pos = name.find(WStr(','));
        if (pos != shared::WSTRING::npos)
        {
            name = name.substr(0, pos);
        }

        // strip spaces
        pos = name.rfind(WStr(' '));
        if (pos != shared::WSTRING::npos)
        {
            name = name.substr(0, pos);
        }

        return name;
    }

    Version GetVersionFromAssemblyReferenceString(const shared::WSTRING& str)
    {
        unsigned short major = 0;
        unsigned short minor = 0;
        unsigned short build = 0;
        unsigned short revision = 0;

        if (str.empty())
        {
            return {major, minor, build, revision};
        }

#ifdef _WIN32

        static auto re = std::wregex(WStr("Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)"));

        std::wsmatch match;
        if (std::regex_search(str, match, re) && match.size() == 5)
        {
            shared::WSTRINGSTREAM(match.str(1)) >> major;
            shared::WSTRINGSTREAM(match.str(2)) >> minor;
            shared::WSTRINGSTREAM(match.str(3)) >> build;
            shared::WSTRINGSTREAM(match.str(4)) >> revision;
        }

#else

        static re2::RE2 re("Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)", RE2::Quiet);
        re2::RE2::PartialMatch(shared::ToString(str), re, &major, &minor, &build, &revision);

#endif

        return {major, minor, build, revision};
    }

    shared::WSTRING GetLocaleFromAssemblyReferenceString(const shared::WSTRING& str)
    {
        shared::WSTRING locale = WStr("neutral");

        if (str.empty())
        {
            return locale;
        }

#ifdef _WIN32

        static auto re = std::wregex(WStr("Culture=([a-zA-Z0-9]+)"));
        std::wsmatch match;
        if (std::regex_search(str, match, re) && match.size() == 2)
        {
            locale = match.str(1);
        }

#else

        static re2::RE2 re("Culture=([a-zA-Z0-9]+)", RE2::Quiet);

        std::string match;
        if (re2::RE2::PartialMatch(shared::ToString(str), re, &match))
        {
            locale = shared::ToWSTRING(match);
        }

#endif

        return locale;
    }

    PublicKey GetPublicKeyFromAssemblyReferenceString(const shared::WSTRING& str)
    {
        BYTE data[8] = {0};

        if (str.empty())
        {
            return PublicKey(data);
        }

#ifdef _WIN32

        static auto re = std::wregex(WStr("PublicKeyToken=([a-fA-F0-9]{16})"));
        std::wsmatch match;
        if (std::regex_search(str, match, re) && match.size() == 2)
        {
            for (int i = 0; i < 8; i++)
            {
                auto s = match.str(1).substr(i * 2, 2);
                unsigned long x;
                shared::WSTRINGSTREAM(s) >> std::hex >> x;
                data[i] = BYTE(x);
            }
        }

#else

        static re2::RE2 re("PublicKeyToken=([a-fA-F0-9]{16})");
        std::string match;
        if (re2::RE2::PartialMatch(shared::ToString(str), re, &match))
        {
            for (int i = 0; i < 8; i++)
            {
                auto s = match.substr(i * 2, 2);
                unsigned long x;
                std::stringstream(s) >> std::hex >> x;
                data[i] = BYTE(x);
            }
        }

#endif

        return PublicKey(data);
    }

} // namespace

} // namespace trace
