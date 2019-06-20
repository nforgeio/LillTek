//-----------------------------------------------------------------------------
// FILE:        HexDumpOption.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Option flags for the Helper.HexDump() method.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Option flags for the <see cref="Helper.HexDump(byte[],int,int,int,HexDumpOption)"/> 
    /// and <see cref="Helper.HexDump(byte[],int,HexDumpOption)"/> methods.
    /// </summary>
    [Flags]
    public enum HexDumpOption
    {
        /// <summary>
        /// Enable no special formatting options.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Enables all formatting options.
        /// </summary>
        ShowAll = 0x7F,

        /// <summary>
        /// Include ANSI characters after the HEX bytes on each line.
        /// </summary>
        ShowAnsi = 0x01,

        /// <summary>
        /// Include the byte offset of the first byte of each line.
        /// </summary>
        ShowOffsets = 0x02,

        /// <summary>
        /// Format as a HTML fragment.
        /// </summary>
        FormatHTML = 0x80
    }
}
