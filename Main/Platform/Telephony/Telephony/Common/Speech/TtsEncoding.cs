//-----------------------------------------------------------------------------
// FILE:        TtsEncoding.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible text-to-speech audio encodings.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Enumerates the possible text-to-speech audio encodings.
    /// </summary>
    public enum TtsEncoding
    {
        /// <summary>
        /// 8-bit signed pulse-code modulation.  See <a href="http://en.wikipedia.org/wiki/PCM">Wikipedia.org</a>.
        /// </summary>
        Pcm8,

        /// <summary>
        /// 16-bit signed pulse-code modulation.  See <a href="http://en.wikipedia.org/wiki/PCM">Wikipedia.org</a>.
        /// </summary>
        Pcm16,

        /// <summary>
        /// Telephony encoding in North America and Japan.  See <a href="http://en.wikipedia.org/wiki/Ulaw">Wikipedia.org</a>.
        /// </summary>
        Ulaw,

        /// <summary>
        /// Telephony encoding in Europe.  See <a href="http://en.wikipedia.org/wiki/A-law_algorithm">Wikipedia.org</a>.
        /// </summary>
        Alaw
    }
}
