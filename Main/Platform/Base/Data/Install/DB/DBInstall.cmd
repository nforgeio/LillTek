@echo off
REM ----------------------------------------------------------------------------
REM File:        AddProcs.cmd
REM Contributor: Jeff Lill
REM Copyright:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
REM
REM              Based on code and/or techniques from the LillTek Platform.
REM ----------------------------------------------------------------------------
REM
REM This batch file creates a database installation package in 
REM a temporary directory and then launches the installer.

call dbpack %LT_TEMP%\tsqlpp\LillTek.DBExample.dbpack
dbpackage -install:%LT_TEMP%\tsqlpp\LillTek.DBExample.dbpack

echo.

:done
