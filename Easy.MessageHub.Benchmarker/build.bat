@echo off
dotnet restore
dotnet build -r win7-x64
dotnet publish -c release -r win7-x64

dotnet build -r win7-x86
dotnet publish -c release -r win7-x86