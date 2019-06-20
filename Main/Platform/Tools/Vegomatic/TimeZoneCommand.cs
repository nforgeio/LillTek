//-----------------------------------------------------------------------------
// FILE:        TimeZone.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the TIMEZONE commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the TIMEZONE commands.
    /// </summary>
    public static class TimeZoneCommand
    {
        /// <summary>
        /// Executes the specified TIMEZONE command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic timezone list [-db]

Writes serialized timezone information out to standard output for
all timezones known to the current computer.  A CSV table will be
written by default.  

    -db         generate the output as TSQL suitable for inserting 
                into a database table.

    -dbupgrade  generates a TSQL script for upgrading a database
                table in-place, adding the timezone ID while
                trying to maintain existing foreign key references.
                This is a one-time hack.

The serialized output is compatible with the .NET TimeZoneInfo class.
";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "list":

                    return ListZones(args);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int ListZones(string[] args)
        {
            bool db        = args.Length >= 2 && String.Compare(args[1], "-db", true) == 0;
            bool dbUpgrade = args.Length >= 2 && String.Compare(args[1], "-dbupgrade", true) == 0;

            // Note that one obscure timezone has an ID and display name with a single 
            // quote (') that causes problems in the TSQL script.  I'm going to correct 
            // this by replacing the quote with a dash.

            if (db)
            {
                foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
                    Console.WriteLine("insert into PortalTimeZones(ID,DisplayName,TimeZoneInfo) values('{0}','{1}','{2}')",
                                      zone.Id.Replace("'", "-"),
                                      zone.DisplayName.Replace("'", "-"),
                                      zone.ToSerializedString().Replace("'", "-"));
            }
            else if (dbUpgrade)
            {
                // This is a one-time hack to deal with the fact that I mistakenly used
                // the time zone DisplayName as the unique identifier in the Paraworks
                // time zone table.  I need to add an ID column instead and use this.
                // The problem is that existing organizations will have references to
                // this table (probably only to the PST zone) and I'd like to maintain
                // these references.
                //
                // The script generated below performs the following steps:
                //
                //      1. Adds the ID and Upgraded columns to the PortalTimeZone
                //         table, initializing ID=' ' and Upgraded=0.
                //
                //      2. For each Windows time zone, code will be generated that
                //         looks for an exact match in the database based on
                //         the display name.  If a match is found, the ID column
                //         will be set to the time zone ID and Upgraded will 
                //         be set to 1.  If no match is found, then a new
                //         time zone row will be added.
                //
                //      3. Any rows with Upgraded=0 will be deleted.
                //
                //      4. The Upgraded column will be removed.
                //
                //      5. Create an index on the ID column.

                Console.WriteLine("alter table PortalTimeZones");
                Console.WriteLine("    add ID $(ObjectName) not null default ' '");
                Console.WriteLine("alter table PortalTimeZones");
                Console.WriteLine("    add Upgraded bit");
                Console.WriteLine("go");
                Console.WriteLine();
                Console.WriteLine("update PortalTimeZones set Upgraded=0");
                Console.WriteLine("go");
                Console.WriteLine();

                foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
                {
                    string id          = "'" + zone.Id.Replace("'", "-") + "'";
                    string displayName = "'" + zone.DisplayName.Replace("'", "-") + "'";
                    string data        = "'" + zone.ToSerializedString().Replace("'", "-") + "'";
                    string script      =
@"if exists(select 1 from PortalTimeZones where DisplayName={1})
    update PortalTimeZones
        set ID={0}, DisplayName={1}, Upgraded=1
        where DisplayName={1}
else
    insert into PortalTimeZones(ID,DisplayName,TimeZoneInfo,Upgraded) values({0},{1},{2},1)
";
                    Console.WriteLine(script, id, displayName, data);
                }

                Console.WriteLine("go");
                Console.WriteLine();

                Console.WriteLine("delete from PortalTimeZones where Upgraded=0");
                Console.WriteLine("go");
                Console.WriteLine();
                Console.WriteLine("alter table PortalTimeZones");
                Console.WriteLine("    drop column Upgraded");
                Console.WriteLine("go");
                Console.WriteLine();
                Console.WriteLine("create unique index ID on PortalTimeZones(ID)");
                Console.WriteLine("go");
            }
            else
            {
                using (var writer = new CsvTableWriter(new string[] { "ID", "DisplayName", "Data" }, Console.OpenStandardOutput(), Helper.AnsiEncoding))
                {
                    foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
                    {
                        writer.Set("ID", zone.Id);
                        writer.Set("DisplayName", zone.DisplayName.Replace("'", "-"));
                        writer.Set("Data", zone.ToSerializedString().Replace("'", "-"));
                        writer.WriteRow();
                    }
                }
            }

            return 0;
        }
    }
}
