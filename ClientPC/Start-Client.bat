@echo off
title Session Management System - Client Launcher
color 0D

echo.
echo ==========================================================
echo       SESSION MANAGEMENT SYSTEM - CLIENT LAUNCHER         
echo ==========================================================
echo.

set ROOT=%~dp0

if not exist "%ROOT%Client\SessionManagement.Client.exe" (
    echo [ERROR] Client executable not found at %ROOT%Client\SessionManagement.Client.exe
    pause
    exit /b 1
)

echo Starting Client Application...
echo Make sure the Server PC is running on the local network.
echo Configured Server Base URL will be loaded from Client\appsettings.json.
echo.

start "Session Management Client" /d "%ROOT%Client" "%ROOT%Client\SessionManagement.Client.exe"

echo Client Application launched successfully.
timeout /t 3 /nobreak >nul
exit /b 0
