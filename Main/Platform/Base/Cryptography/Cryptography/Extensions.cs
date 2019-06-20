//-----------------------------------------------------------------------------
// FILE:        Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Cryptography related extension methods.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Cryptography related extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Extends <see cref="ArgCollection" /> by adding a method to set an encrypted string value.
        /// </summary>
        /// <param name="args">The current collection.</param>
        /// <param name="name">The argument name.</param>
        /// <param name="value">The plain text value.</param>
        /// <param name="key">The symmetric encryption key.</param>
        public static void SetEncrypted(this ArgCollection args, string name, string value, SymmetricKey key)
        {
            if (value == null)
                args.Set(name, (byte[])null);
            else
                args.Set(name, Crypto.EncryptStringWithSalt8(value, key));
        }

        /// <summary>
        /// Extends <see cref="ArgCollection" /> by adding a method to retrieve an encrypted string value.
        /// </summary>
        /// <param name="args">The current collection.</param>
        /// <param name="name">The argument name.</param>
        /// <param name="key">The symmetric encryption key.</param>
        /// <returns>The decrypted value.</returns>
        public static string GetEncrypted(this ArgCollection args, string name, SymmetricKey key)
        {
            var encrypted = args.Get(name, (byte[])null);

            if (encrypted == null)
                return null;

            return Crypto.DecryptStringWithSalt8(encrypted, key);
        }
    }
}
