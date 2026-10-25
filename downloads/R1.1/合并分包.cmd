@echo off
setlocal
cd /d "%~dp0"
if not exist "FiberRouteR1-win-x64-R1.1.zip.part001" (echo Missing FiberRouteR1-win-x64-R1.1.zip.part001 & pause & exit /b 1)
if not exist "FiberRouteR1-win-x64-R1.1.zip.part002" (echo Missing FiberRouteR1-win-x64-R1.1.zip.part002 & pause & exit /b 1)
if not exist "FiberRouteR1-win-x64-R1.1.zip.part003" (echo Missing FiberRouteR1-win-x64-R1.1.zip.part003 & pause & exit /b 1)
if not exist "FiberRouteR1-win-x64-R1.1.zip.part004" (echo Missing FiberRouteR1-win-x64-R1.1.zip.part004 & pause & exit /b 1)
copy /b "FiberRouteR1-win-x64-R1.1.zip.part001"+"FiberRouteR1-win-x64-R1.1.zip.part002"+"FiberRouteR1-win-x64-R1.1.zip.part003"+"FiberRouteR1-win-x64-R1.1.zip.part004" "FiberRouteR1-win-x64-R1.1.zip" /y
if errorlevel 1 (pause & exit /b 1)
powershell -NoProfile -Command "if ((Get-FileHash -LiteralPath 'FiberRouteR1-win-x64-R1.1.zip' -Algorithm SHA256).Hash -ne '1011ea4aac733c1491630adf092305f62ebd0ab49a2eb8fce2ada010188aa173') { Write-Error 'SHA256 mismatch. Do not use the ZIP.'; exit 1 }; Write-Host 'SHA256 OK. Extract the ZIP and run the launcher.'"
if errorlevel 1 (pause & exit /b 1)
pause
