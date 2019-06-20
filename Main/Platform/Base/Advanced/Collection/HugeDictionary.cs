//-----------------------------------------------------------------------------
// FILE:        HugeDictionary.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a dictionary capable of performing well even when
//              it's loaded with a huge number of items.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

// $todo(jeff.lill): Not entirely implemented see comments.

// $todo(jeff.lill):
//
// The class needs a way to keep track of the current enumerator and call its
// OnSourceChanged() method when the collection changes.  The enumerator will
// also need to maintain a reference to the parent collection so that it can
// notify the parent when the enumerator's Dispose() method is called.  This
// will allow the parent to clear the current enumerator reference.

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a dictionary capable of performing well even when it's loaded 
    /// with a huge number of items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The built-in .NET <see cref="Dictionary{TKey,TValue}" /> implementation seems
    /// to work well for up to about 100,000 items.  After this, performance starts
    /// to degrade significantly.  This class abstracts the composition of multiple 
    /// <see cref="Dictionary{TKey,TValue}" /> instances using a multi-level hashing
    /// scheme to deal with this problem.
    /// </para>
    /// <para>
    /// The class constructors all accept an integer <b>count</b> parameter.  This is
    /// the number of internal dictionaries the class should maintain.  The count
    /// should be selected such that the number of expected items divided by the count
    /// is significantly less than 100,000.
    /// </para>
    /// </remarks>
    /// <threadsaftey instance="false" />
    public class HugeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private const string CountMsg = "HugeDictionary: [DictionaryCount] must be greater than zero.";

        private Dictionary<TKey, TValue>[]  dictionaries;
        private IEqualityComparer<TKey>     comparer;
        private int                         count;

        /// <summary>
        /// Constructor. 
        /// </summary>
        /// <param name="dictionaryCount">The number of internal dictionaries to be created.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="dictionaryCount" /> is less than or equal to zero.</exception>
        public HugeDictionary(int dictionaryCount)
        {
            if (dictionaryCount <= 0)
                throw new ArgumentException(CountMsg);

            dictionaries = new Dictionary<TKey, TValue>[dictionaryCount];
            for (int i = 0; i < dictionaries.Length; i++)
                dictionaries[i] = new Dictionary<TKey, TValue>();

            this.count    = 0;
            this.comparer = null;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="dictionaryCount">The number of internal dictionaries to be created.</param>
        /// <param name="capacity">The inital capacity to allocate for each internal dictionary.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="dictionaryCount" /> is less than or equal to zero.</exception>
        public HugeDictionary(int dictionaryCount, int capacity)
        {
            if (dictionaryCount <= 0)
                throw new ArgumentException(CountMsg);

            dictionaries = new Dictionary<TKey, TValue>[dictionaryCount];
            for (int i = 0; i < dictionaries.Length; i++)
                dictionaries[i] = new Dictionary<TKey, TValue>(capacity);

            this.count    = 0;
            this.comparer = null;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="dictionaryCount">The number of internal dictionaries to be created.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{TKey}" /> to be used for comparing key values.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="dictionaryCount" /> is less than or equal to zero.</exception>
        public HugeDictionary(int dictionaryCount, IEqualityComparer<TKey> comparer)
        {
            if (dictionaryCount <= 0)
                throw new ArgumentException(CountMsg);

            dictionaries = new Dictionary<TKey, TValue>[dictionaryCount];
            for (int i = 0; i < dictionaries.Length; i++)
                dictionaries[i] = new Dictionary<TKey, TValue>(comparer);

            this.count    = 0;
            this.comparer = comparer;
        }

        /// <summary>
        /// Returns the number of items in the dictionary.
        /// </summary>
        public int Count
        {
            get { return count; }
        }

        /// <summary>
        /// Returns the sub-dictionary used to hold the item with a specified key.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns>The sub-dictionary.</returns>
        private Dictionary<TKey, TValue> GetDictionary(TKey key)
        {
            int hash;

            if (comparer == null)
                hash = key.GetHashCode();
            else
                hash = comparer.GetHashCode(key);

            return dictionaries[Helper.HashToIndex(dictionaries.Length, hash)];
        }

        /// <summary>
        /// Adds an item to the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(TKey key, TValue value)
        {
            GetDictionary(key).Add(key, value);
            count++;
        }

        /// <summary>
        /// Determines whether an item with a specified key is present in the dictionary.
        /// </summary>
        /// <param name="key">The key being tested.</param>
        /// <returns><c>true</c> if the item is present.</returns>
        public bool ContainsKey(TKey key)
        {
            return GetDictionary(key).ContainsKey(key);
        }

        /// <summary>
        /// <b>Not Implemented: </b> Returns the collection of dictionary keys.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns><c>true</c> if the item was present and was removed.</returns>
        public bool Remove(TKey key)
        {
            if (GetDictionary(key).Remove(key))
            {
                count--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to return an item from the dictionary.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">Returns as the item value on success.</param>
        /// <returns><c>true</c> if the item was retrieved.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return GetDictionary(key).TryGetValue(key, out value);
        }

        /// <summary>
        /// <b>Not Implemented: </b> Returns the collection of dictionary values.
        /// </summary>
        public ICollection<TValue> Values
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Indexes into the dictionary.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns>The corresponding item value.</returns>
        public TValue this[TKey key]
        {
            get { return GetDictionary(key)[key]; }

            set
            {
                var dictionary = GetDictionary(key);

                if (!dictionary.ContainsKey(key))
                    count++;

                dictionary[key] = value;
            }
        }

        /// <summary>
        /// Adds an entry to the dictionary.
        /// </summary>
        /// <param name="item">The new item entry.</param>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            GetDictionary(item.Key).Add(item.Key, item.Value);
            count++;
        }

        /// <summary>
        /// Removes all items from the dictionary.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < dictionaries.Length; i++)
                dictionaries[i].Clear();

            count = 0;
        }

        /// <summary>
        /// Determines whether the dictionary contains an item entry.
        /// </summary>
        /// <param name="item">The item entry.</param>
        /// <returns><c>true</c> if the item entry is present.</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            // $hack(jeff.lill):
            //
            // I expected to be able to implement this as:
            //
            //      GetDictionary(item.Key).Contains(item)
            //
            // but for some reason the sub-dictionary types don't have this
            // method defined.  Weird.

            return GetDictionary(item.Key).ContainsKey(item.Key);
        }

        /// <summary>
        /// <b>Not Implemented: </b> Copies the item entries into an array.
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <param name="arrayIndex">The position in the array where the first entry will be written.</param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Indicates whether the dictionary is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes an item entry from the dictionary.
        /// </summary>
        /// <param name="item">The item entry.</param>
        /// <returns><c>true</c> if the entry was present and was removed.</returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (GetDictionary(item.Key).Remove(item.Key))
            {
                count--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns an enumerator over the dictionary entries.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var enumerators = new IEnumerator<KeyValuePair<TKey, TValue>>[dictionaries.Length];

            for (int i = 0; i < dictionaries.Length; i++)
                enumerators[i] = dictionaries[i].GetEnumerator();

            return new CompositeEnumerator<KeyValuePair<TKey, TValue>>(enumerators);
        }

        /// <summary>
        /// Returns an enumerator over the dictionary entries.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
