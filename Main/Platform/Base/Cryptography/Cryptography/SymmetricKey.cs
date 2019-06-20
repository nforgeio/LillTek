//-----------------------------------------------------------------------------
// FILE:        SymmetricKey.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a symmetric encryption key.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Describes a symmetric encryption key.
    /// </summary>
    public class SymmetricKey : IDisposable, IParseable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a PLAINTEXT key.
        /// </summary>
        public static SymmetricKey PlainText { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SymmetricKey()
        {
            SymmetricKey.PlainText = new SymmetricKey();
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs a PLAINTEXT key. 
        /// </summary>
        public SymmetricKey()
        {

            this.Algorithm = CryptoAlgorithm.PlainText;
            this.Key       =
            this.IV        = new byte[0];
        }

        /// <summary>
        /// Constructs a key by parsing the source string passed.
        /// </summary>
        /// <param name="source">The source string</param>
        /// <exception cref="ArgumentException">Thrown if the string passed is not valid.</exception>
        /// <remarks>
        /// <para>
        /// Fully qualified symmetric key strings are formatted as:
        /// </para>
        /// <code language="none">
        /// &lt;algorithm&gt; ":" &lt;key (base64)&gt; ":" &lt;iv (base64)&gt;
        /// 
        /// or just
        /// 
        /// "PLAINTEXT"
        /// </code>
        /// <para>
        /// where <b>algorithm</b> is one of the <see cref="CryptoAlgorithm" />
        /// constants describing the cryptographic algorithm, <b>key</b> is the
        /// base-64 encoded key bytes and <b>iv</b> is the base-64 encoded
        /// initialization vector.
        /// </para>
        /// </remarks>
        public SymmetricKey(string source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            source = source.Trim();
            if (String.Compare(source, "PLAINTEXT", StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.Algorithm = CryptoAlgorithm.PlainText;
                this.Key       =
                this.IV        = new byte[0];

                return;
            }

            var fields = source.Split(':');

            if (fields.Length != 3)
                throw new ArgumentException("Symmetric key does not have the form: <algorithm>:<key>:<iv>");

            this.Algorithm = fields[0];
            this.Key = Convert.FromBase64String(fields[1]);
            this.IV = Convert.FromBase64String(fields[2]);
        }

        /// <summary>
        /// Constructs an instance from the explicit parameters passed.
        /// </summary>
        /// <param name="algorithm">The cryptographic algorithm (see <see cref="CryptoAlgorithm" />).</param>
        /// <param name="key">The cryptogrpahic key.</param>
        /// <param name="iv">The initialization vector.</param>
        public SymmetricKey(string algorithm, byte[] key, byte[] iv)
        {
            this.Algorithm = algorithm;
            this.Key       = key;
            this.IV        = iv;
        }

        /// <summary>
        /// Specifies the cryptographic algorithm.  See <see cref="CryptoAlgorithm" />
        /// for the possible values.
        /// </summary>
        public string Algorithm { get; set; }

        /// <summary>
        /// The cryptographic key bytes.
        /// </summary>
        public byte[] Key { get; set; }

        /// <summary>
        /// The cryptographic initialization vector.
        /// </summary>
        public byte[] IV { get; set; }

        /// <summary>
        /// Renders the key as a string.
        /// </summary>
        /// <returns>The key string formatted as: &lt;algorithm&gt; ":" &lt;key (base64)&gt; ":" &lt;iv (base64)&gt; or just "PLAINTEXT".</returns>
        public override string ToString()
        {
            if (this.Algorithm == CryptoAlgorithm.PlainText)
                return "PLAINTEXT";
            else
                return string.Format("{0}:{1}:{2}", Algorithm, Convert.ToBase64String(Key), Convert.ToBase64String(IV));
        }

        /// <summary>
        /// Returns a deep clone of the key.
        /// </summary>
        /// <returns>The cloned <see cref="SymmetricKey" />.</returns>
        /// <remarks>
        /// This method is useful in situations where a copy of a key needs
        /// to be made before the original key is disposed and its
        /// <see cref="Key" /> and <see cref="IV" /> properties are zeroed.
        /// </remarks>
        public SymmetricKey Clone()
        {
            var clone = new SymmetricKey();

            clone.Algorithm = this.Algorithm;

            if (this.Key != null)
            {
                clone.Key = new byte[this.Key.Length];
                Array.Copy(this.Key, clone.Key, this.Key.Length);
            }

            if (this.IV != null)
            {
                clone.IV = new byte[this.IV.Length];
                Array.Copy(this.IV, clone.IV, this.IV.Length);
            }

            return clone;
        }

        //---------------------------------------------------------------------
        // IDisposable implementation.

        /// <summary>
        /// Clears the encryption key and IV.
        /// </summary>
        public void Dispose()
        {
            if (this.Key != null)
                Array.Clear(this.Key, 0, this.Key.Length);

            if (this.IV != null)
                Array.Clear(this.IV, 0, this.IV.Length);
        }

        //---------------------------------------------------------------------
        // IParseable implementation.

        /// <summary>
        /// Attempts to parse the configuration value.
        /// </summary>
        /// <param name="value">The configuration value.</param>
        /// <returns><c>true</c> if the value could be parsed, <b></b> if the value is not valid for the type.</returns>
        public bool TryParse(string value)
        {
            if (value == null)
                return false;

            value = value.Trim();
            if (String.Compare(value, "PLAINTEXT", StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.Algorithm = CryptoAlgorithm.PlainText;
                this.Key       =
                this.IV        = new byte[0];

                return true;
            }

            var fields = value.Split(':');

            if (fields.Length != 3)
                return false;

            try
            {
                this.Algorithm = fields[0];
                this.Key       = Convert.FromBase64String(fields[1]);
                this.IV        = Convert.FromBase64String(fields[2]);
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
