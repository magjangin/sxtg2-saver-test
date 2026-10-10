@echo off
setlocal

echo =======================================================
echo   SixtarBypassMod Build and Deploy Script
echo =======================================================
echo.

set "PROJ_PATH=%~dp0sxtg2 saver test\sxtg2 saver test.csproj"
set "BUILD_CONF=Release"
set "OUTPUT_DLL=%~dp0sxtg2 saver test\bin\%BUILD_CONF%\SixtarBypassMod.dll"
set "TARGET_MODS_DIR=H:\Sixtar Gate STARTRAIL server\Mods"

echo [1/2] Building mod (%BUILD_CONF% mode)...
dotnet build "%PROJ_PATH%" -c %BUILD_CONF%

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed! Check the error output above.
    echo =======================================================
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/2] Copying DLL to Mods folder...
if not exist "%TARGET_MODS_DIR%" (
    echo [INFO] Creating Mods directory: "%TARGET_MODS_DIR%"
    mkdir "%TARGET_MODS_DIR%"
)

copy /y "%OUTPUT_DLL%" "%TARGET_MODS_DIR%\SixtarBypassMod.dll" > nul

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Failed to copy DLL. Make sure the game is closed.
    echo =======================================================
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo =======================================================
echo [SUCCESS] Build and Deployment Complete!
echo Target: %TARGET_MODS_DIR%\SixtarBypassMod.dll
echo =======================================================
echo.
pause
