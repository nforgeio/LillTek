-------------------------------------------------------------------------------
-- File:        StdError.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

if (exists (select 1 from sysobjects where xtype='P' and name='StdError'))
    drop procedure StdError
go

-------------------------------------------------------------------------------
-- This procedure returns a result set with the error code and message passed.
--
-- Note that these codes correspond to the codes defined in MSSQL2000.Error.*
--
-- Note that SQL Azure does not implement the xp_sprintf stored procedure
-- so messages will not be properly formatted on this platform.
--
-- Parameters:
--
--      @sprocID                - @@procid of the calling sproc
--      @code                   - The error code (or 0 for success)
--      @message                - The error message (or null for success)
--      @param1..4              - Optional parameters.  These strings will
--                                be substituted for %s markers in the
--                                message string.
--
-- Result Set:
--
--      Code:int                - One of the non-zero MSSQL2000.Error.* constants on failure
--                                or zero on success.
--      Procedure:$(ObjectName) - Name of the SPROC returning the error (or null)
--      Message:ErrorMessage    - A human readable error message (or null)

create procedure StdError
    @sprocID            int,
    @code               int,
    @message            $(ErrorMsg) = null,
    @param1             $(ErrorMsg) = null,
    @param2             $(ErrorMsg) = null,
    @param3             $(ErrorMsg) = null,
    @param4             $(ErrorMsg) = null
as
    set nocount on
    
    declare @msg      as $(ErrorMsg)

	if (serverproperty('edition') = 'SQL Azure') begin

		set @msg = @message

		if (@param1 is not null)
		    set @msg = @msg + ' P1=[' + @param1 + ']'
		if (@param2 is not null)
		    set @msg = @msg + ' P2=[' + @param2 + ']'
		if (@param3 is not null)
		    set @msg = @msg + ' P3=[' + @param3 + ']'
		if (@param4 is not null)
		    set @msg = @msg + ' P4=[' + @param4 + ']'
	end
	else begin
    
	    if (@param4 is not null)
	        exec xp_sprintf @msg output, @message, @param1, @param2, @param3, @param4
	    else if (@param3 is not null)
	        exec xp_sprintf @msg output, @message, @param1, @param2, @param3
	    else if (@param2 is not null)
	        exec xp_sprintf @msg output, @message, @param1, @param2
	    else if (@param1 is not null)
	        exec xp_sprintf @msg output, @message, @param1
	    else
	        set @msg = @message
	end
        
    select @code                   as 'Code',
           dbo.GetSPName(@sprocID) as 'Procedure',
           @msg                    as 'Message'
go
