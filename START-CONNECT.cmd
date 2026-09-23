@echo off
REM ============================================================
REM  START-CONNECT.cmd
REM  Startet Connect Desktop mit echtem integrierten AI-Browser.
REM ============================================================
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desktop\connect-browser\start-connect-full.ps1"
if %ERRORLEVEL% NEQ 0 (
  pause
)
