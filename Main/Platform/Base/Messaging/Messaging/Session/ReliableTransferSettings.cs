//-----------------------------------------------------------------------------
// FILE:        ReliableTransferSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the settings used when creating a ReliableTransferSession instance.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the settings used when creating a <see cref="ReliableTransferSession" /> instance.
    /// </summary>
    /// <threadsafety instance="false" />
    public class ReliableTransferSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads and returns a <see cref="ReliableTransferSession" /> instance 
        /// from the application configuration using the specified configuration 
        /// key prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <remarks>
        /// <para>
        /// The <see cref="ReliableTransferSession" /> settings are loaded from the application
        /// configuration, under the specified key prefix.  The following
        /// settings are recognized by the class:
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DefBlockSize</td>
        ///     <td>16K</td>
        ///     <td>
        ///     The default transfer block size in bytes.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxBlockSize</td>
        ///     <td>16K</td>
        ///     <td>
        ///     The maximum transfer block size in bytes.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>RetryWaitTime</td>
        ///     <td>3s</td>
        ///     <td>
        ///     Maximum interval to wait for a response from the other side of the
        ///     reliable transfer session before retrying the operation or failing
        ///     with a <see cref="TimeoutException" />.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxTries</td>
        ///     <td>3</td>
        ///     <td>
        ///     Maximum number of times to try an operation against the other
        ///     side of a session before giving up.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        /// <returns>The <see cref="ReliableTransferSettings" /> instance.</returns>
        public static ReliableTransferSettings LoadConfig(string keyPrefix)
        {
            return new ReliableTransferSettings(keyPrefix);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The default transfer block size.  This defaults to 16K.
        /// </summary>
        public int DefBlockSize = 16 * 1024;

        /// <summary>
        /// The maximum transfer block size.  This defaults to 16K.
        /// </summary>
        public int MaxBlockSize = 16 * 1024;

        /// <summary>
        /// The maximum interval to wait before retrying a reliable transfer operation
        /// over the network.  This is loaded from the application configuration's
        /// <b>MsgRouter.ReliableTransfer.RetryWaitTime</b> setting and defaults to
        /// 3 seconds.
        /// </summary>
        public TimeSpan RetryWaitTime = TimeSpan.FromSeconds(3);

        /// <summary>
        /// The maximum number of times a reliable transfer network operation can
        /// be tried before giving up and throwing a <see cref="TimeoutException" />.
        /// This is loaded from the application configuration's 
        /// <b>MsgRouter.ReliableTransfer.MaxTries</b> setting and defaults to 3.
        /// </summary>
        public int MaxTries = 3;

        /// <summary>
        /// Initializes an instance with reasonable default settings.
        /// </summary>
        public ReliableTransferSettings()
        {
        }

        /// <summary>
        /// Initializes an instance by loading settings from the application
        /// configuration using the specified configuration key prefix.
        /// </summary>
        /// <param name="keyPrefix">The fully qualified configuration key prefix.</param>
        public ReliableTransferSettings(string keyPrefix)
        {
            Config config = new Config(keyPrefix);

            this.DefBlockSize  = config.Get("DefBlockSize", this.DefBlockSize);
            this.MaxBlockSize  = config.Get("MaxBlockSize", this.MaxBlockSize);
            this.RetryWaitTime = config.Get("RetryWaitTime", this.RetryWaitTime);
            this.MaxTries      = config.Get("MaxTries", this.MaxTries);
        }

        /// <summary>
        /// Loads any override settings from a <see cref="ArgCollection" />.
        /// </summary>
        /// <param name="args">The override settings.</param>
        public void LoadCustom(ArgCollection args)
        {
            this.DefBlockSize  = args.Get("DefBlockSize", this.DefBlockSize);
            this.MaxBlockSize  = args.Get("MaxBlockSize", this.MaxBlockSize);
            this.RetryWaitTime = args.Get("RetryWaitTime", this.RetryWaitTime);
            this.MaxTries      = args.Get("MaxTries", this.MaxTries);
        }

        /// <summary>
        /// Returns a shallow clone of the instance.
        /// </summary>
        /// <returns>The clone.</returns>
        public ReliableTransferSettings Clone()
        {
            var clone = new ReliableTransferSettings();

            clone.DefBlockSize  = this.DefBlockSize;
            clone.MaxBlockSize  = this.MaxBlockSize;
            clone.RetryWaitTime = this.RetryWaitTime;
            clone.MaxTries      = this.MaxTries;

            return clone;
        }
    }
}
