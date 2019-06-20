//-----------------------------------------------------------------------------
// FILE:        IDomainEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines common methods and properties that can be used to extend
//              proxy classes that map domain entities to persistant storage.

using System;

namespace LillTek.Common {

    /// <summary>
    /// Defines common methods and properties that can be used to extend
    /// proxy classes that map domain entities to persistant storage.
    /// </summary>
    public interface IDomainEntity {

        /// <summary>
        /// Indicates whether or not the entity has been persisted to storage.
        /// </summary>
        /// <remarks>
        /// <note>
        /// For proxies generated for SQL Server databases with an <b>identity</b> ID column, 
        /// this property will typically check the value of the ID, where ID=0 indicates that
        /// the entity has not been persisted.
        /// </note>
        /// </remarks>
        bool IsPersisted { get; }
    }
}
