@echo off
set releaseVersion=%1

dotnet restore .\Easy.MessageHub
dotnet pack .\Easy.MessageHub\Easy.MessageHub.csproj -o ..\nupkgs -c Release /p:Version=%releaseVersion% --include-symbols --include-source
