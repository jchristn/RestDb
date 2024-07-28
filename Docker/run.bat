@echo off

IF "%1" == "" GOTO :Usage

if not exist restdb.json (
  echo Configuration file litegraph.json not found.
  exit /b 1
)

REM Items that require persistence
REM   restdb.json
REM   sample.db
REM   logs/

REM Argument order matters!

docker run ^
  -p 8000:8000 ^
  -t ^
  -i ^
  -e "TERM=xterm-256color" ^
  -v .\restdb.json:/app/restdb.json ^
  -v .\sample.db:/app/sample.db ^
  -v .\logs\:/app/logs/ ^
  jchristn/restdb:%1

GOTO :Done

:Usage
ECHO Provide one argument indicating the tag. 
ECHO Example: dockerrun.bat v2.0.6
:Done
@echo on
