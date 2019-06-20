-------------------------------------------------------------------------------
-- File:        StdSuccess.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

if (exists (select 1 from sysobjects where xtype='P' and name='StdSuccess'))
    drop procedure StdSuccess
go

-------------------------------------------------------------------------------
-- This procedure returns a standard result set indicating a successful operation.
--
-- Parameters: None
--
-- Result Set:
--
--      Code:int                - 0 indicating success
--      Procedure:$(ObjectName  - null
--      Message:ErrorMessage    - null

create procedure StdSuccess
as
    set nocount on
            
    select 0    as 'Code',
           null as 'Procedure',
           null as 'Message'
go
