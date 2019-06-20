//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace Documentation

using System;

using LillTek.Service;
using LillTek.Messaging;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;

using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Service
{
    /// <summary>
    /// Implements the LillTek GeoTracker Service which provides distributed and scalable
    /// geographical tracking related services for physical entities.
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
    /// The configuration will need to include the appropriate <b>MsgRouter</b> settings
    /// for a <see cref="LeafRouter" /> in addition to the settings described below.
    /// </para>
    /// <para>
    /// See <see cref="GeoTrackerServerSettings" /> for service specific settings.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

