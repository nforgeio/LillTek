-------------------------------------------------------------------------------
-- File:        Schema.sql
-- Owner:       JEFFL
-- Copyright:   Copyright (c) 2005-2014 by LillTek, LLC All rights reserved.
-- Description: Initializes the LillTek Property Store database schema.

-- Make sure that we're not modifying the MASTER database

if (db_name() = 'master') raiserror ('WARNING: Cannot modify the MASTER database.',1,1)
go
while (db_name() = 'master') print ' ' 
go

-------------------------------------------------------------------------------
-- Set SIMPLE recovery mode (for non SQL Azure databases)

declare @statement  nvarchar(1024)

if (serverproperty('edition') <> 'SQL Azure') begin

	set @statement = 'alter database ' + db_name() + ' set recovery simple'
	exec sp_executesql @statement
end
go

-------------------------------------------------------------------------------
-- ProductInfo

create table ProductInfo (

	InfoID				$(UID) 			primary key identity,
    ProductName         $(ObjectName),
    ProductID           $(ObjectName),
    ProductVersion      $(Version),
    DatabaseType        $(ObjectName),
    SchemaVersion       $(Version),
    CreatedOn           datetime,
    ModifiedOn          datetime
)
go

insert into ProductInfo(ProductName,ProductID,ProductVersion,DatabaseType,SchemaVersion,CreatedOn,ModifiedOn)
    values('$(ProductName)','$(ProductID)','$(ProductVersion)','$(DatabaseType)','$(SchemaVersion)',GetUtcDate(),GetUtcDate())

-------------------------------------------------------------------------------
-- dbHistory

create table dbHistory (

	HistoryID			$(UID) 			primary key identity,
    ModifiedOn          datetime,
    ModifiedBy          $(UserName),
    Version             $(Version)     null,
    Description         $(Description) null
)
go

declare @ver    $(Version)
declare @user   $(UserName)

select @ver  = (select SchemaVersion from ProductInfo)
select @user = user

insert into dbHistory(Version,ModifiedOn,ModifiedBy,Description)
    values(@ver,GetUtcDate(),@user,'Install')
