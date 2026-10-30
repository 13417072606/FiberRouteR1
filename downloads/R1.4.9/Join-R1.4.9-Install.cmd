@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Join-R1.4.9-Install.ps1"
if errorlevel 1 pause
