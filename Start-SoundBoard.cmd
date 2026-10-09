@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-SoundBoard.ps1"
if errorlevel 1 pause
