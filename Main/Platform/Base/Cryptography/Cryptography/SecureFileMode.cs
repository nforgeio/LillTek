//-----------------------------------------------------------------------------
// FILE:        SecureFileMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to indicate whether a secure file is being opened for 
//              encrypting or decrypting.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Used to indicate whether a secure file is being opened for 
    /// encrypting or decrypting.
    /// </summary>
    public enum SecureFileMode
    {
        /// <summary>
        /// Indicates that the file is being opened for encrypting (writing).
        /// </summary>
        Encrypt,

        /// <summary>
        /// Indicates that the file is being opened for decrypting (reading).
        /// </summary>
        Decrypt
    }
}
