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

%ExePath% -i:QC_Shew_12_02_pt5_2c_20Dec12_Leopard_12-11-10_xt.txt -e:xtandem_Rnd1PartTryp_Rnd2DynMetOx.xml                               > PeptideListToXML_ConsoleOutput.txt

echo.  >> PeptideListToXML_ConsoleOutput.txt
echo.  >> PeptideListToXML_ConsoleOutput.txt
echo.  >> PeptideListToXML_ConsoleOutput.txt

%ExePath% -i:QC_Shew_12_02_pt5_2c_20Dec12_Leopard_12-11-10_xt.txt -e:xtandem_Rnd1PartTryp_Rnd2DynMetOx.xml /NoScanStats -o:NoScanStats\ >> PeptideListToXML_ConsoleOutput.txt

:Done

pause
