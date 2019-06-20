//-----------------------------------------------------------------------------
// FILE:        IConfigProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines an interface the Config class can use to dynamically
//              retrieve configuration information.

using System;
using System.Reflection;

namespace LillTek.Common
{
    /// <summary>
    /// Defines an interface the <see cref="Config" /> class can use to dynamically 
    /// retrieve configuration information.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic idea here is to provide a reasonably flexible mechanism for centralizing
    /// application configuration, where different configuration information can be
    /// returned based on the application's executable file name, the executable version,
    /// the specific server the application is running on and finally, the use to
    /// which the application is being put.
    /// </para>
    /// <para>
    /// The <see cref="GetConfig" /> method is implemented by each configuration
    /// provider to return the textual (.ini file) representation of configuration information
    /// as described in <see cref="Config" />.  The settings parameter specify the provider
    /// specific name/value parameters.  The cacheFile parameter specifies the name of the
    /// file where the provider can cache settings, and the remaining parameters are used to 
    /// describe the configuration to be returned.  Implementations of IConfigProvider are free to 
    /// ignore or interpret these parameters as they will.
    /// </para>
    /// </remarks>
    public interface IConfigProvider
    {
        /// <summary>
        /// Returns configuration information formatted as an .ini file as
        /// specified by <see cref="Config" />.
        /// </summary>
        /// <param name="settings">Provider specific settings.</param>
        /// <param name="cacheFile">File path to use for cached settings.</param>
        /// <param name="machineName">The requesting machine's name.</param>
        /// <param name="exeFile">The unqualified name of the current process' executable file.</param>
        /// <param name="exeVersion">The version number of the current executable file.</param>
        /// <param name="usage">Used to indicate a non-default usage for this application instance.</param>
        /// <returns>The configuration file data (or <c>null</c>).</returns>
        /// <remarks>
        /// <para>
        /// The cacheFile parameter specifies the name of the file where the provider
        /// may choose to cache configuration settings.  Some providers will save a copy
        /// of the last set of settings returned by a remote service in this file and
        /// then use these settings if the remote service is unavailable the next time
        /// the service is started.  This improves the operational robustness of a
        /// large network of servers.  Choosing to implement this behavior is completely
        /// up to the discretion of the provider.  Specify <b>no-cache</b> to disable
        /// caching behavior.
        /// </para>
        /// <note>
        /// The method will return null if the requested configuration
        /// information could not be loaded for any reason.
        /// </note>
        /// </remarks>
        string GetConfig(ArgCollection settings, string cacheFile, string machineName, string exeFile, Version exeVersion, string usage);
    }
}
