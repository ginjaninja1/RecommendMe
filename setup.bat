@echo off
setlocal enabledelayedexpansion

:: 1. Copy the hook file into the hidden Git directory
if exist .githooks\pre-commit (
    xcopy /y .githooks\pre-commit .git\hooks\ >nul
    echo [+] Successfully installed pre-commit hook.
) else (
    echo [!] Error: .githooks\pre-commit file not found.
    pause
    exit /b
)

:: 2. Clean up the visible staging folder
rmdir /s /q .githooks
echo [+] Cleaned up temporary .githooks folder.

echo [+] Setup complete. This script will now self-destruct.
pause

:: 3. The self-destruct trick (deletes the running batch file itself)
(goto) 2>nul & del "%~f0"
