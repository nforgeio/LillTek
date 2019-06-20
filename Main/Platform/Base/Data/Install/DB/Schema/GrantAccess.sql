-------------------------------------------------------------------------------
-- File:        GrantAccess.sql
-- Contributor: Jeff Lill
-- Copyright:   Copyright (c) 2009-2010 by Clearsight Systems LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

-------------------------------------------------------------------------------
-- This script grants access to all stored procedures and tables in the 
-- database to a specific account.  The script is designed to be edited
-- before running.  Replace the "%account%" text with the actual
-- SQL account name.
--
-- Note that a database user must already have been created for the login
-- before executing this script.

-- Grant execute access to the stored procedures

declare @strCMD     nchar(255)
declare @procName   sysname

declare curProcs cursor local
    for select name from sysobjects 
        where xtype='P'

open curProcs

fetch next from curProcs into @procName
while (@@fetch_status <> -1) begin

    if (@@fetch_status <> -2) begin

        set @strCMD = 'grant execute on ' + @procName + ' to %account%'
        exec sp_executesql @strCMD
    end

    fetch next from curProcs into @procName
end

close curProcs
deallocate curProcs

-- Grant direct table permissions so applications can use LINQ.

declare @tableName  sysname

declare curTables cursor local
    for select name from sysobjects 
        where xtype='U'

open curTables

fetch next from curTables into @tableName
while (@@fetch_status <> -1) begin

    if (@@fetch_status <> -2) begin

        set @strCMD = 'grant delete, insert, references, select, update on ' + @tableName + ' to %account%'
        exec sp_executesql @strCMD
    end

    fetch next from curTables into @tableName
end

close curTables
deallocate curTables

go
