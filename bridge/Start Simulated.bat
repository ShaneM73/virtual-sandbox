@echo off
rem Starts the AR Sandbox with a simulated Kinect (no hardware needed).
cd /d "%~dp0"
start "" "http://localhost:8765/physical/?sim=1"
SandboxBridge.exe --no-browser %*
pause
