//-----------------------------------------------------------------------------
// FILE:        CompositeEnumerator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an enumerator by composing multiple individial
//              enumerators.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

// $todo(jeff.lill):
//
// Need to implement a way to communicate Dispose() calls to the 
// source collection.

namespace LillTek.Advanced
{

    /// <summary>
    /// Implements an enumerator by composing multiple individial enumerators.
    /// </summary>
    /// <typeparam name="T">The value type being enumerated</typeparam>
    /// <remarks>
    /// <para>
    /// This class is designed to make it easy to compose multiple collections 
    /// into a single composite collection.  A good example of this is the 
    /// <see cref="HugeDictionary{TKey,TValue}" /> class which composes multiple 
    /// <see cref="Dictionary{TKey,TValue}" /> instances together into a single
    /// dictionary for performance purposes.
    /// </para>
    /// <para>
    /// This class is pretty easy to use.  Simply pass the set of enumerators for
    /// the subcollections to the constructor.  The only real trick is that the
    /// source collections must call <see cref="OnSourceChanged" /> whenever the
    /// source collection is modified.  This signals to the composite enumerator
    /// that is can no longer continue enumerating.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class CompositeEnumerator<T> : IEnumerator<T>
    {
        private const string ChangedMsg = "CompositeEnumerator: Enumeration source has been modified during enumeration.  Enumeration cannot continue.";

        private IEnumerator<T>[]    enumerators;
        private int                 curIndex;
        private IEnumerator<T>      curEnumerator;
        private T                   curItem;
        private bool                sourceChanged;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="enumerators">The enumerator instances being composed.</param>
        /// <exception cref="ArgumentException">Thrown if the set of enumerators passed is empty.</exception>
        public CompositeEnumerator(params IEnumerator<T>[] enumerators)
        {
            if (enumerators.Length == 0)
                throw new ArgumentException("CompositeEnumerator: At least one enumerator must be passed.");

            this.enumerators   = enumerators;
            this.curIndex      = 0;
            this.curEnumerator = this.enumerators[curIndex];
            this.curItem       = default(T);
            this.sourceChanged = false;
        }

        /// <summary>
        /// Returns the current value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the enumeration has been modified.</exception>
        public T Current
        {
            get
            {
                if (sourceChanged)
                    throw new InvalidOperationException(ChangedMsg);

                return curItem;
            }
        }

        /// <summary>
        /// Returns the current value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the enumeration has been modified.</exception>
        object IEnumerator.Current
        {
            get
            {
                if (sourceChanged)
                    throw new InvalidOperationException(ChangedMsg);

                return curItem;
            }
        }

        /// <summary>
        /// Moves to the next item in the enumeration.
        /// </summary>
        /// <returns><c>true</c> if the another item was available, <c>false</c> if we've reached the end of the set.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the enumeration has been modified.</exception>
        public bool MoveNext()
        {
            if (sourceChanged)
                throw new InvalidOperationException(ChangedMsg);

            while (true)
            {
                if (curIndex >= enumerators.Length)
                {
                    curItem = default(T);
                    curEnumerator = null;
                    return false;
                }

                if (!curEnumerator.MoveNext())
                {
                    curIndex++;
                    if (curIndex >= enumerators.Length)
                    {

                        curItem = default(T);
                        curEnumerator = null;
                        return false;
                    }

                    curEnumerator = enumerators[curIndex];
                    continue;
                }

                curItem = curEnumerator.Current;
                return true;
            }
        }

        /// <summary>
        /// Resets the enumerator to the initial state.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the enumeration has been modified.</exception>
        /// <exception cref="NotSupportedException">Thrown if any of the underlying enumerators do not support <see cref="Reset" />.</exception>
        public void Reset()
        {
            if (sourceChanged)
                throw new InvalidOperationException(ChangedMsg);

            this.curIndex = 0;
            this.curEnumerator = this.enumerators[curIndex];
            this.curItem = default(T);

            foreach (var enumerator in enumerators)
                enumerator.Reset();
        }

        /// <summary>
        /// Releases any unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var enumerator in enumerators)
                enumerator.Dispose();
        }

        /// <summary>
        /// Called by the enumerator's source instance when the source has 
        /// been modified.  This will cause all subsequent enumeration operations
        /// to fail with an <see cref="InvalidOperationException" />,
        /// </summary>
        public void OnSourceChanged()
        {
            sourceChanged = true;
        }
    }
}
