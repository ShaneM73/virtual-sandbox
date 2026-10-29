@echo off
rem Starts the AR Sandbox with the Kinect and opens the control page in your browser.
cd /d "%~dp0"
SandboxBridge.exe %*
pause
