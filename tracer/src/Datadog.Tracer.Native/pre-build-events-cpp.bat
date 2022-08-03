REM This batch file moves locked outputs (dll/pdb/xml) so as not to block the local build process.
REM The corprofiler dlls get locked up when running the samples against iisexpress.exe for example
REM This is to fix the need to close Visual Studio, manually kill a VBCSCompiler.exe process, delete a dll, then restart Visual Studio

echo PreBuildEvents 
echo  $(TargetPath) is %1
echo  $(TargetFileName) is %2 
echo  $(TargetDir) is %3   
echo  $(TargetName) is %4

set dir=%3%LockedAssemblies

if not exist %dir% (mkdir %dir%)

REM delete all assemblies not really locked by a process
del "%dir%\*" /q

REM assembly file (.exe / .dll) - .pdb file - eventually .xml file (documentation) are concerned
if exist "%1"  move "%1" "%dir%\%2.locked.%random%"
if exist "%3%4.pdb" move "%3%4.pdb" "%dir%\%4.pdb.locked%random%"
if exist "%3%4.xml.locked" del "%dir%\%4.xml.locked%random%"