//-----------------------------------------------------------------------------
// FILE:        PhraseType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible Phrase types.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Enumerates the possible <see cref="Phrase" /> types.
    /// </summary>
    public enum PhraseType
    {
        /// <summary>
        /// The phrase type is not known.
        /// </summary>
        Unknown,

        /// <summary>
        /// The phrase is simple text.
        /// </summary>
        Text,

        /// <summary>
        /// The phrase is Microsoft compatible Speech Synthesis Markup Language (SSML).
        /// See <a href="http://en.wikipedia.org/wiki/Speech_Synthesis_Markup_Language">Wikipedia.org</a>
        /// for more information.
        /// </summary>
        Ssml
    }
}
