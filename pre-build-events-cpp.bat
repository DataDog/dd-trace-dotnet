REM This script is invoked before compiling an assembly, and if the target file exist, it moves it to a temporary location
REM The file-move works even if the existing assembly file is currently locked-by/in-use-in any process.
REM This way we can be sure that the compilation won't end up claiming the assembly cannot be erased!

echo PreBuildEvents 
echo  $(TargetPath) is %1
echo  $(TargetFileName) is %2 
echo  $(TargetDir) is %3   
echo  $(TargetName) is %4

set dir=%3%LockedAssemblies

if not exist %dir% (mkdir %dir%)

REM delete all assemblies moved not really locked by a process
del "%dir%\*" /q

REM assembly file (.exe / .dll) - .pdb file - eventually .xml file (documentation) are concerned
REM use %random% to let coexists several process that hold several versions of locked assemblies
if exist "%1"  move "%1" "%dir%\%2.locked.%random%"
if exist "%3%4.pdb" move "%3%4.pdb" "%dir%\%4.pdb.locked%random%"
if exist "%3%4.xml.locked" del "%dir%\%4.xml.locked%random%"

REM Code with Macros
REM   if exist "$(TargetPath)"  move "$(TargetPath)" "C:\temp\LockedAssemblies\$(TargetFileName).locked.%random%"
REM   if exist "$(TargetDir)$(TargetName).pdb" move "C:\temp\LockedAssemblies\$(TargetName).pdb" "$(TargetDir)$(TargetName).pdb.locked%random%"
REM   if exist "$(TargetDir)$(TargetName).xml.locked" del "C:\temp\LockedAssemblies\$(TargetName).xml.locked%random%"