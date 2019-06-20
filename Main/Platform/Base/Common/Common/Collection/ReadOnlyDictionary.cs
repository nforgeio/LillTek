//-----------------------------------------------------------------------------
// FILE:        ReadOnlyDictionary.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Wraps a normal dictionary to make it read-only. 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Wraps a normal dictionary to make it read-only. 
    /// </summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    public class ReadOnlyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private const string ReadOnlyMsg = "Cannot modify a read-only dictoionary.";

        private IDictionary<TKey, TValue> dictionary;

        /// <summary>
        /// Constructs an empty read-only dictionary.
        /// </summary>
        public ReadOnlyDictionary()
        {
            this.dictionary = new Dictionary<TKey, TValue>();
        }

        /// <summary>
        /// Wraps a normal dictionary to make it read-only.
        /// </summary>
        /// <param name="dictionary">The dictionary bring wrapped.</param>
        public ReadOnlyDictionary(IDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary;
        }

        /// <summary>
        /// Adds a value to the dictionary.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="NotSupportedException">Thrown always because this dictionary is read-only.</exception>
        public void Add(TKey key, TValue value)
        {
            throw new NotSupportedException(ReadOnlyMsg);
        }

        /// <summary>
        /// Determines whether the dictionary has a value with a specific key.
        /// </summary>
        /// <param name="key">The key being tested.</param>
        /// <returns><c>true</c> if the key exists.</returns>
        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Returns a collection of the keys in the dictionary.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get { return dictionary.Keys; }
        }

        /// <summary>
        /// Removes a value from the dictionary.
        /// </summary>
        /// <param name="key">The key to be removed.</param>
        /// <returns><c>true</c> if the value was removed.</returns>
        /// <exception cref="NotSupportedException">Thrown always because this dictionary is read-only.</exception>
        public bool Remove(TKey key)
        {
            throw new NotSupportedException(ReadOnlyMsg);
        }

        /// <summary>
        /// Attempts to retrive the value for a key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">Returns as the value on success.</param>
        /// <returns><c>true</c> if the value was found and returned.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns a collection of the values in the dictionary.
        /// </summary>
        public ICollection<TValue> Values
        {
            get { return dictionary.Values; }
        }

        /// <summary>
        /// Accesses a value based on its key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        /// <exception cref="NotSupportedException">Thrown when the setter is called because this dictionary is read-only.</exception>
        public TValue this[TKey key]
        {
            get { return dictionary[key]; }
            set { throw new NotSupportedException(ReadOnlyMsg); }
        }

        /// <summary>
        /// Addes a key/value pair to the collection.
        /// </summary>
        /// <param name="item">The pair being added.</param>
        /// <exception cref="NotSupportedException">Thrown always because this dictionary is read-only.</exception>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException(ReadOnlyMsg);
        }

        /// <summary>
        /// Removes all items from the dictionary.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown always because this dictionary is read-only.</exception>
        public void Clear()
        {
            throw new NotSupportedException(ReadOnlyMsg);
        }

        /// <summary>
        /// Determines whether the collection contains a specific name/value pair.
        /// </summary>
        /// <param name="item">The pair being tested.</param>
        /// <returns><c>true</c> if the dictionary contains the pair.</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return dictionary.Contains(item);
        }

        /// <summary>
        /// Copies the name/value pairs from the collection into an array.
        /// </summary>
        /// <param name="array">The output array.</param>
        /// <param name="arrayIndex">The index where the first pair is to be written.</param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            dictionary.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Returns the number of items in the dictionary.
        /// </summary>
        public int Count
        {
            get { return dictionary.Count; }
        }

        /// <summary>
        /// Returns <c>true</c> if the dictionary is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Removes a name/value pair from the dictionary.
        /// </summary>
        /// <param name="item">The pair to be removed.</param>
        /// <returns><c>true</c> if the pair was removed.</returns>
        /// <exception cref="NotSupportedException">Thrown always because this dictionary is read-only.</exception>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException(ReadOnlyMsg);
        }

        /// <summary>
        /// Returns a generic type-safe enumerator for the dictionary.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        /// <summary>
        /// Returns a old-style enumerator for the dictionary.
        /// </summary>
        /// <returns>The enumerator.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (dictionary as System.Collections.IEnumerable).GetEnumerator();
        }
    }
}
