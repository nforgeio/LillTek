//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace documentation.

using System;
using System.IO;
using System.Text;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Tools.TSQLPP {

    /// <summary>
    /// The T-SQL script preprocessor implements macro substitution, constant
    /// imports from assemblies, and a simple nested transaction syntax.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This application preprocesses TSQL files in preparation for loading
    /// them into a database.  The application deletes all files in the output
    /// directory and then copies the source files to this directory, performing
    /// any necessary preprocessing of the files.
    /// </para>
    /// <code language="none">
    /// Usage: TSQLPP -in:&lt;filespec&gt; -out:&lt;path&gt; [-trans] [-sym:&lt;files&gt;] [-def:&lt;file&gt;]
    /// 
    ///      -trans  Replaces $(transaction), $(commit), and $(rollback)
    ///              markers with code that implements well-behaved
    ///              nested transactions
    ///
    ///      -sym    This option may be used to load class constants and 
    ///              enumeration types marked with [TSQLPPAttribute] from the 
    ///              assembly file specified.
    ///
    ///              TSQLPP will then replace any strings of the form 
    ///              $(type.name) in the source files with the corresponding
    ///              integer value for enumerations and public constants
    ///              from classes rendered as strings.
    ///              
    ///              Note that multiple assemblies can be loaded by adding them
    ///              all paths to the -sym options and separated by commas.
    ///
    ///      -def    This option may be used to load macro definitions from
    ///              a file.  These macros are defined via lines of the form
    ///              
    ///                      name = value
    ///
    ///              The preprocessor will substitute the value for any 
    ///              constructs of the form $(name) found in the source
    ///              T-SQL file.  Lines beginning with "--" or "//" are
    ///              considered to be comments and are ignored.  Note that 
    ///              this is super simple.  There is no recursion and name 
    ///              lookups are case sensitive.
    ///
    ///      -in     Specifies the input file specification.  This can be the
    ///              path to a specific file or a file specification with
    ///              wildcards.
    ///
    ///      -out    Specifies the directory where the output files
    ///              should be written.  All files in this directory will
    ///              be deleted before preprocessing begins.  This directory
    ///              will be created if necessary.
    /// </code>
    /// </remarks>
    public static class OverviewDoc {

    }
}

