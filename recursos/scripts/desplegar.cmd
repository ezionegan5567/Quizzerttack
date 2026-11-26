@echo off
REM =========================================================================
REM  LAB14 · Lanzador del despliegue (evita el bloqueo de ExecutionPolicy)
REM  Uso:  desplegar.cmd -Ambiente development -Simular
REM        desplegar.cmd -Ambiente development
REM        desplegar.cmd -Ambiente production -Confirmar
REM        desplegar.cmd -Ambiente development -Revertir ..\respaldos\archivo.ccmzip
REM =========================================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desplegar.ps1" %*
set CODIGO=%ERRORLEVEL%
if not "%CODIGO%"=="0" (
  echo.
  echo El despliegue termino con codigo %CODIGO%.
)
exit /b %CODIGO%
