@echo off
title Session Management System - Launcher
color 0A

echo.
echo ==========================================================
echo        SESSION MANAGEMENT SYSTEM - AUTO LAUNCHER          
echo.
echo    Starting all three applications automatically...        
echo ==========================================================
echo.

set ROOT=%~dp0

if not exist "%ROOT%SessionManagement.Server" (
    echo [ERROR] Server project not found.
    pause
    exit /b
)

if not exist "%ROOT%SessionManagement.Admin" (
    echo [ERROR] Admin project not found.
    pause
    exit /b
)

if not exist "%ROOT%SessionManagement.Client" (
    echo [ERROR] Client project not found.
    pause
    exit /b
)

echo [1/3] Building solution first - please wait...
echo.
cd /d "%ROOT%"
dotnet build --nologo -v q

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ==========================================================
    echo    BUILD FAILED - Fix errors before launching.            
    echo ==========================================================
    echo.
    pause
    exit /b
)

echo.
echo [OK] Build succeeded.
echo.

timeout /t 1 /nobreak >nul

echo [2/3] Starting Server on http://localhost:5102 ...
start "SERVER - Session Management" cmd /k "color 0B && echo SERVER - ASP.NET Core + SignalR && echo http://localhost:5102 && cd /d %ROOT%SessionManagement.Server && dotnet run --no-build"

echo [..] Waiting 5 seconds for server to boot...
timeout /t 5 /nobreak >nul

echo [3a] Starting Admin Dashboard...
start "ADMIN - Dashboard" cmd /k "color 0E && echo ADMIN DASHBOARD - WPF && cd /d %ROOT%SessionManagement.Admin && dotnet run --no-build"

timeout /t 2 /nobreak >nul

echo [3b] Starting Client App...
start "CLIENT - Customer App" cmd /k "color 0D && echo CLIENT APP - WPF && cd /d %ROOT%SessionManagement.Client && dotnet run --no-build"

echo.
echo ==========================================================
echo    ALL THREE APPS ARE LAUNCHING                           
echo ==========================================================
echo.
pause