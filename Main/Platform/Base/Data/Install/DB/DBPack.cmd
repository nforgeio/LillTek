@echo on
REM ----------------------------------------------------------------------------
REM File:        AddProcs.cmd
REM Contributor: Jeff Lill
REM Copyright:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
REM
REM              Based on code and/or techniques from the LillTek Platform.
REM ----------------------------------------------------------------------------
REM
REM This batch file creates a database instalation package and
REM then copies it to the location specified on the command line.
REM
REM     Usage: dbpack <output file>

if not '%1'=='' goto argOK
echo Error Usage: dbpack [output file]
goto done
:argOK

set SYMPATH=%ProjectDir%%OutDir%LillTek.Data.Install.dll
set DBROOT=%LT_ROOT%\Base\Data\Install\DB

if '%LT_BUILD_DBPACK%'=='0' goto done

if exist %SYMPATH% goto symOK
echo Error: [%SYMPATH%] has not been built.
goto done
:symOK

echo.
echo Deleting output files....
echo -----------------------------------

if exist %LT_TEMP%\tsqlpp\install.dbpack	del /q %LT_TEMP%\tsqlpp\install.dbpack
if exist %LT_TEMP%\tsqlpp\upgrade			del /q %LT_TEMP%\tsqlpp\upgrade\*.*
if exist %LT_TEMP%\tsqlpp\schema			del /q %LT_TEMP%\tsqlpp\schema\*.*
if exist %LT_TEMP%\tsqlpp\functions 	    del /q %LT_TEMP%\tsqlpp\functions\*.*
if exist %LT_TEMP%\tsqlpp\sp   				del /q %LT_TEMP%\tsqlpp\sp\*.*
if exist %LT_TEMP%\tsqlpp\log.txt 	        del %LT_TEMP%\tsqlpp\log.txt

echo.
echo Preprocessing Schema files....
echo -----------------------------------

tsqlpp -sym:%SYMPATH% -in:%DBROOT%\Schema\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\schema

echo.
echo Preprocessing Upgrade files....
echo -----------------------------------

tsqlpp -sym:%SYMPATH% -in:%DBROOT%\Upgrade\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\upgrade

echo.
echo Copying processed files to: %LT_TEMP%\tsqlpp
echo.

echo.
echo Preprocessing Functions....
echo -----------------------------------

tsqlpp -trans -sym:%SYMPATH% -in:%DBROOT%\Functions\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\functions

echo.
echo Preprocessing Stored Procedures....
echo -----------------------------------

tsqlpp -trans -sym:%SYMPATH% -in:%DBROOT%\SP\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\sp

echo.
echo Creating the database package....
echo -----------------------------------

dbpackage -create -setup:%DBROOT%\Schema\schema.def -schema:%LT_TEMP%\tsqlpp\schema -welcome:%DBROOT%\Setup\welcome.rtf -upgrade:%LT_TEMP%\tsqlpp\upgrade -funcs:%LT_TEMP%\tsqlpp\functions -procs:%LT_TEMP%\tsqlpp\sp -out:%LT_TEMP%\tsqlpp\install.dbpack

if '%1' == '' goto done

copy %LT_TEMP%\tsqlpp\install.dbpack %1

echo.

:done

exit /b 0
