@ECHO OFF
ECHO\
ECHO Usage:
ECHO     BatchRunComputerAndExceptions.bat [-CleanTestRunDir]
ECHO\

ECHO =========== Test run settings; edit this script to adjust them as needed: ===========
SET DD_INTERNAL_DEMORUN_ITERATIONS=100
ECHO DD_INTERNAL_DEMORUN_ITERATIONS=%DD_INTERNAL_DEMORUN_ITERATIONS%

SET DD_INTERNAL_DEMORUN_TIMEOUT_SEC=185
ECHO DD_INTERNAL_DEMORUN_TIMEOUT_SEC=%DD_INTERNAL_DEMORUN_TIMEOUT_SEC%

ECHO\

SET DD_INTERNAL_BIN_OUTPUT_ROOT=%~dp0..\..\..\..\_build\
SET DD_INTERNAL_CURRENT_TEST_DIR=%DD_INTERNAL_BIN_OUTPUT_ROOT%TestRuns\BatchRunSimpleDemos

IF "%1"=="-CleanTestRunDir" (
    ECHO =========== -CleanTestRunDir specified. Cleaning test directory... ===========
    ECHO DD_INTERNAL_CURRENT_TEST_DIR=%DD_INTERNAL_CURRENT_TEST_DIR%
    ECHO\
    del %DD_INTERNAL_CURRENT_TEST_DIR% /f /s /q
    rmdir %DD_INTERNAL_CURRENT_TEST_DIR% /s /q
) ELSE (
    ECHO =========== -CleanTestRunDir NOT specified. Leaving test directory intact. ===========
    ECHO DD_INTERNAL_CURRENT_TEST_DIR=%DD_INTERNAL_CURRENT_TEST_DIR%
)

ECHO\

ECHO =========== Calling DDProf-SetEnv to configure the basic environment variables... =========== 
call %DD_INTERNAL_BIN_OUTPUT_ROOT%DDProf-Deploy\DDProf-SetEnv.bat 

@ECHO OFF
ECHO =========== Completed execution of DDProf-SetEnv. =========== 

ECHO\
ECHO =========== Configuring test-specific directories... ===========
ECHO\

SET DD_PROFILING_LOG_DIR=%DD_INTERNAL_CURRENT_TEST_DIR%\Logs
SET DD_PROFILING_OUTPUT_DIR=%DD_INTERNAL_CURRENT_TEST_DIR%\PProf-Files

ECHO DD_PROFILING_LOG_DIR=%DD_PROFILING_LOG_DIR%
ECHO DD_PROFILING_OUTPUT_DIR=%DD_PROFILING_OUTPUT_DIR%

SET DD_INTERNAL_DEMODIR_COMPUTER=%DD_INTERNAL_BIN_OUTPUT_ROOT%bin\Debug-AnyCPU\Demos\Computer01\
SET DD_INTERNAL_DEMODIR_EXCEPTIONGENERATOR=%DD_INTERNAL_BIN_OUTPUT_ROOT%bin\Debug-AnyCPU\Demos\ExceptionGenerator\

ECHO\
ECHO =========== Running the Demo "Computer" on Net Fx %DD_INTERNAL_DEMORUN_ITERATIONS% times for %DD_INTERNAL_DEMORUN_TIMEOUT_SEC% seconds each... ===========

for /l %%i in (1, 1, %DD_INTERNAL_DEMORUN_ITERATIONS%) do (

  ECHO\
  ECHO =========== Running the Demo "Computer" on Net Fx, iteration %%i of %DD_INTERNAL_DEMORUN_ITERATIONS%... ===========
  ECHO\
  %DD_INTERNAL_DEMODIR_COMPUTER%net45\Datadog.Demos.Computer01.exe --timeout %DD_INTERNAL_DEMORUN_TIMEOUT_SEC%
)

ECHO =========== Completed running the Demo "Computer" on Net Fx %DD_INTERNAL_DEMORUN_ITERATIONS% times. ===========
ECHO\

ECHO\
ECHO =========== Running the Demo "Computer" on Net Core %DD_INTERNAL_DEMORUN_ITERATIONS% times for %DD_INTERNAL_DEMORUN_TIMEOUT_SEC% seconds each... ===========

for /l %%i in (1, 1, %DD_INTERNAL_DEMORUN_ITERATIONS%) do (

  ECHO\
  ECHO =========== Running the Demo "Computer" on Net Core, iteration %%i of %DD_INTERNAL_DEMORUN_ITERATIONS%... ===========
  ECHO\
  %DD_INTERNAL_DEMODIR_COMPUTER%netcoreapp3.1\Datadog.Demos.Computer01.exe --timeout %DD_INTERNAL_DEMORUN_TIMEOUT_SEC%
)

ECHO =========== Completed running the Demo "Computer" on Net Core %DD_INTERNAL_DEMORUN_ITERATIONS% times. ===========
ECHO\

ECHO\
ECHO =========== Running the Demo "ExceptionGenerator" on Net Fx %DD_INTERNAL_DEMORUN_ITERATIONS% times for %DD_INTERNAL_DEMORUN_TIMEOUT_SEC% seconds each... ===========

for /l %%i in (1, 1, %DD_INTERNAL_DEMORUN_ITERATIONS%) do (

  ECHO\
  ECHO =========== Running the Demo "ExceptionGenerator" on Net Fx, iteration %%i of %DD_INTERNAL_DEMORUN_ITERATIONS%... ===========
  ECHO\
  %DD_INTERNAL_DEMODIR_EXCEPTIONGENERATOR%net45\Datadog.Demos.ExceptionGenerator.exe --timeout %DD_INTERNAL_DEMORUN_TIMEOUT_SEC%
)

ECHO =========== Completed running the Demo "ExceptionGenerator" on Net Fx %DD_INTERNAL_DEMORUN_ITERATIONS% times. ===========
ECHO\

ECHO\
ECHO =========== Running the Demo "ExceptionGenerator" on Net Core %DD_INTERNAL_DEMORUN_ITERATIONS% times for %DD_INTERNAL_DEMORUN_TIMEOUT_SEC% seconds each... ===========

for /l %%i in (1, 1, %DD_INTERNAL_DEMORUN_ITERATIONS%) do (

  ECHO\
  ECHO =========== Running the Demo "ExceptionGenerator" on Net Core, iteration %%i of %DD_INTERNAL_DEMORUN_ITERATIONS%... ===========
  ECHO\
  %DD_INTERNAL_DEMODIR_EXCEPTIONGENERATOR%netcoreapp3.1\Datadog.Demos.ExceptionGenerator.exe --timeout %DD_INTERNAL_DEMORUN_TIMEOUT_SEC%
)

ECHO =========== Completed running the Demo "ExceptionGenerator" on Net Core %DD_INTERNAL_DEMORUN_ITERATIONS% times. ===========
ECHO\

ECHO\
ECHO ON
