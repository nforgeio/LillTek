//-----------------------------------------------------------------------------
// FILE:        Dtmf.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: DTMF related constants and utilities.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// DTMF related constants and utilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dual-tone multi-frequency (DTMF) capabilities was first deployed to telephone
    /// networks by AT&amp;T in the early 1960s, known as <b>Touch-Tone</b>.  The standard
    /// defined sixteen possible keys including the numeric digits <b>0-9</b>, the <b>*</b>
    /// and <b>#</b> keys and four menu keys <b>A-D</b>.  The menu keys were never widely 
    /// deployed on phone handsets and over time were used internally by the telephone 
    /// networks for signalling purposes.  NeonSwitch categorizes DTMF digits into three
    /// groups: <see cref="DtmfType.Number" />, <see cref="DtmfType.Alert" />, and 
    /// <see cref="DtmfType.Signalling" />.
    /// </para>
    /// <para>
    /// <see cref="DtmfType.Number" /> keys are typically used for chosing from a selection
    /// menus and entering for entering phone numbers and other information.  
    /// </para>
    /// <para>
    /// <see cref="DtmfType.Alert" /> keys are typically used for indicating that the caller
    /// has completed entering a response or to alert the application to stop what its
    /// doing (e.g. speaking a prompt) or indicate that the caller wants some service.  
    /// </para>
    /// <para>
    /// <see cref="DtmfType.Signalling" /> keys are not typically used for caller interaction 
    /// but may be useful in some situations for switches and/or endpoints to communicate amonsgt
    /// themselves.
    /// </para>
    /// <note>
    /// Many (perhaps all) public telephone networks will block or ignore signalling DTMF 
    /// tones from normal endpoints.
    /// </note>
    /// <para>
    /// Check out <a href="http://en.wikipedia.org/wiki/Dtmf">Wikipedia.org</a> for more about DTMF.
    /// </para>
    /// </remarks>
    public static class Dtmf
    {
        /// <summary>
        /// The DTMF alert digits.
        /// </summary>
        public const string AlertDigits = "*#";

        /// <summary>
        /// The DTMF numeric digits.
        /// </summary>
        public const string NumericDigits = "0123456789";

        /// <summary>
        /// The DTMF signalling digits.
        /// </summary>
        public const string SignallingDigits = "abcd";

        /// <summary>
        /// All possible DTMF digits.
        /// </summary>
        public const string AllDigits = "0123456789*#abcd";

        /// <summary>
        /// Determines whether a character is a valid DTMF digit: <b>0-9</b>, <b>a-d</b> <b>*</b>, and <b>#</b>.
        /// </summary>
        /// <param name="ch">The character being tested.</param>
        /// <returns><c>true</c> for valid DTMF digits.</returns>
        /// <remarks>
        /// Valid DTMF digits include characters in the range of <b>0..9</b>,
        /// <b>a..d</b>, <b>A..D</b>, and the <b>*</b> or <b>#</b> characters.
        /// </remarks>
        public static bool IsDtmf(char ch)
        {
            return ('0' <= ch && ch <= '9') ||
                   ('a' <= ch && ch <= 'd') ||
                   ('A' <= ch && ch <= 'D') ||
                   ch == '*' ||
                   ch == '#';
        }

        /// <summary>
        /// Returns the <see cref="DtmfType" /> for an individual DTMF digit.
        /// </summary>
        /// <param name="ch">The character being tested.</param>
        /// <returns>The <see cref="DtmfType" />.</returns>
        /// <exception cref="ArgumentException">Thrown if the character is not a valid DTMF digit.</exception>
        public static DtmfType Type(char ch)
        {
            if ('0' <= ch && ch <= '9')
                return DtmfType.Number;
            else if (('a' <= ch && ch <= 'd') || ('A' <= ch && ch <= 'D'))
                return DtmfType.Signalling;
            else if (ch == '*' || ch == '#')
                return DtmfType.Alert;
            else
                throw new ArgumentException(string.Format("[{0}] is not a valid DTMF digit.", ch), "ch");
        }

        /// <summary>
        /// Verifies that a string contains only valid DTMF digits: <b>0-9</b>, <b>a-d</b> <b>*</b>, and <b>#</b>.
        /// </summary>
        /// <param name="dtmfDigits">The DTMF digits.</param>
        /// <returns>The DTMF digits.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dtmfDigits"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the string contains non-DTMF digits.</exception>
        public static string Validate(string dtmfDigits)
        {
            if (dtmfDigits == null)
                throw new ArgumentNullException("dtmfDigits");

            foreach (var ch in dtmfDigits)
                if (!IsDtmf(ch))
                    throw new ArgumentException(string.Format("DTMF string [{0}] contains one or more invalid DTMF digits.", dtmfDigits), "dtmfDigits");

            return dtmfDigits;
        }

        /// <summary>
        /// Verfies that a string consists solely of numeric digits with the specified constraints.
        /// </summary>
        /// <param name="digits">The digit string.</param>
        /// <param name="minCount">The minimum number of digits.</param>
        /// <param name="maxCount">The maximum number of digits.</param>
        /// <param name="allowNull">Pass <c>true</c> to allow <c>null</c> if <b>minDigits=0</b>.</param>
        /// <returns>The DTMF digits,</returns>
        /// <exception cref="ArgumentException">Thrown if the digits are not valid.</exception>
        public static string ValidateNumeric(string digits, int minCount, int maxCount, bool allowNull)
        {
            if (digits == null)
            {
                if (!allowNull)
                    throw new ArgumentException("NULL digit string is not allowed.");

                if (minCount == 0)
                    return digits;

                throw new ArgumentException(string.Format("[{0}] digits present.  Minimum of [{1}] digits required.", 0, minCount));
            }

            for (int i = 0; i < digits.Length; i++)
                if (!Char.IsDigit(digits[i]))
                    throw new ArgumentException(string.Format("[{0}] contains non-numeric characters.", digits));

            if (digits.Length < minCount)
                throw new ArgumentException(string.Format("[{0}] digits present.  Minimum of [{1}] digits required.", digits.Length, minCount));
            else if (digits.Length > maxCount)
                throw new ArgumentException(string.Format("[{0}] digits present.  Maximum of [{1}] digits allowed.", digits.Length, maxCount));

            return digits;
        }
    }
}
