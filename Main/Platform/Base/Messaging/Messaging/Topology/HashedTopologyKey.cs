//-----------------------------------------------------------------------------
// FILE:        HashedTopologyKey.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements serialization for the keys to ITopologyProvider
//              implementations that make routing decisions based on the
//              hash code computed for the key.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements serialization for the keys to <see cref="ITopologyProvider" /> implementations 
    /// that make routing decisions based on the hash code computed for the key.
    /// </summary>
    public sealed class HashedTopologyKey
    {
        private int hashCode;

        /// <summary>
        /// Constructs an instance from the original topology key.
        /// </summary>
        /// <param name="key">The topology key.</param>
        public HashedTopologyKey(object key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            hashCode = key.GetHashCode();
        }

        /// <summary>
        /// Reconsitutes the topology key from its string form.
        /// </summary>
        /// <param name="key">The serialized topology key.</param>
        public HashedTopologyKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (!key.StartsWith("HashedTopologyKey:"))
                throw new ArgumentException(string.Format("[{0}] is not a serialized [HashedTopologyKey].", key));

            hashCode = int.Parse(key.Substring(19));
        }

        /// <summary>
        /// Serializes the topology key to a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "HashedTopologyKey:" + hashCode.ToString();
        }

        /// <summary>
        /// Returns the hash code of the original topology key.
        /// </summary>
        /// <returns>The computed hash.</returns>
        public override int GetHashCode()
        {
            return hashCode;
        }

        /// <summary>
        /// Returns <c>true</c> if this object equals another (note that this falls through directly 
        /// to the inherited <see cref="object.Equals(object)" /> implementation).
        /// </summary>
        /// <param name="obj">The object to be compared with this instance.</param>
        /// <returns><c>true</c> if the objects are to be considered equal.</returns>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
    }
}
