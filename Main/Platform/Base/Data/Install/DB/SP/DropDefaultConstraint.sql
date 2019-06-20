-------------------------------------------------------------------------------
-- File:        DropDefaultConstraint.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

if (exists (select 1 from sysobjects where xtype='P' and name='DropDefaultConstraint'))
    drop procedure dbo.DropDefaultConstraint
go

-------------------------------------------------------------------------------
-- Drops a default constraint on a table and column if a constraint exists.
-- Pass the name of the table (without the "dbo." prefix) as well as the
-- name of the column.
--
-- Parameters: 
--
--      @tableName          - Name of the table (without a "dbo." prefix
--      @columnName         - Name of the column
--
-- Result Set: (none)

create procedure dbo.DropDefaultConstraint
    @tableName      varchar(100),
    @columnName     varchar(100)
as 
    declare @constraintName     varchar(100)
    declare @sqlCmd             nvarchar(1000)
    
    set @constraintName = (
        select name 
            from sysobjects so join sysconstraints sc
            on so.id = sc.constid 
            where object_name(so.parent_obj) = @tableName 
              and so.xtype = 'D'
              and sc.colid = (
                select colid from syscolumns 
                    where id = object_id('dbo.' + @tableName) and name = @columnName
                )
        )
    
    if (@constraintName is not null) begin
     
        set @sqlCmd = 'alter table dbo.' + @tableName + ' drop constraint ' + @constraintName
        exec sp_executesql @sqlCmd
    end
go
