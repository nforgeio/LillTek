//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace Documentation

using System;

using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Net.Broadcast;
using LillTek.Service;

namespace LillTek.Datacenter.BroadcastServer
{
    /// <summary>
    /// Implements the UDP Broadcast Server to provide broadcast services when running
    /// on networks that don't support UDP multicast or broadcast (such as Windows Azure
    /// and Amazon AWS).  Applications can use the <see cref="UdpBroadcastClient" />
    /// class to register with one or more broadcast servers and the servers will handle
    /// the broadcasting of UDP messages between the registered clients.
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
    /// The server retrieves it settings from the <b>LillTek.Datacenter.BroadcastServer</b>
    /// section of the application configuration.  See <see cref="UdpBroadcastServerSettings" /> 
    /// for documentation for these settings.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

