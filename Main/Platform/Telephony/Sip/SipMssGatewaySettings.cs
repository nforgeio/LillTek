//-----------------------------------------------------------------------------
// FILE:        SipMssGatewaySettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the settings used to configure a SipMssGateway.

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes the settings used to configure a <see cref="SipMssGateway" />.
    /// </summary>
    public sealed class SipMssGatewaySettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads the settings from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>The loaded <see cref="SipMssGatewaySettings" />.</returns>\
        /// <remarks>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>SpeechServerUri</td>
        ///     <td>(none)</td>
        ///     <td>
        ///     Specifies the <see cref="SipUri" /> for Microsoft Speech Server.
        ///     This setting must be a valid SIP URI.
        ///     <note>
        ///     The <see cref="SipMssGatewaySettings" /> class will automatically add the
        ///     <b>transport=tcp</b> parameter to the URI if no transport parameter is
        ///     specified.
        ///     </note>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>TrunkUri</td>
        ///     <td>(none)</td>
        ///     <td>
        ///     Specifies the <see cref="SipUri" /> for the SIP trunking service.
        ///     This setting must be a valid SIP URI.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Register[#]</td>
        ///     <td>(none)</td>
        ///     <td>
        ///     Specifies zero or more <see cref="SipUri" />s that must be periodically
        ///     re-registered with the SIP trunking service.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static SipMssGatewaySettings LoadConfig(string keyPrefix)
        {
            var         config = new Config(keyPrefix);
            var         settings = new SipMssGatewaySettings();
            string      s;
            string[]    sArr;

            s = config.Get("SpeechServerUri", (string)null);
            try
            {
                if (s == null)
                    throw new ArgumentNullException();

                settings.SpeechServerUri = new SipUri(s);
                if (!settings.SpeechServerUri.Parameters.ContainsKey("transport"))
                    settings.SpeechServerUri.Parameters.Add("transport", "tcp");
            }
            catch
            {
                throw new ArgumentException("MssGateway: Invalid or missing [SpeechServerUri] configuration setting.");
            }

            s = config.Get("TrunkUri", (string)null);
            try
            {
                if (s == null)
                    throw new ArgumentNullException();

                settings.TrunkUri = new SipUri(s);
            }
            catch
            {
                throw new ArgumentException("MssGateway: Invalid or missing [TrunkUri] configuration setting.");
            }

            sArr = config.GetArray("Register");
            if (sArr != null && sArr.Length > 0)
            {
                settings.Register = new SipUri[sArr.Length];
                for (int i = 0; i < sArr.Length; i++)
                {
                    try
                    {
                        settings.Register[i] = new SipUri(sArr[i]);
                    }
                    catch
                    {
                        throw new ArgumentException(string.Format("MssGateway: Invalid [Register[{0}]] configuration setting.", i));
                    }
                }
            }

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Specifies the <see cref="SipUri" /> for Microsoft Speech Server.
        /// </summary>
        public SipUri SpeechServerUri = null;

        /// <summary>
        /// Specifies the <see cref="SipUri" /> for the SIP trunking service.
        /// </summary>
        public SipUri TrunkUri = null;

        /// <summary>
        /// Specifies zero or more <see cref="SipUri" />s that must be periodically
        /// re-registered with the SIP trunking service.
        /// </summary>
        public SipUri[] Register = new SipUri[0];

        /// <summary>
        /// Private default constructor.
        /// </summary>
        private SipMssGatewaySettings()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="speechServerUri">The Microsoft Speech Server <see cref="SipUri" />.</param>
        /// <param name="trunkUri">The SIP trunking service URI <see cref="SipUri" />.</param>
        public SipMssGatewaySettings(SipUri speechServerUri, SipUri trunkUri)
        {
            if (speechServerUri == null)
                throw new ArgumentNullException("speechServerUri");

            if (trunkUri == null)
                throw new ArgumentNullException("trunkUri");

            this.SpeechServerUri = speechServerUri;
            this.TrunkUri        = trunkUri;
        }
    }
}
