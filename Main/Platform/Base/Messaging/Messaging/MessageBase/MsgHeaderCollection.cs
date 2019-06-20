//-----------------------------------------------------------------------------
// FILE:        MsgHeaderCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a message extension header collection.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a message extension header collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class holds a set of one or more <see cref="MsgHeader" /> instances
    /// defining the extended headers to be included in a message transmission.
    /// Each header consists of a <see cref="MsgHeaderID" /> and up to 
    /// 65535 bytes of binary data.  Note that only one header with a given
    /// <see cref="MsgHeaderID" /> can be added to the collection at any
    /// one time.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public sealed class MsgHeaderCollection : IEnumerable<MsgHeader>
    {
        private List<MsgHeader> list;

        /// <summary>
        /// Constructs an empty collection.
        /// </summary>
        public MsgHeaderCollection()
        {
            this.list = new List<MsgHeader>();
        }

        /// <summary>
        /// Constructs an empty collection with the specified initial capacity.
        /// </summary>
        public MsgHeaderCollection(int capacity)
        {
            this.list = new List<MsgHeader>(capacity);
        }

        /// <summary>
        /// Private constructor that performs no initialization.
        /// </summary>
        /// <param name="stub">Ignored.</param>
        private MsgHeaderCollection(Stub stub)
        {
        }

        /// <summary>
        /// Adds or modifies a message header in the collection.
        /// </summary>
        /// <param name="header">The header</param>
        /// <exception cref="InvalidOperationException">Thrown if a header with this ID is already present.</exception>
        public void Set(MsgHeader header)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].HeaderID == header.HeaderID)
                {
                    list[i] = header;
                    return;
                }
            }

            list.Add(header);
        }

        /// <summary>
        /// Adds or modifies a message header in the collection.
        /// </summary>
        /// <param name="headerID">The <see cref="MsgHeaderID" /></param>
        /// <param name="contents">The header value encoded into a byte array.</param>
        /// <exception cref="InvalidOperationException">Thrown if a header with this ID is already present.</exception>
        public void Set(MsgHeaderID headerID, byte[] contents)
        {
            Set(new MsgHeader(headerID, contents));
        }

        /// <summary>
        /// Returns the header with the specified ID if present in the
        /// collection or <c>null</c>.
        /// </summary>
        /// <param name="id">The <see cref="MsgHeaderID" /></param>
        /// <returns>The requested <see cref="MsgHeader" /> or <c>null</c>.</returns>
        public MsgHeader this[MsgHeaderID id]
        {
            get
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].HeaderID == id)
                        return list[i];
                }

                return null;
            }
        }

        /// <summary>
        /// Returns the header at the specified index in the collection
        /// where the index is a value ranging from 0 to <see cref="Count"/>-1.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>A <see cref="MsgHeader" />.</returns>
        public MsgHeader this[int index]
        {
            get { return list[index]; }
        }

        /// <summary>
        /// Returns the number of items in the collection.
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }

        /// <summary>
        /// Returns an enumerator for the headers in the collection.
        /// </summary>
        IEnumerator<MsgHeader> IEnumerable<MsgHeader>.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator for the headers in the collection.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        /// <summary>
        /// Returns a shallow copy of the instance.
        /// </summary>
        /// <returns>A cloned <see cref="MsgHeaderCollection" />.</returns>
        public MsgHeaderCollection Clone()
        {
            MsgHeaderCollection clone;

            clone      = new MsgHeaderCollection((Stub)null);
            clone.list = new List<MsgHeader>(list.Count);

            for (int i = 0; i < list.Count; i++)
                clone.list.Add(list[i]);

            return clone;
        }
    }
}
