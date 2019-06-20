//-----------------------------------------------------------------------------
// FILE:        NotifyPropertyChanged.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An implementation of INotifyPropertyChanged that can be used for
//              classes that don't need to derive from another base class.

using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace LillTek.Common
{
    /// <summary>
    /// An implementation of <see cref="INotifyPropertyChanged" /> that can be
    /// used for classes that don't need to derive from another base class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is optionally capable of raising the <see cref="PropertyChanged" />
    /// event on the user interface thread.  The default constructor <see cref="NotifyPropertyChanged()" />
    /// creates an instance that raises this event on the current thread.  To have the
    /// class raise the event on the UI thread, pass <c>true</c> to the <see cref="NotifyPropertyChanged(bool)" />
    /// constructor.
    /// </para>
    /// <note>
    /// <see cref="Helper.SetUIActionDispatcher" /> must have already been called to configure the global user interface 
    /// action dispatcher first, events may be raised on the UI thread.  This is typically 
    /// performed by downstream class libraries such as the various flavors of <b>LillTek.Xaml</b>.
    /// </note>
    /// <para>
    /// Then simply call <see cref="RaisePropertyChanged" />, passing the property name whenever an 
    /// instance property is changed or <c>null</c> when all properties have changed.
    /// </para>
    /// </remarks>
    [DataContract]
    public class NotifyPropertyChanged : INotifyPropertyChanged
    {
        private bool onUIThread;

        /// <summary>
        /// Constructs an instance that raises the <see cref="PropertyChanged" /> event on the same thread 
        /// that calls <see cref="RaisePropertyChanged" />.
        /// </summary>
        public NotifyPropertyChanged()
        {
            this.onUIThread = false;
        }

        /// <summary>
        /// Constructs an instance that optionally raises the <see cref="PropertyChanged" /> event on 
        /// the user interface thread or on the same thread that calls <see cref="RaisePropertyChanged" />.
        /// </summary>
        /// <param name="onUIThread">Pass <c>true</c> to raise the event on the user interface thread.</param>
        public NotifyPropertyChanged(bool onUIThread)
        {
            this.onUIThread = onUIThread;
        }

        /// <summary>
        /// Raised when an instance property value changes. 
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged" /> event for the named property.
        /// </summary>
        /// <param name="propertyName">The property name or <c>null</c> to indicate that all properties have changed.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the global UI action dispatcher has not yet been set by a call
        /// to <see cref="Helper.SetUIActionDispatcher" />
        /// </exception>
        /// <remarks>
        /// <para>
        /// The event will be raised on the user interface thread if <c>true</c> was passed to the
        /// parameterized constructor, otherwise the event will be raised on the current thread.
        /// </para>
        /// <note>
        /// <see cref="Helper.SetUIActionDispatcher" /> must have already been called to configure the global user interface 
        /// action dispatcher first, events may be raised on the UI thread.  This is typically 
        /// performed by downstream class libraries such as the various flavors of <b>LillTek.Xaml</b>.
        /// </note>
        /// </remarks>
        protected void RaisePropertyChanged(string propertyName)
        {
            var propertyChanged = PropertyChanged;

            if (propertyChanged == null)
                return;

            if (onUIThread)
                Helper.UIDispatch(() => propertyChanged(this, new PropertyChangedEventArgs(propertyName)));
            else
                propertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
