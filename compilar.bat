@echo off
title Compilando StreamControlC#
echo ==============================================
echo  Compilando y actualizando StreamControlCSharp.exe...
echo ==============================================

set DOTNET="%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
if not exist %DOTNET% set DOTNET=dotnet

%DOTNET% publish "src\StreamControlCSharp\StreamControlCSharp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "publish"

if %ERRORLEVEL% EQU 0 (
    copy /y "publish\StreamControlCSharp.exe" "StreamControlCSharp.exe" >nul
    rd /s /q "publish"
    echo.
    echo ==============================================
    echo  LISTO: StreamControlCSharp.exe actualizado con exito!
    echo ==============================================
) else (
    echo.
    echo [ERROR] Hubo un error de sintaxis en el archivo XAML o C#. Revisa el mensaje arriba.
)

pause
