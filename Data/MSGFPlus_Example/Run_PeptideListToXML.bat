@echo off

set ExePath=PeptideListToXML.exe

if exist %ExePath% goto DoWork
if exist ..\%ExePath% set ExePath=..\%ExePath% && goto DoWork
if exist ..\..\Bin\%ExePath% set ExePath=..\..\Bin\%ExePath% && goto DoWork

echo Executable not found: %ExePath%
goto Done

:DoWork
echo.
echo Procesing with %ExePath%
echo.

%ExePath% /i:QC_Shew_13_05b_HCD_500ng_24Mar14_Tiger_14-03-04_msgfplus_syn.txt /e:MSGFPlus_PartTryp_MetOx_20ppmParTol.txt > PeptideListToXML_ConsoleOutput.txt

:Done

pause
