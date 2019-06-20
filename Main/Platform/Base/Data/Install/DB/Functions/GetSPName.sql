-------------------------------------------------------------------------------
-- File:        GetSPName.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

if (exists (select 1 from sysobjects where xtype='FN' and name='GetSPName'))
    drop function GetSPName
go

-------------------------------------------------------------------------------
-- Returns the name of the stored procedure whose ID is passed.
--
-- Parameters:
--
--      @sprocID                - ID of the procedure

create function GetSPName(@sprocID int) returns $(ObjectName)
as begin

    declare @name $(ObjectName)
    
    select @name=name from sysobjects where id=@sprocID
    return @name
end
go
