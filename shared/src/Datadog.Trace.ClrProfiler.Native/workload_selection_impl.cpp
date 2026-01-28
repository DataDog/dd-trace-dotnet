#include "workload_selection_impl.h"

#include "log.h"
#include "util.h"

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

    std::string GetDotnetDll(const std::vector<::shared::WSTRING>& argv)
    {
        if (argv.size() < 2)
        {
            return "";
        }

        if (argv[0] != L"dotnet" && argv[0] != L"dotnet.exe")
        {
            return "";
        }

        auto dll_arg = argv[1];
        if (argv[1] == L"exec")
        {
            if (argv.size() <= 2)
            {
                return "";
            }
            dll_arg = argv[2];
        }

        if (dll_arg.empty() || !dll_arg.ends_with(L".dll"))
        {
            return "";
        }

        auto dll_path = fs::path(::shared::ToString(dll_arg));
        return dll_path.filename().string();
    }

} // namespace

std::optional<std::vector<uint8_t>> readPolicies()
{
    const auto policies_file = GetPoliciesPath();

    FILE* file = NULL;
    fopen_s(&file, policies_file.string().c_str(), "rb");
    if (!file)
    {
        return std::nullopt;
    }

    fseek(file, 0, SEEK_END);
    const auto file_size = ftell(file);
    fseek(file, 0, SEEK_SET);

    std::vector<uint8_t> buffer(file_size);

    const auto read_size = fread(buffer.data(), 1, file_size, file);
    fclose(file);

    if (read_size != file_size)
    {
        return std::nullopt;
    }

    return buffer;
}

bool isWorkloadAllowed(const ::shared::WSTRING& process_name, const std::vector<::shared::WSTRING>& argv,
                       const ::shared::WSTRING& application_pool, const std::vector<uint8_t>& policies,
                       const bool is_iis)
{
    const auto process_name_str = ::shared::ToString(process_name);
    const auto application_pool_str = ::shared::ToString(application_pool);

    const auto dotnet_dll = GetDotnetDll(argv);

    plcs_eval_ctx_init();
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_RUNTIME_LANGUAGE, "dotnet");
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_PROCESS_EXE, process_name_str.c_str());
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_IIS_APPLICATION_POOL, application_pool_str.c_str());
    plcs_eval_ctx_set_str_eval_param(PLCS_STR_EVAL_RUNTIME_ENTRY_POINT_FILE, dotnet_dll.c_str());

    plcs_eval_ctx_register_action(onInjectionDeny, PLCS_ACTION_INJECT_DENY);
    plcs_eval_ctx_register_action(onInjectionAllow, PLCS_ACTION_INJECT_ALLOW);

    injection_status = InjectionStatus::UNKNOWN;

    if (auto res = plcs_evaluate_buffer(policies.data(), policies.size()); res != PLCS_ESUCCESS)
    {
        Log::Error(__func__, ": An error occured while evaluating workload selection policies (errno: ", res, ")");
        return true;
    }

    // We enable or disable instrumentation depending on the context:
    //   - Windows IIS: instrumentation is ENABLED by default, only need to consider DENY rules.
    //   - .NET: instrumentation is DISABLED by default, only consider ALLOW rules.
    if (is_iis)
    {
        return injection_status == InjectionStatus::DENY ? false : true;
    }

    return injection_status == InjectionStatus::ALLOW ? true : false;
}

} // namespace datadog::shared::nativeloader
