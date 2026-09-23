@echo off
REM Native Connect window. This starts openbot-desktop.exe.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0START-CONNECT.ps1" %*
exit /b %ERRORLEVEL%
