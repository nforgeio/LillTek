//-----------------------------------------------------------------------------
// FILE:        TtsEncoding.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible text-to-speech audio sampling rates.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes the supported NeonSwitch text-to-speech audio sampling rates.
    /// </summary>
    public enum TtsSampleRate
    {
        /// <summary>
        /// 8,000 samples per second.
        /// </summary>
        KHz_8000,

        /// <summary>
        /// 11,025 samples per second.
        /// </summary>
        KHz_11025,

        /// <summary>
        /// 16,000 samples per second.
        /// </summary>
        KHz_16000
    }
}
