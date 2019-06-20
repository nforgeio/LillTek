//-----------------------------------------------------------------------------
// FILE:        PromptResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the caller's response to a prompt operation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Describes the caller's response to a prompt operation.
    /// </summary>
    public struct PromptResponse
    {
        /// <summary>
        /// Indicates that the caller entered a valid response.
        /// </summary>
        public readonly bool Success;

        /// <summary>
        /// The DTMF digits entered by the user (even if they were not valid).
        /// </summary>
        public readonly string Digits;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="success">The success code.</param>
        /// <param name="digits">The received DTMF digits.</param>
        internal PromptResponse(bool success, string digits)
        {
            this.Success = success;
            this.Digits  = digits;
        }
    }
}
