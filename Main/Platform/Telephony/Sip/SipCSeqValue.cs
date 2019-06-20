//-----------------------------------------------------------------------------
// FILE:        SipCSeqValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP CSeq header value.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP <b>CSeq</b> header value.
    /// </summary>
    public sealed class SipCSeqValue : SipValue
    {
        /// <summary>
        /// Implicit cast of a <see cref="SipHeader" />'s first value into a 
        /// <see cref="SipCSeqValue" />.
        /// </summary>
        /// <param name="header">The source header <see cref="SipHeader" />.</param>
        /// <returns>The parsed <see cref="SipCSeqValue" />.</returns>
        public static implicit operator SipCSeqValue(SipHeader header)
        {
            return new SipCSeqValue(header.Text);
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipCSeqValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipCSeqValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipCSeqValue(string rawText)
        {
            return new SipCSeqValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string  method = string.Empty;
        private int     number = 0;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipCSeqValue()
        {
        }

        /// <summary>
        /// Constructs a <b>CSeq</b> header from a method and sequence number.
        /// </summary>
        /// <param name="number">The sequence number.</param>
        /// <param name="method">The method.</param>
        public SipCSeqValue(int number, string method)
            : base()
        {
            this.number = number;
            this.method = method;

            SetText();
        }

        /// <summary>
        /// Constructs a <b>CSeq</b> header from a method and sequence number.
        /// </summary>
        /// <param name="number">The sequence number.</param>
        /// <param name="method">The method.</param>
        public SipCSeqValue(int number, SipMethod method)
            : base()
        {
            this.number = number;
            this.method = method.ToString();

            SetText();
        }

        /// <summary>
        /// Parses a <b>CSeq</b> header from the raw header text.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        public SipCSeqValue(string rawText)
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

            string[] fields;

            fields = base.Text.Split(' ');
            if (fields.Length != 2)
                throw new SipException("Invalid [CSEQ] header.");

            int.TryParse(fields[0], out number);
            method = fields[1];
        }

        /// <summary>
        /// Sets the base class Text property.
        /// </summary>
        private void SetText()
        {
            base.Text = string.Format("{0} {1}", number, method);
        }

        /// <summary>
        /// The sequence number.
        /// </summary>
        public int Number
        {
            get { return number; }

            set
            {
                number = value;
                SetText();
            }
        }

        /// <summary>
        /// The method.
        /// </summary>
        public string Method
        {
            get { return method; }

            set
            {
                method = value;
                SetText();
            }
        }
    }
}
