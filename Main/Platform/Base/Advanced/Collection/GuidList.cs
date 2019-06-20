//-----------------------------------------------------------------------------
// FILE:        GuidList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a serializable list of Guids.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a serializable list of Guids.
    /// </summary>
    public class GuidList : List<Guid>
    {
        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public GuidList()
            : base()
        {
        }

        /// <summary>
        /// Constructs an empty list with the specified capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        public GuidList(int capacity)
            : base(capacity)
        {
        }

        /// <summary>
        /// Constructs a list by extracting <see cref="Guid" /> instances from
        /// the byte array.
        /// </summary>
        /// <param name="input">The input bytes.</param>
        public GuidList(byte[] input)
            : base(input.Length / 16)
        {
            if (input.Length % 16 != 0)
                throw new ArgumentException("Input byte array length must be a multiple of 16.");

            for (int i = 0; i < input.Length / 16; i++)
                base.Add(new Guid(Helper.Extract(input, i * 16, 16)));
        }

        /// <summary>
        /// Serializes the list into a byte array.
        /// </summary>
        /// <returns>The bytes.</returns>
        public byte[] ToByteArray()
        {
            var output = new byte[base.Count * 16];

            for (int i = 0; i < base.Count; i++)
                Array.Copy(base[i].ToByteArray(), 0, output, i * 16, 16);

            return output;
        }
    }
}
