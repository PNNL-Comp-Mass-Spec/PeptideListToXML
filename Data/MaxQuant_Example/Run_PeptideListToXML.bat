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

%ExePath% QC_Mam_19_01_Run3_02Jun21_Cicero_WBEH-20-09-08_maxq_syn.txt /E:MaxQuant_Tryp_Stat_CysAlk_Dyn_MetOx_NTermAcet_20ppmParTol.xml /NoMSGF > PeptideListToXML_ConsoleOutput.txt

:Done

pause
