@echo off
setlocal
cd /d "%~dp0"
set "MSBUILD="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul`) do set "MSBUILD=%%i"
)
if not defined MSBUILD (
  for /f "delims=" %%i in ('where msbuild 2^>nul') do if not defined MSBUILD set "MSBUILD=%%i"
)
if not defined MSBUILD (
  for %%p in (
    "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    "%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
  ) do if not defined MSBUILD if exist "%%~p" set "MSBUILD=%%~p"
)
if not defined MSBUILD (
  echo MSBuild was not found.
  echo Install Visual Studio 2022 or Visual Studio Build Tools with the .NET desktop build tools.
  pause
  exit /b 1
)
"%MSBUILD%" HarborBrowser.sln /restore /m /p:Configuration=Release /p:Platform="Any CPU"
if errorlevel 1 (
  echo Build failed. Review the errors above.
  pause
  exit /b 1
)
echo.
echo Build finished. Look in HarborBrowser\bin\Release\ for HarborBrowser.exe.
pause
