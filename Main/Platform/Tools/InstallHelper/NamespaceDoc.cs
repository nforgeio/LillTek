//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace Documentation

using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;

using LillTek.Common;

namespace LillTek.Tools.InstallHelper {

    /// <summary>
    /// InstallHelper application overview.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The InstallHelper tool performs installation related activities
    /// after queued by <see cref="LillTek.Install.InstallTools"/> once
    /// setup has completed.
    /// </para>
    /// <para>
    /// This tool is designed to add functionality that is not
    /// supported by the somewhat brain-dead Visual Studio deployment
    /// project implementation.  The program accepts a set of parameters
    /// on the command line that specify the actions to be performed.
    /// With the exception of the -wait command, the commands will be
    /// executed in the order they appear on the command line.
    /// </para>
    /// <para>
    /// The -wait:[processID] command is special.  If it appears
    /// anywhere on the command line, it indicates that the tool
    /// should wait for the process whose Windows process ID is passed
    /// to exit before executing any of the commands.  Typically,
    /// this will be used to wait for setup to complete and perform
    /// some post setup actions.
    /// </para>
    /// <code language="none">
    /// Commands                Description
    /// ---------------------------------------------------------------
    /// -wait:[processID]       Waits for the specified process to exit
    ///                         before executing any other commands.
    ///                         The ID should be passed as an unsigned
    ///                         decimal integer.
    ///                         
    /// -title:[name]           The title of any subsequent command related 
    ///                         dialogs presented to the user.
    ///                     
    /// -start:[serviceName]    Starts the named Windows service.
    /// 
    /// -configdb:[args]        Starts the DBPackage.exe tool to peform
    ///                         a database configuration, passing [args]
    ///                         as the command line arguments.
    ///
    /// -regeventsource:[name]  Registers a Native Windows application 
    ///                         event source if one doesn't already 
    ///                         exist.
    /// </code>
    /// </remarks>
    /// <seealso cref="LillTek.Install.InstallTools"/>
    public static class OverviewDoc {

    }
}