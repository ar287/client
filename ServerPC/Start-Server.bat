@echo off
title Session Management System - Server & Admin Launcher
color 0B

echo.
echo ==========================================================
echo       SESSION MANAGEMENT SYSTEM - SERVER LAUNCHER         
echo ==========================================================
echo.

set ROOT=%~dp0

if not exist "%ROOT%Server\SessionManagement.Server.exe" (
    echo [ERROR] Server executable not found at %ROOT%Server\SessionManagement.Server.exe
    pause
    exit /b 1
)

if not exist "%ROOT%Admin\SessionManagement.Admin.exe" (
    echo [ERROR] Admin executable not found at %ROOT%Admin\SessionManagement.Admin.exe
    pause
    exit /b 1
)

echo [1/3] Starting ASP.NET Core Server on http://0.0.0.0:5102 ...
start "Session Management Server" /d "%ROOT%Server" "%ROOT%Server\SessionManagement.Server.exe"

echo [2/3] Waiting for Server health check (http://localhost:5102/api/health)...

set RETRIES=0
:check_health
timeout /t 2 /nobreak >nul
set /a RETRIES+=1

powershell -Command "$ProgressPreference = 'SilentlyContinue'; try { $r = Invoke-WebRequest -Uri 'http://localhost:5102/api/health' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } else { exit 1 } } catch { exit 1 }" >nul 2>&1

if %ERRORLEVEL% EQU 0 (
    echo [OK] Server is ready and accepting requests!
    goto server_ready
)

if %RETRIES% GEQ 15 (
    echo [WARNING] Health check timed out. Proceeding to launch Admin...
    goto server_ready
)

echo [..] Server starting... retry %RETRIES%/15
goto check_health

:server_ready
echo.
echo ==========================================================
echo    SERVER IS ACTIVE AND LISTENING ON LAN (PORT 5102)      
echo    Centralized Client connection endpoint:                
echo    http://<SERVER-IP>:5102                                
echo ==========================================================
echo.
echo [3/3] Starting Admin Dashboard...
start "Session Management Admin" /d "%ROOT%Admin" "%ROOT%Admin\SessionManagement.Admin.exe"

echo.
echo Server and Admin have been launched successfully.
echo.
pause
