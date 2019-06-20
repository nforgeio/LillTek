@echo off
REM ----------------------------------------------------------------------------
REM File:        AddProcs.cmd
REM Contributor: Jeff Lill
REM Copyright:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
REM
REM              Based on code and/or techniques from the LillTek Platform.
REM ----------------------------------------------------------------------------
REM
REM This batch file processes the *.sql files found in this directory
REM using the TSQLPP utility to process macro definitions found in
REM Schema.def.  The post-processed files will be located at
REM %LT_TEMP%\tsqlpp\schema

set SYMPATH=%ProjectDir%%OutDir%LillTek.Data.Install.dll
set DBROOT=%LT_ROOT%\Base\Data\Install\DB

if exist %SYMPATH% goto symOK
echo Error: [%SYMPATH%] has not been built.
goto done
:symOK

echo.
echo Preprocessing Schema files....
echo -----------------------------------

if exist %LT_TEMP%\tsqlpp\schema del /q %LT_TEMP%\tsqlpp\schema\*.*

tsqlpp -sym:%SYMPATH% -in:%DBROOT%\Schema\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\schema

echo.
echo Post-processed files copied to: %LT_TEMP%\tsqlpp\schema
echo.

echo.
echo Preprocessing Upgrade files....
echo -----------------------------------

if exist %LT_TEMP%\tsqlpp\upgrade del /q %LT_TEMP%\tsqlpp\upgrade\*.*

tsqlpp -sym:%SYMPATH% -in:%DBROOT%\Upgrade\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\upgrade

echo.
echo Post-processed files copied to: %LT_TEMP%\tsqlpp\upgrade
echo.

:done
