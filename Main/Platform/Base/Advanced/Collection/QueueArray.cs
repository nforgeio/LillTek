//-----------------------------------------------------------------------------
// FILE:        QueueArray.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Combines the functionality of a Queue and a List.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Combines the functionality of the <see cref="Queue{T}" /> and <see cref="List{T}" />
    /// classes by adding the ability to index back into a queues's elements and also to
    /// enqueue and dequeue from within the queue rather just from the ends.
    /// </summary>
    /// <threadsafety instance="false" />
    public class QueueArray<TValue> : IEnumerable<TValue>
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to hold the value within a doubly linked list.
        /// </summary>
        private sealed class Node : IDLElement
        {
            public TValue   Value;
            private object  previous;
            private object  next;

            public Node(TValue value)
            {
                this.Value    = value;
                this.previous = null;
                this.next     = null;
            }

            public object Previous
            {
                get { return previous; }
                set { previous = value; }
            }

            public object Next
            {

                get { return next; }
                set { next = value; }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private DoubleList list;

        /// <summary>
        /// Constructs an empty queue.
        /// </summary>
        public QueueArray()
        {
            list = new DoubleList();
        }

        /// <summary>
        /// Adds a value to the end of the queue.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Enqueue(TValue value)
        {
            list.AddToEnd(new Node(value));
        }

        /// <summary>
        /// Removes and returns a value from the front of the queue.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public TValue Dequeue()
        {
            Node node;

            if (list.Count == 0)
                throw new InvalidOperationException("Queue is empty.");

            node = (Node)list[0];
            list.Remove(node);
            return node.Value;
        }

        /// <summary>
        /// Returns the value from the front of the queue without removing it.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public TValue Peek()
        {
            Node node;

            if (list.Count == 0)
                throw new InvalidOperationException("Queue is empty.");

            node = (Node)list[0];
            return node.Value;
        }

        /// <summary>
        /// Inserts a value at specific position in the queue.
        /// </summary>
        /// <param name="index">The position where the value will be inserted.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// Pass <b>index=0</b> to insert the value at the front of the list
        /// or <b>index=Count</b> to insert it at the end.
        /// </note>
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is not in the range <b>0..Count</b>.</exception>
        public void InsertAt(int index, TValue value)
        {
            list.InsertAt(index, new Node(value));
        }

        /// <summary>
        /// Removes a value from a specific position in the queue.
        /// </summary>
        /// <param name="index">The index of the value to be removed.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is not in the range <b>0..Count-1</b>.</exception>
        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        /// <summary>
        /// Returns the number of items in the queue.
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }

        /// <summary>
        /// Indexes into the queue and returns the requested value.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value at the index.</returns>
        /// <remarks>
        /// <note>
        /// The value at the front of the queue is accessed using
        /// <b>index=0</b> and the value at the end is accessed
        /// using <b>index=Count-1</b>.
        /// </note>
        /// <note>
        /// This class is implemented using a doubly linked list.
        /// This means that the list will have to be walked to 
        /// find a particular item.  The class does cache the
        /// current position so it is efficient to walk the list
        /// from front to back or the reverse but random indexing
        /// will be expensive.
        /// </note>
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is not in the range <b>0..Count-1</b>.</exception>
        public TValue this[int index]
        {
            get { return ((Node)list[index]).Value; }
        }

        /// <summary>
        /// Returns the queue as an array.
        /// </summary>
        /// <returns>The array of queued values.</returns>
        public TValue[] ToArray()
        {
            var array = new TValue[list.Count];
            int i;

            i = 0;
            foreach (Node node in list)
                array[i++] = node.Value;

            return array;
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
            // a doublly longed list.  I'm keeping this to maintain consistency
            // with the other collection classes.
        }

        //---------------------------------------------------------------------
        // IEnumerable implementation

        /// <summary>
        /// Returns an enumerator over the collection.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Values from the top to the bottom of the stack will be returned
        /// by the enumerator.
        /// </note>
        /// </remarks>
        public IEnumerator<TValue> GetEnumerator()
        {
            var values = new List<TValue>(list.Count);

            foreach (Node node in list)
                values.Add(node.Value);

            return values.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator over the collection.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Values from the top to the bottom of the stack will be returned
        /// by the enumerator.
        /// </note>
        /// </remarks>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
