//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Namespace Documentation

using System;
using System.IO;
using System.Drawing;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data.Install;

namespace LillTek.Tools.DBPackage
{
    /// <summary>
    /// Database installation package creation and installation tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This application has two basic mode: create mode and install mode.
    /// In create mode, the tool creates a database package.  Install mode
    /// is used to apply schema packages to a database during application
    /// install.
    /// </para>
    /// <para>
    /// The application mode is selected by passing either the -create
    /// or -install option on the command line.  If neither are present
    /// then -create will be assumed.  Each mode defines additional
    /// command line parameters.
    /// </para>
    /// <code language="none">
    /// Command Line Option     Description
    /// ---------------------------------------------------------------
    /// -create                 Puts the tool into Create mode
    /// -install:[package]      Puts the tool into Install mode,
    ///                         installing the database package whose
    ///                         file path is specified.
    ///                         
    /// Create Options:
    /// ---------------
    /// 
    /// -setup:[file]           Names the file holding the database
    ///                         package setup.ini information.
    ///                         
    /// -welcome:[RTF file]     Names the RTF file with the welcome
    ///                         text (optional).
    /// 
    /// -upgrade:[directory]    Specifies the directory holding the SQL
    ///                         database schema upgrade scripts.
    /// 
    /// -schema:[directory]     Specifies the directory holding the
    ///                         schema related scripts.
    /// 
    /// -funcs:[directory]      Specifies the directory holding the
    ///                         database function creation scripts.
    /// 
    /// -procs:[directory]      Specifies the directory holding the
    ///                         database stored procedure creation
    ///                         scripts.
    ///                         
    /// -out:[package]          Specifies where the new package should
    ///                         be saved.
    /// 
    /// Install Options
    /// ---------------
    /// 
    /// -config:[file]          Names the configuration file to be updated
    ///                         with the new database connection string
    ///                         
    /// -setting:[key]          The fully qualified configuration key for
    ///                         the database connection string.
    ///                         
    /// -defdb:[name]           The default database name to use if not
    ///                         already specified in the configuration file.
    /// </code>
    /// <para>
    /// Form more information on how these modes function see 
    /// <see cref="LillTek.Data.Install.DBPackageInstaller">DBPackagerInstaller</see>
    /// and <see cref="LillTek.Data.Install.DBPackageInstaller">DBPackagerBuilder</see>.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

