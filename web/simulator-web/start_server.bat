@echo off
cd /d "%~dp0"
python -m http.server 8766 --bind 127.0.0.1
