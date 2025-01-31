#include <iostream>
#include <string>
#include <sys/stat.h>

#include <vector>
#include <fstream>
#include "library_config.h"
#ifndef _WINDOWS
#include <datadog/library-config.h>
#include <unistd.h>
extern char **environ;
#endif
#include <datadog/common.h>

#include "util.h"

// TODO: change path to new location & add Windows
// TODO: when libdatadog is bumped over 15.0.0 there will be two config paths
std::string shared::LibraryConfig::config_path = "/etc/datadog-agent/managed/datadog-apm-libraries/stable/libraries_config.yaml";

ddog_CharSlice 
shared::LibraryConfig::to_char_slice(std::string const& str)
{
    return {str.data(), str.size()};
}

#ifndef _WINDOWS
ddog_Slice_CharSlice 
shared::LibraryConfig::to_slice_char_slice(const std::vector<std::string>& vec) {
    ddog_CharSlice* slices = new ddog_CharSlice[vec.size()];
    for (size_t i = 0; i < vec.size(); i++) {
        slices[i] = to_char_slice(vec[i]);
    }
    return {slices, vec.size()};
}

std::vector<shared::ConfigEntry>
shared::LibraryConfig::get_configuration(bool debug_logs)
{
    // Get environment
    std::vector<std::string> envp;
    for (char **current = environ; *current; current++) {
        envp.push_back(*current);
    }

    // Get args (by reading /proc/self/cmdline); linux only for now
    // TODO: add MacOS support
    std::vector<std::string> args;
    #ifdef __linux__
    std::ifstream cmdline("/proc/self/cmdline", std::ios::in);
    if (cmdline) {
        std::string arg;
        char ch;
        while (cmdline.get(ch)) {
            if (ch == '\0') {
                if (!arg.empty()) {
                    args.push_back(arg);
                    arg.clear();
                }
            } else {
                arg.push_back(ch);
            }
        }
        if (!arg.empty()) {
            args.push_back(arg);
        }
    }
    #endif

    // Get configuration
    ddog_ProcessInfo process_info{
        .args = shared::LibraryConfig::to_slice_char_slice(args),
        .envp = shared::LibraryConfig::to_slice_char_slice(envp),
        .language = shared::LibraryConfig::to_char_slice("dotnet"),
    };

    ddog_Configurator *configurator = ddog_library_configurator_new(debug_logs);
    ddog_Result_VecLibraryConfig config_result = ddog_library_configurator_get_path(configurator, process_info, shared::LibraryConfig::to_char_slice(shared::LibraryConfig::config_path));
    if (config_result.tag == DDOG_RESULT_VEC_LIBRARY_CONFIG_ERR_VEC_LIBRARY_CONFIG) {
        ddog_Error err = config_result.err;
        if (debug_logs) {
            auto ddog_err = ddog_Error_message(&err);
            std::string err_msg;
            std::cerr << err_msg.assign(ddog_err.ptr, ddog_err.ptr + ddog_err.len) << std::endl;
        }
        ddog_Error_drop(&err);
        return {};
    }

    // Format to return type
    ddog_Vec_LibraryConfig configs = config_result.ok;
    std::vector<shared::ConfigEntry> result(configs.len);
    for (size_t i = 0; i < configs.len; i++) {
        const ddog_LibraryConfig *cfg = &configs.ptr[i];
        ddog_CStr name = ddog_library_config_name_to_env(cfg->name);
        const ddog_CString value = cfg->value;
        result.push_back({
            .key = name.ptr,
            .value = value.ptr,
        });
    };

    return result;
}
#else
std::vector<shared::ConfigEntry>
shared::LibraryConfig::get_configuration(bool debug_logs)
{
    // Configuration from file is not yet supported on Windows; once the paths 
    // are defined we can just remove that check
    return {};
}
#endif
