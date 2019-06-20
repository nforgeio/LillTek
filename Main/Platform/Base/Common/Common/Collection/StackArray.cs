//-----------------------------------------------------------------------------
// FILE:        StackArray.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Combines the functionality of a Stack and a List.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Combines the functionality of the <see cref="Stack{T}" /> and <see cref="List{T}" />
    /// classes by adding the ability to index back into a stack's elements.
    /// </summary>
    /// <typeparam name="TValue">The stack value type.</typeparam>
    /// <threadsafety instance="false" />
    public class StackArray<TValue> : IEnumerable<TValue>
    {
        private List<TValue> stack;

        /// <summary>
        /// Constructor.
        /// </summary>
        public StackArray()
        {
            this.stack = new List<TValue>();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity of the stack.</param>
        public StackArray(int capacity)
        {
            this.stack = new List<TValue>(capacity);
        }

        /// <summary>
        /// Pushes a value onto the stack.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Push(TValue value)
        {
            stack.Add(value);
        }

        /// <summary>
        /// Pops the value off the top of the stack and returns it.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stack is empty.</exception>
        public TValue Pop()
        {
            TValue value;

            if (stack.Count == 0)
                throw new InvalidOperationException("The stack is empty.");

            value = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            
            return value;
        }

        /// <summary>
        /// Pops and discards the specified number of values off the top of the stack.
        /// </summary>
        /// <param name="count">The number of values to pop.</param>
        /// <remarks>
        /// <note>
        /// It is not an error specify a count greater than the number
        /// of items in the stack.  The method will simply clear the stack
        /// in this case.
        /// </note>
        /// </remarks>
        public void Discard(int count)
        {
            if (count >= stack.Count)
            {
                stack.Clear();
                return;
            }

            for (int i = 0; i < count; i++)
                stack.RemoveAt(stack.Count - 1);
        }

        /// <summary>
        /// Returns the top value from the stack without removing it.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stack is empty.</exception>
        public TValue Peek()
        {
            if (stack.Count == 0)
                throw new InvalidOperationException("The stack is empty.");

            return stack[stack.Count - 1];
        }

        /// <summary>
        /// Returns the number of values on the stack.
        /// </summary>
        public int Count
        {
            get { return stack.Count; }
        }

        /// <summary>
        /// Clears the stack.
        /// </summary>
        public void Clear()
        {
            stack.Clear();
        }

        /// <summary>
        /// Inserts a new value into the stack at a specific position.
        /// </summary>
        /// <param name="index">
        /// The zero based position relative to the top of the stack
        /// where the value will be located after insertion.  Note that
        /// and index of 0 will push the item onto the top of the stack.
        /// </param>
        /// <param name="value">The value to be inserted.</param>
        public void Insert(int index, TValue value)
        {
            stack.Insert(stack.Count - index, value);
        }

        /// <summary>
        /// Removes the first occurance of a value from the stack.
        /// </summary>
        /// <param name="value">The value to be removed.</param>
        public void Remove(TValue value)
        {
            stack.Remove(value);
        }

        /// <summary>
        /// Removes a value at a specific index relative to the top of the stack.
        /// </summary>
        /// <param name="index">
        /// The zero based index of the value to be removed.
        /// </param>
        public void RemoveAt(int index)
        {
            stack.RemoveAt(stack.Count - index - 1);
        }

        /// <summary>
        /// Indexes into the stack and returns the specified value where an 
        /// index=0 returns the top value, index=1 returns the second value from the
        /// top, index=1 the third value from the top, etc.
        /// </summary>
        /// <param name="index">The offset from the top of the stack.</param>
        /// <returns>The value.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stack is empty.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is invald.</exception>
        public TValue this[int index]
        {
            get
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException("The stack is empty.");

                if (index < 0 || index >= stack.Count)
                    throw new IndexOutOfRangeException();

                return stack[stack.Count - index - 1];
            }
        }

        /// <summary>
        /// Scans the stack from to bottom returning the index of the first stack
        /// entry that equals the value passed.
        /// </summary>
        /// <param name="value">The value to be matched.</param>
        /// <returns>The index of the first matching value or <b>-1</b> if no match was found.</returns>
        /// <remarks>
        /// Values are considered to match if the <b>Equals()</b> method returns <c>true</c>.
        /// </remarks>
        public int IndexOf(TValue value)
        {
            for (int i = 0; i < Count; i++)
                if (value.Equals(this[i]))
                    return i;

            return -1;
        }

        /// <summary>
        /// Converts the stack into an array.
        /// </summary>
        /// <returns>The array.</returns>
        public TValue[] ToArray()
        {
            return stack.ToArray();
        }

        /// <summary>
        /// Sets the capacity of the collection to the actual number of items present,
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
            stack.TrimExcess();
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
            List<TValue> list;

            list = new List<TValue>(stack);
            list.Reverse();

            return list.GetEnumerator();
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

