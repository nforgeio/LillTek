//-----------------------------------------------------------------------------
// FILE:        CollectionExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc extension methods.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace LillTek.Common
{
    /// <summary>
    /// Delegate used for cloning dictionary entry elements.
    /// </summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="entry">The entry to be cloned.</param>
    /// <returns>The cloned entry.</returns>
    public delegate KeyValuePair<TKey, TValue> DictionaryEntryCloner<TKey, TValue>(KeyValuePair<TKey, TValue> entry);

    /// <summary>
    /// Delegate used for cloning item collection elements.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="item">The item to be cloned.</param>
    /// <returns>The cloned item.</returns>
    public delegate TItem ItemCollectionCloner<TItem>(TItem item);

    /// <summary>
    /// Misc extension methods.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Replaces the current contents of a generic collection with the items with an enumerable collection.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="source">The source enumerable collection.</param>
        /// <param name="target">The target collection.</param>
        public static void CopyTo<TEntity>(this IEnumerable<TEntity> source, ICollection<TEntity> target)
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }

        /// <summary>
        /// Appends the items from an <see cref="IEnumerable{TEntity}" /> to an <see cref="ICollection{TEntity}" />.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="source">The source enumerable collection.</param>
        /// <param name="target">The target collection.</param>
        public static void AppendTo<TEntity>(this IEnumerable<TEntity> source, ICollection<TEntity> target)
        {
            foreach (var item in source)
                target.Add(item);
        }

        /// <summary>
        /// Randomly shuffles contents of the <see cref="IList{TEntity}" />.
        /// </summary>
        /// <typeparam name="TEntity">The list element type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <remarks>
        /// <note>
        /// This method uses <see cref="Random" /> as the random number source
        /// which will work in many instances but should not relied upon 
        /// when serious security is required.
        /// </note>
        /// </remarks>
        public static void Shuffle<TEntity>(this IList<TEntity> list)
        {            
            if (list.Count <= 1)
                return;     // Nothing to shuffle

            var rand = new Random(Environment.TickCount);

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var randIndex = rand.Next(i + 1);
                var temp      = list[i];

                // Swap

                list[i] = list[randIndex];
                list[randIndex] = temp;
            }
        }

        /// <summary>
        /// Randomly shuffles contents of the <see cref="IList{TEntity}" />.
        /// </summary>
        /// <typeparam name="TEntity">The list element type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <param name="seed">The seed value for the pseudo random sequence.</param>
        /// <remarks>
        /// <note>
        /// This method uses <see cref="Random" /> as the random number source
        /// which will work in many instances but should not relied upon 
        /// when serious security is required.
        /// </note>
        /// </remarks>
        public static void Shuffle<TEntity>(this IList<TEntity> list, int seed)
        {
            if (list.Count <= 1)
                return;     // Nothing to shuffle

            var rand = new Random(seed);

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var randIndex = rand.Next(i + 1);
                var temp      = list[i];

                // Swap

                list[i] = list[randIndex];
                list[randIndex] = temp;
            }
        }

        /// <summary>
        /// Searches the list from the beginning for the first item that satisfies a predicate.
        /// </summary>
        /// <typeparam name="TItem">The list item type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <param name="predicate">The search predicate.</param>
        /// <returns>The first item that for which the predicate returns <c>true</c> or <c>default(IItem)</c>.</returns>
        public static TItem Find<TItem>(this List<TItem> list, Predicate<TItem> predicate)
        {
            foreach (var item in list)
                if (predicate(item))
                    return item;

            return default(TItem);
        }

        /// <summary>
        /// Searches the list from the beginning for the first item that satisfies a predicate.
        /// </summary>
        /// <typeparam name="TItem">The list item type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <param name="predicate">The search predicate.</param>
        /// <returns>The first item that for which the predicate returns <c>true</c> or <c>default(IItem)</c>.</returns>
        public static TItem Find<TItem>(this ObservableCollection<TItem> list, Predicate<TItem> predicate)
        {
            foreach (var item in list)
                if (predicate(item))
                    return item;

            return default(TItem);
        }

        /// <summary>
        /// Determines whether an item that satisfies a predicate exists in the collection.
        /// </summary>
        /// <typeparam name="TItem">The list item type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <param name="predicate">The search predicate.</param>
        /// <returns><c>true</c> if at least one item in the collection satisfies the predicate.</returns>
        public static bool Exists<TItem>(this List<TItem> list, Predicate<TItem> predicate)
        {
            foreach (var item in list)
                if (predicate(item))
                    return true;

            return false;
        }

        /// <summary>
        /// Determines whether an item satisfies matches a predicate exists in the collection.
        /// </summary>
        /// <typeparam name="TItem">The list item type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <param name="predicate">The search predicate.</param>
        /// <returns>The first item that for which the predicate returns <c>true</c> or <c>default(IItem)</c>.</returns>
        public static bool Exists<TItem>(this ObservableCollection<TItem> list, Predicate<TItem> predicate)
        {
            foreach (var item in list)
                if (predicate(item))
                    return true;

            return false;
        }

        /// <summary>
        /// Returns a read-only version of an existing dictionary.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="dictionary">The current dictonary.</param>
        /// <returns>The read-only dictionary.</returns>
        public static IDictionary<TKey, TValue> ToReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return new ReadOnlyDictionary<TKey, TValue>(dictionary);
        }

        /// <summary>
        /// Removes dictionary entries selected by a predicate.
        /// </summary>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <typeparam name="TValue">The dictionary value type.</typeparam>
        /// <param name="dictionary">The current dictionary.</param>
        /// <param name="predicate">The selection predicate that returns <c>true</c> for entries to be removed.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate" /> is <c>null</c>.</exception>
        public static void Remove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Predicate<KeyValuePair<TKey, TValue>> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException("predicate");

            var delList = new List<TKey>();

            foreach (var entry in dictionary)
            {
                if (predicate(entry))
                    delList.Add(entry.Key);
            }

            foreach (var key in delList)
                dictionary.Remove(key);
        }

        /// <summary>
        /// Creates a shallow clone of the dictionary.
        /// </summary>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <typeparam name="TValue">The dictionary value type.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="comparer">The equality comparer or <c>null</c>.</param>
        /// <returns>The shallow clone.</returns>
        public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            var clone = new Dictionary<TKey, TValue>(comparer);

            foreach (var entry in dictionary)
                clone.Add(entry.Key, entry.Value);

            return clone;
        }

        /// <summary>
        /// Creates a clone of the dictionary an optional callback to create the cloned entries.
        /// </summary>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <typeparam name="TValue">The dictionary value type.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="comparer">The equality comparer or <c>null</c>.</param>
        /// <param name="entryCloner">A callback the handlings entry cloner or <c>null</c>.</param>
        /// <returns>The shallow clone.</returns>
        public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
                                                                 IEqualityComparer<TKey> comparer,
                                                                 DictionaryEntryCloner<TKey, TValue> entryCloner)
        {
            var clone = new Dictionary<TKey, TValue>(comparer);

            if (entryCloner == null)
            {
                foreach (var entry in dictionary)
                    clone.Add(entry.Key, entry.Value);
            }
            else
            {
                foreach (var entry in dictionary)
                {
                    var clonedEntry = entryCloner(entry);

                    clone.Add(clonedEntry.Key, clonedEntry.Value);
                }
            }

            return clone;
        }

        /// <summary>
        /// Removes list items selected by a predicate.
        /// </summary>
        /// <typeparam name="TItem">The list item type.</typeparam>
        /// <param name="list">The current list.</param>
        /// <param name="predicate">The selection predicate that returns <c>true</c> for items to be removed.</param>
        public static void Remove<TItem>(this IList<TItem> list, Predicate<TItem> predicate)
        {
            // Note that I'm going to delete by index rather than using the
            // simple Remove(TItem) method for better performance (especially
            // for very large lists).

            if (predicate == null)
                throw new ArgumentNullException("predicate");

            var delIndexes = new List<int>();
            var offset = 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                    delIndexes.Add(i);
            }

            foreach (var delIndex in delIndexes)
                list.RemoveAt(delIndex + offset--);
        }

        /// <summary>
        /// Returns a shallow clone of the list.
        /// </summary>
        /// <typeparam name="TItem">The list item type.</typeparam>
        /// <param name="list">The source list.</param>
        /// <returns>The shallow clone.</returns>
        public static List<TItem> Clone<TItem>(this IList<TItem> list)
        {
            var clone = new List<TItem>(list.Count);

            clone.AddRange(list);
            return clone;
        }

        /// <summary>
        /// Creates a clone of the item collection using an optional callback to create the cloned entries.
        /// </summary>
        /// <typeparam name="TItem">The item type.</typeparam>
        /// <param name="list">The source list.</param>
        /// <param name="itemCloner">The item cloner callback or <c>null</c>.</param>
        /// <returns></returns>
        public static List<TItem> Clone<TItem>(this IList<TItem> list, ItemCollectionCloner<TItem> itemCloner)
        {
            var clone = new List<TItem>(list.Count);

            if (itemCloner == null)
                clone.AddRange(list);
            else
            {
                foreach (var item in list)
                    clone.Add(itemCloner(item));
            }

            return clone;
        }

        //---------------------------------------------------------------------
        // StringBuilder extensions

        /// <summary>
        /// Appends a formatted line of text.
        /// </summary>
        /// <param name="sb">The current <see cref="StringBuilder"/>.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">The format arguments.</param>
        public static void AppendFormatLine(this StringBuilder sb, string format, params object[] args)
        {
            sb.AppendLine(string.Format(format, args));
        }

        /// <summary>
        /// Starts a new line of output if the string builder is not already at
        /// the beginning of a new line.
        /// </summary>
        /// <param name="sb">The current <see cref="StringBuilder"/>.</param>
        public static void ClearLine(this StringBuilder sb)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                sb.AppendLine();
        }
    }
}
