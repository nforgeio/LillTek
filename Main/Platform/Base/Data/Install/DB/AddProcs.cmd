@echo off
REM ----------------------------------------------------------------------------
REM File:        AddProcs.cmd
REM Contributor: Jeff Lill
REM Copyright:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
REM
REM              Based on code and/or techniques from the LillTek Platform.
REM ----------------------------------------------------------------------------
REM
REM This batch file loads the functions and stored procedures into the
REM database specified.
REM
REM	Usage:	addprocs <server> <database> [ <account> <password> ]
REM
REM	where <server> is the name of the server hosting the <database> and
REM <account>/<password> are SQL/Server credentials that have owner
REM access rights to the database.  Windows security will be used if
REM the account credentials are not passed.
REM
REM The post-processed files will be located in %LT_TEMP%\tsqlpp\sp 
REM after this returns.
REM
REM Note that the SQL/Server utility OSQL.EXE must be located somewhere
REM on the current bin path for this to work.

if '%3'=='' set QUERY= osql -S %1 -d %2 -E -n -r -o %IF_TEMP%\tsqlpp\log.txt -i
if not '%3'=='' set QUERY= osql -S %1 -d %2 -U %3 -P %4 -n -r -o %LT_TEMP%\tsqlpp\log.txt -i

set SYMPATH=%ProjectDir%%OutDir%LillTek.Data.Install.dll
set DBROOT=%LT_ROOT%\Base\Data\Install\DB

@echo off

if exist %LT_TEMP%\tsqlpp\functions		del /q %LT_TEMP%\tsqlpp\functions\*.*
if exist %LT_TEMP%\tsqlpp\sp   			del /q %LT_TEMP%\tsqlpp\sp\*.*
if exist %LT_TEMP%\tsqlpp\log.txt 	    del %LT_TEMP%\tsqlpp\log.txt

if exist %SYMPATH% goto symOK
echo Error: [%SYMPATH%] has not been built.
goto done
:symOK

echo.
echo Copying processed files to: %LT_TEMP%\tsqlpp
echo.

echo.
echo Preprocessing Functions....
echo -----------------------------------

tsqlpp -trans -sym:%SYMPATH% -in:%DBROOT%\Functions\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\functions

echo.
echo Loading Functions....
echo -----------------------------------

for %%F in (%LT_TEMP%\tsqlpp\functions\*.sql) do (

	echo %%F
	%QUERY% %%F
)

echo.
echo Preprocessing Stored Procedures....
echo -----------------------------------

tsqlpp -trans -sym:%SYMPATH% -in:%DBROOT%\SP\*.sql -def:%DBROOT%\Schema\Schema.def -out:%LT_TEMP%\tsqlpp\sp

echo.
echo Loading Stored Procedures....
echo -----------------------------------

for %%F in (%LT_TEMP%\tsqlpp\sp\*.sql) do (

	echo %%F
	%QUERY% %%F
)

rem -----------------------------------------------------------------
rem Initialize the stored procedure security

echo.
echo Setting Stored Procedure Security
%QUERY% %DBROOT%\SP\SetProcSecurity.sql

echo.

:done