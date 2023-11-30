@echo off
start cmd /c powershell.exe -ExecutionPolicy RemoteSigned "%~dp0\powershot.ps1" <nul
