#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_

#include "string.h"  // NOLINT

namespace shared {
	namespace environment {

		// Sets the path for the profiler's log file.
		// Environment variable DD_TRACE_LOG_DIRECTORY takes precedence over this setting, if set.
		const WSTRING log_path = WStr("DD_TRACE_LOG_PATH");

		// Sets the directory for the profiler's log file.
		// If set, this setting takes precedence over environment variable DD_TRACE_LOG_PATH.
		// If not set, default is
		// "%ProgramData%"\Datadog .NET Tracer\logs\" on Windows or
		// "/var/log/datadog/dotnet/" on Linux.
		const WSTRING log_directory = WStr("DD_TRACE_LOG_DIRECTORY");


		//
		// LOADER FEATURE FLAGS
		//

		// Enables or disables the module initializer rewriting
		const WSTRING loader_rewrite_module_initializer_enabled = WStr("DD_LOADER_REWRITE_MODULE_INITIALIZER_ENABLED");

		// Enables or disables the module entrypoint rewriting
		const WSTRING loader_rewrite_module_entrypoint_enabled = WStr("DD_LOADER_REWRITE_MODULE_ENTRYPOINT_ENABLED");

		// Enables or disables the mscorlib rewriting
		const WSTRING loader_rewrite_mscorlib_enabled = WStr("DD_LOADER_REWRITE_MSCORLIB_ENABLED");

		// Enables or disables NGEN images support
		const WSTRING loader_ngen_enabled = WStr("DD_LOADER_NGEN_ENABLED");

	}  // namespace environment
}  // namespace shared

#endif
