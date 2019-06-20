//-----------------------------------------------------------------------------
// FILE:        DoubleList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a doubly linked list.

using System;
using System.IO;
using System.Text;
using System.Collections;

// $todo(jeff.lill):
//
// This is very old code.  Convert into a generic class and get rid 
// of the IDLElement class too.

namespace LillTek.Common
{
    /// <summary>
    /// Implements a doubly linked list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements a doubly linked list of objects that implement
    /// the <see cref="IDLElement" /> interface.  This interface defines the Previous and Next
    /// properties used by the class to link to the previous and next objects
    /// in the list.
    /// </para>
    /// <para>
    /// Elements can be added at either end of the list or inserted after a
    /// specified element.  Elements can be removed efficiently from anywhere
    /// in the list.  Note that an element may be a part of only one list
    /// at a time.
    /// </para>
    /// <para>
    /// The list provides an indexer for iterating through the list.  This will
    /// provide for access in linear time when iterating forward or backwards 
    /// in the with no intervening additions or deletions.  The class also
    /// implements the IEnumerable interface by returning an enumerator that
    /// walks the list from beginning to end.
    /// </para>
    /// <para>
    /// <b><u>Implementation Note</u></b>
    /// </para>
    /// <para>
    /// The first element of the list will have its Previous property set to
    /// the list instance and the last element in the list will have its
    /// Next property set to the list instance.
    /// </para>
    /// <para>
    /// I'm going to cache the index and element returned by the class'
    /// indexer to implement efficient forward and backward scans through
    /// the list.  I'll purge these cached values whenever elements are
    /// added or removed.
    /// </para>
    /// <para>
    /// The enumerator returned will simply walk forward through the list.
    /// The opCount property will be used by the enumerator to determine
    /// if additions or deletions are made to the underlying list during
    /// enumeration.
    /// </para>
    /// </remarks>
    public class DoubleList : IEnumerable
    {
        private const string AlreadyInListMsg = "Element is already in a list.";
        private const string NotInListMsg = "Element not in a list.";
        private const string ListChangedMsg = "List changed during enumeration.";
        private const string MovePastEndMsg = "Attempt to move past the end of the list.";

        //---------------------------------------------------------------------
        // The enumerator class

        private sealed class Enumerator : IEnumerator
        {
            private DoubleList list;
            private object current;
            private int opCount;

            public Enumerator(DoubleList list)
            {
                this.list = list;
                this.current = null;
                this.opCount = list.opCount;
            }

            public void Reset()
            {
                current = null;
            }

            public object Current
            {
                get
                {
                    if (list.opCount != opCount)
                        throw new InvalidOperationException(ListChangedMsg);

                    return current;
                }
            }

            public bool MoveNext()
            {
                if (list.opCount != opCount)
                    throw new InvalidOperationException(ListChangedMsg);

                if (object.ReferenceEquals(list, current))
                    throw new InvalidOperationException(MovePastEndMsg);

                if (current == null)
                {
                    if (list.front == null)
                    {
                        current = list;
                        return false;
                    }

                    current = list.front;
                    return true;
                }

                current = ((IDLElement)current).Next;
                if (current == list)
                {
                    current = null;
                    return false;
                }

                return true;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private IDLElement      front;          // The first element in the list (or null)
        private IDLElement      back;           // The last element in the list (or null)
        private int             count;          // # of elements in the list
        private int             opCount;        // Counter used to detect changes to the underlying
                                                // list during enumeration
        private int             lastIndex;      // Index of the last referenced element
        private IDLElement      lastElement;    // The last referenced element (or null)

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public DoubleList()
        {
            this.front       = null;
            this.back        = null;
            this.count       = 0;
            this.opCount     = 0;
            this.lastElement = null;
            this.lastIndex   = -1;
        }

        /// <summary>
        /// Returns the number of elements in the list.
        /// </summary>
        public int Count
        {
            get { return count; }
        }

        /// <summary>
        /// Called whenever the list is modified to update the opCount, as well
        /// as clearing the cached index and element.
        /// </summary>
        private void Flush()
        {
            opCount++;
            lastIndex   = -1;
            lastElement = null;
        }

        /// <summary>
        /// Adds an element to the front of the list.
        /// </summary>
        /// <param name="element">The element to be inserted.</param>
        /// <exception cref="InvalidOperationException">Thrown if the element is already in a list.</exception>
        public void AddToFront(IDLElement element)
        {
            Flush();

            if (element.Previous != null || element.Next != null)
                throw new InvalidOperationException(AlreadyInListMsg);

            if (this.front == null)
            {
                // The list is empty

                this.front       = element;
                this.back        = element;
                element.Previous = this;
                element.Next     = this;
            }
            else
            {

                element.Previous    = this;
                element.Next        = this.front;
                this.front.Previous = element;
                this.front          = element;
            }

            this.count++;
        }

        /// <summary>
        /// Adds an element to the end of the list.
        /// </summary>
        /// <param name="element">The element to be inserted.</param>
        /// <exception cref="InvalidOperationException">Thrown if the element is already in a list.</exception>
        public void AddToEnd(IDLElement element)
        {
            Flush();

            if (element.Previous != null || element.Next != null)
                throw new InvalidOperationException(AlreadyInListMsg);

            if (this.front == null)
            {
                // The list is empty

                this.front       = element;
                this.back        = element;
                element.Previous = this;
                element.Next     = this;
            }
            else
            {
                element.Previous = this.back;
                element.Next     = this;
                this.back.Next   = element;
                this.back        = element;
            }

            this.count++;
        }

        /// <summary>
        /// Inserts an element after another in the list.
        /// </summary>
        /// <param name="preceding">The element already in the list.</param>
        /// <param name="element">The element to be inserted.</param>
        /// <remarks>
        /// Passing preceding as null will add the element at the 
        /// front of the list.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the element is already in a list.</exception>
        public void InsertAfter(IDLElement preceding, IDLElement element)
        {
            Flush();

            if (element.Previous != null || element.Next != null)
                throw new InvalidOperationException(AlreadyInListMsg);

            if (preceding == null)
                AddToFront(element);
            else
            {
                element.Previous = preceding;
                element.Next     = preceding.Next;
                preceding.Next   = element;

                if (object.ReferenceEquals(element.Next, this))
                    this.back = element;
                else
                    ((IDLElement)element.Next).Previous = element;

                this.count++;
            }
        }

        /// <summary>
        /// Inserts an element at s specified position in the list.
        /// </summary>
        /// <param name="index">The index where the element will be inserted.</param>
        /// <param name="element">The element to be inserted.</param>
        /// <exception cref="InvalidOperationException">Thrown if the element is already in a list.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is outside the range of <b>0..Count</b>.</exception>
        public void InsertAt(int index, IDLElement element)
        {
            Flush();

            if (element.Previous != null || element.Next != null)
                throw new InvalidOperationException(AlreadyInListMsg);

            if (index < 0 || index > count)
                throw new IndexOutOfRangeException();

            if (index == 0)
                AddToFront(element);
            else
                InsertAfter(this[index - 1], element);
        }

        /// <summary>
        /// Removes an element from the list.
        /// </summary>
        /// <param name="element">The element to be removed.</param>
        /// <exception cref="InvalidOperationException">Thrown if the element is not in the list.</exception>
        public void Remove(IDLElement element)
        {
            Flush();

            if (element.Previous == null || element.Next == null)
                throw new InvalidOperationException(NotInListMsg);

            if (count == 1)
            {
                this.front = null;
                this.back = null;
            }
            else
            {
                if (this.front == element)
                {
                    this.front = (IDLElement)element.Next;
                    ((IDLElement)element.Next).Previous = this;
                }
                else if (this.back == element)
                {
                    this.back = (IDLElement)element.Previous;
                    ((IDLElement)element.Previous).Next = this;
                }
                else
                {
                    ((IDLElement)element.Previous).Next = element.Next;
                    ((IDLElement)element.Next).Previous = element.Previous;
                }
            }

            element.Previous = null;
            element.Next = null;

            this.count--;
            if (count < 0)
                throw new InvalidOperationException("Remove error.");
        }

        /// <summary>
        /// Removes an element from the list.
        /// </summary>
        /// <param name="index">Index of the element to be removed.</param>
        public void RemoveAt(int index)
        {
            Remove(this[index]);
        }

        /// <summary>
        /// Removes all elements from the list.
        /// </summary>
        public void Clear()
        {
            Flush();

            this.front = null;
            this.back  = null;
            this.count = 0;
        }

        /// <summary>
        /// Returns the element at the specified position in the list.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Index=0 references the element at the front of the list and
        /// Index=Count-1 reference the last element in the list.
        /// </note>
        /// <note>
        /// The class optimizes for scanning forward or backwards
        /// through the list.
        /// </note>
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is not in the range <b>0..Count-1</b>.</exception>
        public IDLElement this[int index]
        {
            get
            {
                if (index < 0 || index >= count)
                    throw new IndexOutOfRangeException();

                if (lastElement != null)
                {
                    if (index == lastIndex)
                        return lastElement;
                    else if (index == lastIndex - 1)
                    {
                        lastIndex   = index;
                        lastElement = (IDLElement)lastElement.Previous;

                        return lastElement;
                    }
                    else if (index == lastIndex + 1)
                    {
                        lastIndex   = index;
                        lastElement = (IDLElement)lastElement.Next;

                        return lastElement;
                    }
                }

                // $todo(jeff.lill): 
                //
                // If I ever have some spare time to burn, I
                // could optimize this by deciding whether it
                // makes more sense to count up or down from
                // the current position, count up from the beginning,
                // or count down from the end of the list.  

                IDLElement element;

                element = this.front;
                for (int i = 0; i < index; i++)
                    element = (IDLElement)element.Next;

                lastIndex   = index;
                lastElement = element;

                return element;
            }
        }

        /// <summary>
        /// Returns an enumerator that walks the list from beginning to end.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            return new Enumerator(this);
        }
    }
}
