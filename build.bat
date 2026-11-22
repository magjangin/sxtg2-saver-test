@echo off
setlocal

rem ---------------------------------------------------------------
rem  SixtarBypassMod build and deploy script
rem
rem  Usage:
rem    build.bat                      build Release for the default game folder, copy to Mods
rem    build.bat "D:\Games\Sixtar"    pass the game folder as the first argument
rem    set GAME_DIR=D:\Games\Sixtar   or set it as an environment variable
rem    set NO_PAUSE=1                 skip the final pause (automation)
rem ---------------------------------------------------------------

echo =======================================================
echo   SixtarBypassMod Build and Deploy Script
echo =======================================================
echo.

if not "%~1"=="" set "GAME_DIR=%~1"
if "%GAME_DIR%"=="" set "GAME_DIR=H:\Sixtar Gate STARTRAIL server"

set "PROJ_PATH=%~dp0sxtg2 saver test\sxtg2 saver test.csproj"
set "BUILD_CONF=Release"
set "OUTPUT_DLL=%~dp0sxtg2 saver test\bin\%BUILD_CONF%\SixtarBypassMod.dll"
set "TARGET_MODS_DIR=%GAME_DIR%\Mods"

if not exist "%GAME_DIR%\Sixtar Gate STARTRAIL_Data\Managed\Assembly-CSharp.dll" (
    echo [ERROR] Game folder not found or invalid: "%GAME_DIR%"
    echo         Pass the game folder as the first argument or set GAME_DIR.
    goto :fail
)

echo Game folder : %GAME_DIR%
echo.
echo [1/2] Building mod (%BUILD_CONF% mode)...
dotnet build "%PROJ_PATH%" -c %BUILD_CONF% /nologo /p:GameDir="%GAME_DIR%"
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed! Check the error output above.
    goto :fail
)

echo.
echo [2/2] Copying DLL to Mods folder...
if not exist "%TARGET_MODS_DIR%" (
    echo [INFO] Creating Mods directory: "%TARGET_MODS_DIR%"
    mkdir "%TARGET_MODS_DIR%"
)

copy /y "%OUTPUT_DLL%" "%TARGET_MODS_DIR%\SixtarBypassMod.dll" > nul
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to copy DLL. Make sure the game is closed.
    goto :fail
)

echo.
echo =======================================================
echo [SUCCESS] Build and Deployment Complete!
echo Target: %TARGET_MODS_DIR%\SixtarBypassMod.dll
echo =======================================================
echo.
if not defined NO_PAUSE pause
exit /b 0

:fail
echo =======================================================
if not defined NO_PAUSE pause
exit /b 1
