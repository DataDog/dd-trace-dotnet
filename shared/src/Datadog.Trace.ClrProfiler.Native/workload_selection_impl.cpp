#include "workload_selection_impl.h"

#include "../../../shared/src/native-src/string.h"
#include "log.h"
#include "util.h"

#include <optional>
extern "C"
{
#include <dd/policies/error_codes.h>
#include <stdio.h>
    void _except_handler4_common(void)
    {
    }
}

namespace datadog::shared::nativeloader
{
namespace
{
    enum class InjectionStatus : uint8_t
    {
        ALLOW,
        DENY,
        UNKNOWN
    };

    InjectionStatus injection_status;

    std::optional<plcs_errors> evaluatePolicies()
    {
        const auto policies_file = GetPoliciesPath();

        FILE* file = NULL;
        fopen_s(&file, policies_file.string().c_str(), "rb");
        if (!file)
        {
            return PLCS_ENO_DATA;
        }

        fseek(file, 0, SEEK_END);
        const auto file_size = ftell(file);
        fseek(file, 0, SEEK_SET);

        std::vector<uint8_t> buffer(file_size);

        const auto read_size = fread(buffer.data(), 1, file_size, file);
        fclose(file);

        if (read_size != file_size)
        {
            return PLCS_ENO_DATA;
        }

        auto res = plcs_evaluate_buffer(buffer.data(), buffer.size());
        if (res != PLCS_ESUCCESS)
        {
            return res;
        }

        return std::nullopt;
    }

    plcs_errors onInjectionAllow(plcs_evaluation_result eval_result, char**, size_t, const char* policy, int)
    {
        if (injection_status != InjectionStatus::UNKNOWN) return PLCS_ESUCCESS;

        injection_status = (eval_result == PLCS_EVAL_RESULT_TRUE) ? InjectionStatus::ALLOW : InjectionStatus::UNKNOWN;

        return PLCS_ESUCCESS;
    }

    plcs_errors onInjectionDeny(plcs_evaluation_result eval_result, char**, size_t, const char* policy, int)
    {
        if (injection_status != InjectionStatus::UNKNOWN) return PLCS_ESUCCESS;

        injection_status = (eval_result == PLCS_EVAL_RESULT_TRUE) ? InjectionStatus::DENY : InjectionStatus::UNKNOWN;

        return PLCS_ESUCCESS;
    }

    std::string GetDotnetDll()
    {
        const auto [_, tokenized_command_line] = ::shared::GetCurrentProcessCommandLine();
        if (tokenized_command_line.size() < 2)
        {
            return "";
        }

        if (tokenized_command_line[0] != L"dotnet" && tokenized_command_line[0] != L"dotnet.exe")
        {
            return "";
        }

        if (tokenized_command_line[1].ends_with(L".dll"))
        {
            return ::shared::ToString(tokenized_command_line[1]);
        }

        return "";
    }

} // namespace

bool wls()
{
    const auto process_name = ::shared::ToString(::shared::GetCurrentProcessName());

    std::string application_pool = "";
    if (auto maybe_application_pool = GetApplicationPool())
    {
        application_pool = std::move(::shared::ToString(*maybe_application_pool));
    }

    const auto dotnet_dll = GetDotnetDll();

    plcs_eval_ctx_init();
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_RUNTIME_LANGUAGE, "dotnet");
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_PROCESS_EXE, process_name.c_str());
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_IIS_APPLICATION_POOL, application_pool.c_str());
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_RUNTIME_ENTRY_POINT_FILE, dotnet_dll.c_str());

    plcs_eval_ctx_register_action(onInjectionDeny, PLCS_ACTION_INJECT_DENY);
    plcs_eval_ctx_register_action(onInjectionAllow, PLCS_ACTION_INJECT_ALLOW);

    injection_status = InjectionStatus::UNKNOWN;
    auto maybe_error = evaluatePolicies();
    if (maybe_error)
    {
        Log::Error(__func__, ": An error occured while evaluating workload selection policies (errno: ", *maybe_error,
                   ")");
        return false;
    }

    // We enable or disable instrumentation depending on the context:
    //   - Windows IIS: instrumentation is ENABLED by default, only need to consider DENY rules.
    //   - .NET: instrumentation is DISABLED by default, only consider ALLOW rules.
    if (IsRunningOnIIS())
    {
        return injection_status == InjectionStatus::DENY ? false : true;
    }

    return injection_status == InjectionStatus::ALLOW;
}

} // namespace datadog::shared::nativeloader
