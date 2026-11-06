@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the free .NET SDK 8 from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet publish src\LocalChat.csproj -c Release -r win-x64 -o publish
if errorlevel 1 (
  echo Build failed. See errors above.
) else (
  xcopy /E /I /Y runtime publish\runtime >nul
  echo EXE: %~dp0publish\NORA.exe - keep publish\runtime next to it
)
pause
