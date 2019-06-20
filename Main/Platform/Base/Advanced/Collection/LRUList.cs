//-----------------------------------------------------------------------------
// FILE:        LRUList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a least-recently-used list.

using System;
using System.IO;
using System.Text;
using System.Collections;

using LillTek.Common;

namespace LillTek.Advanced
{

    /// <summary>
    /// Implements a least-recently-used list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LRU lists are typically used in cache implementations where
    /// a decision needs to be made about which elements to discard
    /// when the cache is full.  One common algorithm is to discard
    /// the least recently used element.
    /// </para>
    /// <para>
    /// This class provides an efficient implementation of an LRU
    /// list based on the doubly linked list class: DLList.  Call
    /// <see cref="Add" /> whenerver elements are added to the cache and 
    /// <see cref="Remove" /> when elements are deleted.  Call 
    /// <see cref="Touch" /> whenever an element is accessed.  The 
    /// <see cref="RemoveLRU" /> method can then be called
    /// to remove and return the least recently used element from
    /// the list.
    /// </para>
    /// <note>
    /// Elements must implement the <see cref="IDLElement" /> 
    /// interface to support the underlying doubly linked list.
    /// </note>
    /// <para>
    /// <b><u>Implementation Note</u></b>
    /// </para>
    /// <para>
    /// The most recently used elements will be appended to the end of the
    /// list and the least recently used elements will be at the front of
    /// the list.  There a good performance reason for this due to how
    /// <see cref="DoubleList" /> is implemented.  <see cref="RemoveLRU" />
    /// will be much more efficient at indexing into the first element 
    /// of the underlying list, rather than the last.
    /// </para>
    /// </remarks>
    public sealed class LRUList
    {
        private DoubleList list;

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public LRUList()
        {
            list = new DoubleList();
        }

        /// <summary>
        /// Adds an element to the list.
        /// </summary>
        /// <param name="element">The element.</param>
        public void Add(IDLElement element)
        {
            list.AddToEnd(element);
        }

        /// <summary>
        /// Removes and element from the list.
        /// </summary>
        /// <param name="element">The element.</param>
        public void Remove(IDLElement element)
        {
            list.Remove(element);
        }

        /// <summary>
        /// Indicates that an element has been touched by moving it
        /// to the most recently used end of the list.
        /// </summary>
        /// <param name="element">The element.</param>
        public void Touch(IDLElement element)
        {
            list.Remove(element);
            list.AddToEnd(element);
        }

        /// <summary>
        /// Removes the oldest element from the list and returns it.
        /// </summary>
        /// <returns>The oldest element or <c>null</c> if the list is empty.</returns>
        public IDLElement RemoveLRU()
        {
            IDLElement lru;

            if (list.Count == 0)
                return null;

            lru = list[0];
            list.Remove(lru);

            return lru;
        }

        /// <summary>
        /// Returns the number of elements in the list.
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }

        /// <summary>
        /// Returns the indexed item from the list.  Note that the item
        /// at index 0 will be the least recently used item.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The indexed item.</returns>
        /// <remarks>
        /// <note>
        /// It is much faster to scan forward and backwards
        /// through the list incrementing or decrementing the index
        /// by one than randomly jumping around.  This is due to how
        /// the underlying doubly linked list is implemented.
        /// </note>
        /// </remarks>
        public object this[int index]
        {
            get { return list[index]; }
        }

        /// <summary>
        /// Removes all elements from the list.
        /// </summary>
        public void Clear()
        {
            list.Clear();
        }

        /// <summary>
        /// Sets the capcacity of the collection to the actual number of items present,
        /// if that number is less than a threshold value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This class follows the .NET Framework standard by not reallocating the 
        /// collection unless that actual number of items is less then 90% of the
        /// current capacity.
        /// </para>
        /// <note>
        /// The threshold comuputation may change for future releases.
        /// </note>
        /// </remarks>
        public void TrimExcess()
        {
            // This actually a NOP since the underlying implementation is
            // a doubly linked list.  I'm keeping this to maintain consistency
            // with the other collection classes.
        }
    }
}
