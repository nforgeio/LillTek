-------------------------------------------------------------------------------
-- File:        DeleteProcs.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
-- Script to delete the stored procedures and functions

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

        if (@xtype='P') begin
        
            if (@name = 'SetProductInfo' or @name = 'GetProductInfo' or @name = 'StdError' or @name = 'StdSuccess')
                set @cmd = ' '  -- Don't delete these special sprocs
            else    
                set @cmd = 'drop procedure ' + @name
        end
        else
            set @cmd = 'drop function ' + @name
            
        if (@cmd <> ' ')
            exec sp_executesql @cmd
    end

    fetch next from curProcs into @name, @xtype
end

close curProcs
deallocate curProcs
go
