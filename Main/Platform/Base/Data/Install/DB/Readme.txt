Database Folder Structure
-------------------------

This document outlines the folder and file structure for the project's
database.  This structure is designed to make use of the LillTek 
Platform's TSQLPP and DBPACKAGE utilities to pre-process database
script files and also to package them into a database package file
(.dbpack) that will be used to cread or upgrade a database when
the application is installed.

Database scripts are located in the following folders:

    Functions:  Scripts that create SQL functions.
    
    Schema:     Scripts with the DML necessary to create and
                clear a database.  Major files are:
                
                ClearDB.sql:    Clears the database of all objects
                
                Schema.def:     Defines TSQLPP macros as well as
                                globals used by the installer
                                
                Schema.sql:     Creates and initializes the 
                                database tables, views, and indicies.
                                
    Setup:      Holds the Welcome.rtf file which holds the text
                displayed to the user during setup.
                
    SP:         Holds the stored procedure script files.
    
    Upgrade:    Holds any schema upgrade scripts named by
                schema version number.  The installer will
                apply these scripts in order as necessary 
                to bring a database up-to-date.
                
Batch Files
-----------

The enclosing project has a build event that builds the database
package during the normal build process.  The scripts below 
are available for manually performing some of these operations
during the development process.  All of these scripts require
that the project has already been built.

    AddProcs.cmd:       Reloads the stored procedures from the
                        SP folder into a database.l
                    
    DBInstall.cmd:      Builds the database package and then runs
                        the database installer.
                    
    DBPack.cmd:         Builds the database package.
    
    ProcessSchema.cmd:  Pre-processes the files in the Schema folder,
                        copying the results to a temporary folder.
                        This is handy when editing and testing
                        schema changes by hand.
                        
    LinqGen.cmd:        This file will be present only for projects
                        using LINQ-to-SQL.  This regenerates the C#
                        LINQ proxy class files for the project from
                        an existing database.  See the file's comment
                        for more information.