#!/usr/bin/env bash

bash --version 2>&1 | head -n 1

set -eo pipefail
SCRIPT_DIR=$(cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd)

###########################################################################
# CONFIGURATION
###########################################################################

BUILD_PROJECT_FILE="$SCRIPT_DIR/build/_build/_build.csproj"

###########################################################################
# EXECUTION
###########################################################################

export DOTNET_EXE="$(command -v dotnet)"

# Some commands apparently break unless this is set
# e.g. "/property:Platform=AnyCPU" gives
# No se reconoce el comando o el argumento "/property:Platform=AnyCPU"
export DOTNET_CLI_UI_LANGUAGE="en"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUKE_TELEMETRY_OPTOUT=1

echo "Microsoft (R) .NET Core SDK version $("$DOTNET_EXE" --version)"

"$DOTNET_EXE" build "$BUILD_PROJECT_FILE" /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity quiet

echo "dotnet build exited with code $?"
ls -l "$SCRIPT_DIR/build/_build/bin/Debug/_build"

# This shouldn't be needed, but the build randomly fails on OSX because the file doesn't have execute permissions
chmod +x "$SCRIPT_DIR/build/_build/bin/Debug/_build"
"$SCRIPT_DIR/build/_build/bin/Debug/_build" "$@"