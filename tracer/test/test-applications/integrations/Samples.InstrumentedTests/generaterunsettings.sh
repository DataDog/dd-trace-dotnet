PROJECT_FOLDER="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"
SOLUTIONFOLDER="$( cd -- "$(dirname "$0")/../../../../../" >/dev/null 2>&1 ; pwd -P )"
MONITORING_HOME_FOLDER="${SOLUTIONFOLDER}/shared/bin/monitoring-home"
FILE="${PROJECT_FOLDER}/iast.runsettings"
DISTRIBUTION="$(cat /etc/*-release)"
ARCH=$(uname -m)
FILE_EXTENSION="so"

echo DISTRIBUTION $DISTRIBUTION
echo ARCH $ARCH
echo PROJECT_FOLDER $PROJECT_FOLDER
echo FILE $FILE
echo SOLUTIONFOLDER $SOLUTIONFOLDER

if [[ "$ARCH" == *"aarch64"* ]]; then
    case $DISTRIBUTION in
    *"Alpine"*) echo Alpine; BIN_FOLDER="linux-musl-arm64";;
    *) echo Linux; BIN_FOLDER="linux-arm64";;
    esac
  else
	  case $DISTRIBUTION in 
		*"Ubuntu"*) echo Ubuntu; BIN_FOLDER="linux-x64";;
		*"Alpine"*) echo Alpine; BIN_FOLDER="linux-musl-x64";;
		*) echo Linux; BIN_FOLDER="linux-x64";;
	  esac
fi

# Check for macos
# if $DISTRIBUTION is empty and uname -s is Darwin then it's macos
if [[ -z "$DISTRIBUTION" && "$(uname -s)" == "Darwin" ]]; then
  echo "Detected macOS"
  BIN_FOLDER="osx"
  FILE_EXTENSION="dylib"
fi

echo BIN_FOLDER $BIN_FOLDER

echo "<?xml version=\"1.0\" encoding=\"utf-8\"?>" > $FILE
echo "<RunSettings><RunConfiguration><EnvironmentVariables>" >> $FILE
echo "<CORECLR_ENABLE_PROFILING>1</CORECLR_ENABLE_PROFILING>" >> $FILE
echo "<CORECLR_PROFILER>{846F5F1C-F9AE-4B07-969E-05C26BC060D8}</CORECLR_PROFILER>" >> $FILE
echo "<CORECLR_PROFILER_PATH>${MONITORING_HOME_FOLDER}/${BIN_FOLDER}/Datadog.Trace.ClrProfiler.Native.${FILE_EXTENSION}</CORECLR_PROFILER_PATH>" >> $FILE
echo "<DD_DOTNET_TRACER_HOME>${MONITORING_HOME_FOLDER}</DD_DOTNET_TRACER_HOME>" >> $FILE
echo "<DD_VERSION>1.0.0</DD_VERSION>" >> $FILE
echo "<DD_TRACE_DEBUG>1</DD_TRACE_DEBUG>" >> $FILE
echo "<DD_IAST_ENABLED>1</DD_IAST_ENABLED>" >> $FILE
echo "<DD_CIVISIBILITY_ENABLED>0</DD_CIVISIBILITY_ENABLED>" >> $FILE
echo "<DD_IAST_DEDUPLICATION_ENABLED>0</DD_IAST_DEDUPLICATION_ENABLED>" >> $FILE
echo "</EnvironmentVariables></RunConfiguration></RunSettings>" >> $FILE
