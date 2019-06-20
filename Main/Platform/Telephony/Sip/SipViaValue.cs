//-----------------------------------------------------------------------------
// FILE:        SipViaValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP Via header value.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP <b>Via</b> header value.
    /// </summary>
    public sealed class SipViaValue : SipValue
    {
        //---------------------------------------------------------------------
        // Static members

        private static char[] whitespace = new char[] { ' ', '\t' };

        /// <summary>
        /// Implicit cast of a <see cref="SipHeader" />'s first value into a 
        /// <see cref="SipViaValue" />.
        /// </summary>
        /// <param name="header">The source header <see cref="SipHeader" />.</param>
        /// <returns>The parsed <see cref="SipViaValue" />.</returns>
        public static implicit operator SipViaValue(SipHeader header)
        {
            return new SipViaValue(header.Text);
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipViaValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipViaValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipViaValue(string rawText)
        {
            return new SipViaValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        private SipTransportType    transportType = SipTransportType.UDP;
        private string              version       = "SIP/2.0";
        private string              sentBy        = string.Empty;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipViaValue()
        {
        }

        /// <summary>
        /// Constructs a <b>Via</b> header from a transport type and sent-by value.
        /// </summary>
        /// <param name="transportType">The transport type.</param>
        /// <param name="sentBy">The sent-by value.</param>
        public SipViaValue(SipTransportType transportType, string sentBy)
            : base()
        {
            this.transportType = transportType;
            this.sentBy        = sentBy;

            SetText();
        }

        /// <summary>
        /// Parses a <b>Via</b> header from the raw header text.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        public SipViaValue(string rawText)
            : base(rawText)
        {
            Parse(rawText);
        }

        /// <summary>
        /// Parses the value from the text passed.
        /// </summary>
        /// <param name="rawText">The input text.</param>
        internal override void Parse(string rawText)
        {
            base.Parse(rawText);

            int         p, pEnd;
            string      v;

            // I'm going to tolerate badly formatted Via headers.

            p = rawText.IndexOf('/');
            if (p == -1)
                return;

            p = rawText.IndexOf('/', p + 1);
            if (p == -1)
                return;

            version = rawText.Substring(0, p).ToUpper();
            p++;

            pEnd = rawText.IndexOfAny(whitespace, p);
            if (pEnd == -1)
                return;

            v = rawText.Substring(p, pEnd - p).Trim().ToUpper();
            switch (v)
            {
                case "UDP": transportType = SipTransportType.UDP; break;
                case "TCP": transportType = SipTransportType.TCP; break;
                case "TLS": transportType = SipTransportType.TLS; break;

                default:

                    return;
            }

            p = pEnd + 1;
            pEnd = rawText.IndexOf(';');
            if (pEnd == -1)
                sentBy = rawText.Substring(p).Trim();
            else
                sentBy = rawText.Substring(p, pEnd - p).Trim();
        }

        /// <summary>
        /// Sets the base class Text property.
        /// </summary>
        private void SetText()
        {
            base.Text = string.Format("{0}/{1} {2}", version, transportType.ToString().ToUpper(), sentBy);
        }

        /// <summary>
        /// The SIP version field.
        /// </summary>
        public string Version
        {
            get { return version; }

            set
            {
                this.version = value;
                SetText();
            }
        }

        /// <summary>
        /// The <b>transport type</b> field.
        /// </summary>
        public SipTransportType TransportType
        {
            get { return transportType; }

            set
            {

                this.transportType = value;
                SetText();
            }
        }

        /// <summary>
        /// The <b>sent-by</b> field.
        /// </summary>
        public string SentBy
        {

            get { return sentBy; }

            set
            {
                this.sentBy = value;
                SetText();
            }
        }

        /// <summary>
        /// Returns the <b>sent-by</b> field as a <see cref="NetworkBinding" />
        /// instance or <c>null</c> if the field couldn't be parsed.
        /// </summary>
        public NetworkBinding SentByBinding
        {
            get
            {
                NetworkBinding  binding;
                string          s;

                s = sentBy;
                if (s.IndexOf(':') == -1)
                {
                    switch (transportType)
                    {
                        case SipTransportType.UDP:
                        case SipTransportType.TCP:

                            s += ":5060";
                            break;

                        case SipTransportType.TLS:

                            s += ":5061";
                            break;
                    }
                }

                if (!NetworkBinding.TryParse(s, out binding))
                    return null;

                return binding;
            }
        }

        /// <summary>
        /// The <b>branch</b> parameter.
        /// </summary>
        public string Branch
        {
            get { return base["branch"]; }
            set { base["branch"] = value; }
        }

        /// <summary>
        /// The <b>maddr</b> parameter.
        /// </summary>
        public string MAddr
        {
            get { return base["maddr"]; }
            set { base["maddr"] = value; }
        }

        /// <summary>
        /// The <b>received</b> parameter.
        /// </summary>
        public string Received
        {
            get { return base["received"]; }
            set { base["received"] = value; }
        }

        /// <summary>
        /// The <b>rport</b> parameter.
        /// </summary>
        public string RPort
        {
            get { return base["rport"]; }
            set { base["rport"] = value; }
        }
    }
}
