-------------------------------------------------------------------------------
-- File:        ClearDB.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.

-- Make sure that we're not modifying the MASTER database

if (db_name() = 'master') raiserror ('WARNING: Cannot modify the MASTER database.',1,1)
go
while (db_name() = 'master') print ' ' 
go

-------------------------------------------------------------------------------
-- Delete the stored procedures and functions

declare curProcs cursor local
    for select name,xtype from sysobjects 
        where xtype='P' or xtype='FN'

open curProcs

declare @name       sysname
declare @xtype      char(2)
declare @cmd        nchar(255)

fetch next from curProcs into @name, @xtype
while (@@fetch_status <> -1) begin

    if (@@fetch_status <> -2) begin

        if (@xtype='P')
            set @cmd = 'drop procedure ' + @name
        else
            set @cmd = 'drop function ' + @name
            
        exec sp_executesql @cmd
    end

    fetch next from curProcs into @name, @xtype
end

close curProcs
deallocate curProcs
go

-------------------------------------------------------------------------------
-- Delete the tables and indicies

drop table dbHistory
drop table ProductInfo
go
