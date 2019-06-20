-------------------------------------------------------------------------------
-- File:        SetProductInfo.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC. All rights reserved.
--
--              Based on code and/or techniques from the LillTek Platform.

if (exists (select 1 from sysobjects where xtype='P' and name='SetProductInfo'))
    drop procedure SetProductInfo
go

-------------------------------------------------------------------------------
-- Updates the current database schema versions and adds a record to the
-- database history table.
--
-- Parameters: 
--
--      @productName        - Name of the product
--      @productID          - Globally unique product ID
--      @productVersion     - Product version
--      @databaseType       - Describes the database purpose
--      @schemaVersion:     - The current database schema version number
--      @description        - Textual description of the change
--
-- Result Set #1:
--
--      * A standard StdError response.

create procedure SetProductInfo
    @productName        $(ObjectName),
    @productID          $(ObjectName),
    @productVersion     $(Version),
    @databaseType       $(ObjectName),
    @schemaVersion      $(Version),
    @description        $(Description) = null
as
    set nocount on
    set transaction isolation level serializable
    
    declare @now    datetime
    declare @user   $(UserName)
    
    select @now  = GetUtcDate()
    select @user = user
    
    $(transaction)
    
        delete from ProductInfo
        insert into ProductInfo(ProductName,ProductID,ProductVersion,DatabaseType,SchemaVersion,ModifiedOn)
            values(@productName,@productID,@productVersion,@databaseType,@schemaVersion,@now)
        
        insert into dbHistory(Version,ModifiedOn,ModifiedBy,Description)
            values(@schemaVersion,@now,@user,@description)
    
    $(commit)
        
    exec StdSuccess
    return $(Success)
go
