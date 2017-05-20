@echo off
set version=%1

dotnet restore .\Easy.MessageHub
dotnet pack .\Easy.MessageHub\Easy.MessageHub.csproj -o ..\nupkgs -c Release /p:PackageVersion=%version% --include-symbols --include-source
