//-----------------------------------------------------------------------------
// FILE:        SipMaxForwardsValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP Max-Forwards header value.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP <b>Max-Forwards</b> header value.
    /// </summary>
    public sealed class SipMaxForwardsValue : SipValue
    {
        /// <summary>
        /// Implicit cast of a <see cref="SipHeader" />'s first value into a 
        /// <see cref="SipMaxForwardsValue" />.
        /// </summary>
        /// <param name="header">The source header <see cref="SipHeader" />.</param>
        /// <returns>The parsed <see cref="SipMaxForwardsValue" />.</returns>
        public static implicit operator SipMaxForwardsValue(SipHeader header)
        {
            return new SipMaxForwardsValue(header.Text);
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipMaxForwardsValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipMaxForwardsValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipMaxForwardsValue(string rawText)
        {
            return new SipMaxForwardsValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        private int count = 70;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipMaxForwardsValue()
        {
        }

        /// <summary>
        /// Constructs a <b>Max-Forwards</b> header from a method and sequence number.
        /// </summary>
        /// <param name="count">The count.</param>
        public SipMaxForwardsValue(int count)
            : base()
        {
            this.count = count;

            SetText();
        }

        /// <summary>
        /// Parses a <b>Max-Forwards</b> header from the raw header text.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        public SipMaxForwardsValue(string rawText)
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

            if (!int.TryParse(base.Text, out count))
                throw new SipException("Invalid [Max-Forwards] header.");
        }

        /// <summary>
        /// Sets the base class Text property.
        /// </summary>
        private void SetText()
        {
            base.Text = count.ToString();
        }

        /// <summary>
        /// The maximum remaining times the message may be forwarded.
        /// </summary>
        public int Count
        {
            get { return count; }

            set
            {
                count = value;
                SetText();
            }
        }
    }
}
