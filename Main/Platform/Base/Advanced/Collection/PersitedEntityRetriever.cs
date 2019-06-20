//-----------------------------------------------------------------------------
// FILE:        PersistedEntityRetriever.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Delegate used by PersitedEntityCache to retrieve an item that does not exist
//              in the cache.

using System;

namespace LillTek.Advanced
{
    /// <summary>
    /// Called by <see cref="PersistedEntityCache{TKey,TEntity}" /> to retrieve an item that does not currently
    /// exist in the cache from persistent.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <returns>The item or <c>null</c> if the item does not exist.</returns>
    public delegate TItem PersistedEntityRetriever<TItem>();
}
