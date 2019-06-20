-------------------------------------------------------------------------------
-- File:        Ping.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

if (exists (select 1 from sysobjects where xtype='P' and name='Ping'))
    drop procedure Ping
go

-------------------------------------------------------------------------------
-- A standard stored procedure designed to be queried by the LillTek Sentinel
-- service to monitor the health of the database.  The procedure returns a
-- two column one row result set that indicates the database status and an
-- optional more detailed description.
--
-- Parameters: (none)
--
-- Result Set:
--
--		Status:$(ObjectName)		- One of: "OK", "WARNING", "ERROR", "MAINTENENCE"
--		Details:$(Description)		- An optional human readable more detailed description

create procedure Ping
as 
	if (exists (select 1 from ProductInfo))
		select 'OK' as Status,
			   ' '  as Details
	else
		select 'ERROR' as Status,
			   ' '  as Details
go
