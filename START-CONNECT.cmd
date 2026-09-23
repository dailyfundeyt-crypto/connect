@echo off
REM Native Connect window. This file is OpenBot\START-CONNECT.cmd in a clone.
REM Not Desktop, not Downloads, not OpenBot\connect-app. No address bar.
REM The window does not need WSL. The full UI does (Docker + WSL).
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0START-CONNECT.ps1" %*
exit /b %ERRORLEVEL%
