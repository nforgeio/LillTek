//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace Documentation

using System;

using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Service;

namespace LillTek.Datacenter.RouterService
{
    /// <summary>
    /// Hosts a root or hub router as a service.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Command Line Parameters</u></b></para>
    /// <para>
    /// This application uses the LillTek <see cref="ServiceHost" /> class so that
    /// the application can launch as a Windows Form, a console application, or a
    /// Windows Service based on the <b>-mode</b> command line parameter.  Pass no
    /// <b>-mode</b> parameter to start the application as a Windows service.
    /// Pass <b>-mode:form</b> to start as a Windows Form application and <b>-mode:console</b>
    /// to start as a console application.
    /// </para>
    /// <para>
    /// The optional <b>-start</b> argument is recognized when the application is started
    /// as a Windows Form.  If present, this argument causes the service to start immediately.
    /// If not present, then the application will wait for the user to click the start
    /// button.  This parameter is ignored when the application is launched as a console
    /// or Windows Service with the service always being started immediately.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// The service starts a hub or root router based on the <b>LillTek.RouterService.Mode</b>
    /// configuration setting.  Specify <b>ROOT</b> or <b>HUB</b>.  The service will start
    /// as a hub if no mode is specified.
    /// </para>
    /// <para>
    /// The configuration will need to specify the configuration settings required by the
    /// mode specified.  See <see cref="RootRouter" /> for the settings required for a
    /// root router and <see cref="HubRouter" /> for the hub router configuration settings.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

