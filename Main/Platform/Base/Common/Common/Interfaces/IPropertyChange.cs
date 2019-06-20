//-----------------------------------------------------------------------------
// FILE:        IPropertyChange.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implemented by entities managed by PropertyChangeMap to
//              generate change notifications when associated properties
//              are changed.

using System;
using System.ComponentModel;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Implemented by entities managed by PropertyChangeMap to generate change 
    /// notifications when associated properties are changed.
    /// </summary>
    public interface IPropertyChange
    {
        /// <summary>
        /// Called by <see cref="PropertyChangeMap{TEntity}" /> when a change noltification
        /// is raised for source property that is associated with a target property.
        /// This method must raise the appropriate change notifications for the
        /// target property.
        /// </summary>
        /// <param name="changing">
        /// <c>true</c> if <b>PropertyChanging</b> was detected, 
        /// <c>false</c> for <b>PropertyChanged</b>
        /// </param>
        /// <param name="targetProperty">Name of the associated target property.</param>
        /// <param name="sourceProperty">Name of the changes source property.</param>
        void OnPropertyChange(bool changing, string targetProperty, string sourceProperty);
    }
}
