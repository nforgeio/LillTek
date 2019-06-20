//-----------------------------------------------------------------------------
// FILE:        ArrayExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Array extension methods.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Array extension methods.
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Extracts a range of elements from an array.
        /// </summary>
        /// <typeparam name="TElement">The array element type.</typeparam>
        /// <param name="array">The array instance.</param>
        /// <param name="index">The index of the first element to be extracted.</param>
        /// <param name="count">The number of elements to be returned.</param>
        /// <returns>The array of extracted elements.</returns>
        public static TElement[] Extract<TElement>(this Array array, int index, int count)
        {
            TElement[] output;

            if (index < 0 || index + count > array.Length)
                throw new IndexOutOfRangeException();

            output = new TElement[count];

            for (int i = 0; i < count; i++)
                output[i] = (TElement)array.GetValue(index + i);

            return output;
        }

        /// <summary>
        /// Extracts array elements from a position to the end of the array.
        /// </summary>
        /// <typeparam name="TElement">The array element type.</typeparam>
        /// <param name="array">The array instance.</param>
        /// <param name="index">The index of the first element to be extracted.</param>
        /// <returns>The array of extracted elements.</returns>
        public static TElement[] Extract<TElement>(this Array array, int index)
        {
            TElement[] output;
            int count;

            if (index < 0 || index >= array.Length)
                throw new IndexOutOfRangeException();

            count = array.Length - index;
            output = new TElement[count];

            for (int i = 0; i < count; i++)
                output[i] = (TElement)array.GetValue(index + i);

            return output;
        }
    }
}
