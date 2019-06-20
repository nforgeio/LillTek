-------------------------------------------------------------------------------
-- File:        SetProcSecurity.sql
-- Contributor: Jeff Lill
-- Copyright:   Copyright (c) 2010 by EvolveNexus, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.
-------------------------------------------------------------------------------
--
-- This script grants access to all stored procedures and functions in the 
-- database to the PUBLIC role.

-- Grant EXECUTE for sprocs

declare curProcs cursor local
for
    select name from sysobjects 
        where xtype='P' or xtype='FN' or xtype='IF'

open curProcs

declare @procName   sysname
declare @strCMD     nchar(255)

fetch next from curProcs into @procName
while (@@fetch_status <> -1) begin

    if (@@fetch_status <> -2) begin

        set @strCMD = 'grant all on ' + @procName + ' to public'
        exec sp_executesql @strCMD
    end

    fetch next from curProcs into @procName
end

close curProcs
deallocate curProcs
