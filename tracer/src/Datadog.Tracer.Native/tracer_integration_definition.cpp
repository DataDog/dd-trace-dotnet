#include "tracer_integration_definition.h"
#include "logger.h"

#include "../../../shared/src/native-src/util.h"

namespace trace
{

std::vector<IntegrationDefinition>
GetIntegrationsFromTraceMethodsConfiguration(const TypeReference& integration_type,
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
                integration_type, false, false, false));

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
} // namespace trace