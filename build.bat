@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM  r-utils build script
REM  Builds a SELF-CONTAINED, single-file exe that needs NO
REM  runtime installed to RUN. Output goes to the "build" folder
REM  next to this .bat.
REM
REM  NOTE: building still requires the .NET 9 SDK (or newer) to be
REM  installed on this machine. The produced exe does not.
REM ============================================================

REM Work from the folder this .bat lives in, regardless of where it's called from.
cd /d "%~dp0"

echo.
echo === r-utils build ===
echo Folder: %~dp0
echo.

REM --- Check the .NET SDK is available ---
where dotnet >nul 2>nul
if errorlevel 1 goto :nosdk

dotnet --version >nul 2>nul
if errorlevel 1 goto :nosdk

for /f "delims=" %%v in ('dotnet --version 2^>nul') do set "SDKVER=%%v"
echo Using .NET SDK %SDKVER%
echo.

REM --- Publish: self-contained, single file, compressed ---
set "OUTDIR=%~dp0build"

dotnet publish "%~dp0r-utils.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o "%OUTDIR%"

if errorlevel 1 goto :buildfail

echo.
echo === BUILD SUCCEEDED ===
echo Exe: %OUTDIR%\r-utils.exe
echo.
echo This exe is self-contained - copy it anywhere and run it,
echo no .NET install required. Launch it BEFORE opening Roblox.
echo.

REM Offer to launch it right away for a quick test.
choice /c YN /n /m "Run r-utils now to test? [Y/N] "
if errorlevel 2 goto :end
start "" "%OUTDIR%\r-utils.exe"
goto :end

:nosdk
echo.
echo ERROR: the .NET SDK was not found on this machine.
echo Building requires the .NET 9 SDK (or newer). Install it from:
echo   https://dotnet.microsoft.com/download/dotnet/9.0
echo (choose the SDK, not just the Runtime), then run build.bat again.
echo.
pause
exit /b 1

:buildfail
echo.
echo === BUILD FAILED ===
echo Scroll up for the compiler error. If it mentions an SDK version,
echo install the .NET 9 SDK from:
echo   https://dotnet.microsoft.com/download/dotnet/9.0
echo.
pause
exit /b 1

:end
echo Done.
pause
endlocal
