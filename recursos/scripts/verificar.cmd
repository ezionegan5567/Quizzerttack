@echo off
REM =========================================================================
REM  LAB14 · Lanzador de la verificacion (evita el bloqueo de ExecutionPolicy)
REM  Uso:  verificar.cmd -Proyecto 5906c36b-... -Ambiente development
REM        verificar.cmd -Proyecto 5906c36b-... -Ambiente development -VerificarEconomia
REM        verificar.cmd -Proyecto 5906c36b-... -Ambiente development -ProvocarError
REM =========================================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0verificar.ps1" %*
exit /b %ERRORLEVEL%
