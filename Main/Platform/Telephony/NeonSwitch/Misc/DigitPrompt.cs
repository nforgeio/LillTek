//-----------------------------------------------------------------------------
// FILE:        DigitPrompt.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Parameters used to control the prompting for caller DTMF digit input.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Parameters used to control the prompting for caller DTMF digit input.
    /// </summary>
    public class DigitPrompt
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a regular expression string that matches a single DTMF <b>*</b> key.
        /// </summary>
        public const string StarRegex = @"^\*$";

        /// <summary>
        /// Returns a regular expression string that matches a single DTMF <b>#</b> key.
        /// </summary>
        public const string PoundRegex = @"^#$";

        /// <summary>
        /// Returns a regular expression string that matches a single DTMF  <b>*</b> or <b>#</b> key
        /// (that is, an <see cref="DtmfType.Alert" /> key).
        /// </summary>
        public const string AlertRegex = @"^#|\*$";

        /// <summary>
        /// Returns a regular expression that matches a standard 10-digit phone number.
        /// </summary>
        public const string PhoneRegex = @"^([2-9]\d{2}[2-9]\d{6})$";

        /// <summary>
        /// Returns a regular expression that matches a standard 10-digit phone number
        /// with a one digit prefix.
        /// </summary>
        public const string PhonePlusRegEx = @"^1([2-9]\d{2}[2-9]\d{6})$";

        /// <summary>
        /// Returns a regular expression that matches a standard 10-digit phone number
        /// with an optional one digit prefix.
        /// </summary>
        public const string PhoneOptionRegex = @"^1?([2-9]\d{2}[2-9]\d{6})$";

        /// <summary>
        /// Returns a regular expression that matchies the specifed number of 
        /// <see cref="DtmfType.Number "/> digits.
        /// </summary>
        /// <param name="count">The number of digits.</param>
        /// <returns>The regular expression string.</returns>
        public static string NumberRegex(int count)
        {

            return string.Format(@"^[0-9]{{{0}}}$", count);
        }

        //---------------------------------------------------------------------
        // Instance members

        private AudioSource promptAudio;
        private AudioSource retryAudio;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DigitPrompt()
        {
            this.promptAudio = AudioSource.Silence(TimeSpan.FromMilliseconds(50));
            this.retryAudio  = AudioSource.Silence(TimeSpan.FromMilliseconds(50));
        }

        /// <summary>
        /// The minimum number of DTMF digits to return (defaults to <b>0</b>).
        /// </summary>
        public int MinDigits { get; set; }

        /// <summary>
        /// The maximum number of DTMF digits to return (defaults to <b>100</b>).
        /// </summary>
        public int MaxDigits { get; set; }

        /// <summary>
        /// The maximum number of times to allow the caller to try again (defaults to <b>1</b>).
        /// </summary>
        public int MaxTries { get; set; }

        /// <summary>
        /// The set of DTMF digits that when pressed will immediately terminate the operation
        /// (defaults to <c>null</c>).
        /// </summary>
        public string Terminators { get; set; }

        /// <summary>
        /// Specifies the audio to be played prompting the caller to enter the digits.
        /// This defaults to 50ms of silence.  This may also be set to <c>null</c>.
        /// </summary>
        public AudioSource PromptAudio { get; set; }

        /// <summary>
        /// Specifies the audio to be played when the conditions have not been satisfied
        /// and the caller is asked to try again.  This defaults to 50ms of silence.
        /// This may also be set to <c>null</c>.
        /// </summary>
        public AudioSource RetryAudio { get; set; }

        /// <summary>
        /// The regular expression used to validate that the required digits have been pressed
        /// or <c>null</c> if no matching is to be performed (defaults to <c>null</c>).
        /// </summary>
        /// <remarks>
        /// This class provides some useful constants and methods that provide most
        /// common regular expressions: <see cref="StarRegex" />, <see cref="PoundRegex" />,
        /// <see cref="AlertRegex" />, <see cref="PhoneRegex" />, <see cref="PhonePlusRegEx" />
        /// <see cref="PhoneOptionRegex" />, and <see cref="NumberRegex" />.
        /// </remarks>
        public string DigitsRegex { get; set; }

        /// <summary>
        /// The machime time the caller will be allowed from the end of the prompt
        /// to enter the digits correctly before being asked to retry or the operation
        /// will be aborted (if <see cref="MaxTries" /> has been exceeded.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// The maximum time to wait for the caller to press a digit before the
        /// current attempt is aborted.
        /// </summary>
        public TimeSpan DigitTimeout { get; set; }

        /// <summary>
        /// Generates a <see cref="PromptResponse" /> containing the DTMF digits passed and
        /// also indicating whether the digits satisfied the constrains specified by
        /// this <see cref="DigitPrompt" /> instance.
        /// </summary>
        /// <param name="digits">The received DTMF digits.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="digits" /> is <c>null</c>.</exception>
        internal PromptResponse GetResponse(string digits)
        {
            if (digits == null)
                throw new ArgumentNullException(digits);

            var success = true;

            if (digits.Length < MinDigits || MaxDigits < digits.Length)
                success = false;
            else if (!string.IsNullOrWhiteSpace(DigitsRegex))
                success = new Regex(DigitsRegex).IsMatch(digits);

            return new PromptResponse(success, digits);
        }
    }
}
