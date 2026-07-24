@echo off
setlocal
cd /d "%~dp0"
title LTC Cognitive API
echo Starting LTC Cognitive API...
echo Keep this window open while using Unity.
echo.
dotnet run --project "backend\LtcCognitive.Api\LtcCognitive.Api.csproj" --urls "http://127.0.0.1:5077"
echo.
echo The API has stopped. Review any error message above.
pause
