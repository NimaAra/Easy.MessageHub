@echo off
dotnet restore Easy.MessageHub
dotnet pack -o NuGet -c Release Easy.MessageHub