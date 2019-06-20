//-----------------------------------------------------------------------------
// FILE:        DtmfType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Categorizes the possible types of DTMF digits.

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
    /// Categorizes the possible types of DTMF digits.
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
    public enum DtmfType
    {
        /// <summary>
        /// <b>0-9</b>
        /// </summary>
        Number,

        /// <summary>
        /// <b>*</b>  and <b>#</b>
        /// </summary>
        Alert,

        /// <summary>
        /// <b>A-D</b>: signalling (or menu) keys.
        /// </summary>
        Signalling
    }
}
