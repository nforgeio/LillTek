//-----------------------------------------------------------------------------
// FILE:        LifecycleManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages the disposal of a set of [IDisposable] objects.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Manages the disposal of a set of <see cref="IDisposable"/> objects.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    public class LifecycleManager : IDisposable
    {
        private List<IDisposable> items;

        /// <summary>
        /// Constructor.
        /// </summary>
        public LifecycleManager()
        {
            this.items = new List<IDisposable>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~LifecycleManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes any items being managed in the reverse order that they were added.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Disposes any items being managed in the reverse order that they were added.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        protected void Dispose(bool disposing)
        {
            lock (items)
            {
                items.Reverse();

                foreach (var item in items)
                {
                    item.Dispose();
                }

                items.Clear();
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Adds the disposable item passed to the set of items being managed.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        public void Add(IDisposable item)
        {
            if (item == null)
            {
                return;
            }

            lock (items)
            {
                items.Add(item);
            }
        }

        /// <summary>
        /// Removes and item from the set of items being managed and by default,
        /// disposes the item.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <param name="dontDispose">Pass <c>true</c> if the item is not to be disposed.</param>
        public void Remove(IDisposable item, bool dontDispose = false)
        {
            if (item == null)
            {
                return;
            }

            lock (items)
            {
                if (!items.Remove(item))
                {
                    throw new ArgumentException("The item requested is not present in the [LifecycleManager] instance and will not be disposed.");
                }

                if (!dontDispose)
                {
                    item.Dispose();
                }
            }
        }
    }
}
