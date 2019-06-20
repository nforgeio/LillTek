//-----------------------------------------------------------------------------
// FILE:        WeakEventListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an event listener that allows the event owner to
//              be garbage collected if the only remaining references to it
//              are event handlers.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Implements an event listener that allows the event owner to be garbage 
    /// collected if the only remaining references to it are event handlers.
    /// </summary>
    /// <typeparam name="TListener">Type of the instance listening to the event.</typeparam>
    /// <typeparam name="TSource">Type of the source/owner of the event.</typeparam>
    /// <typeparam name="TArgs">Type of the event arguments.</typeparam>
    /// <remarks>
    /// <para>
    /// This class can be used in situations where object instances are not
    /// being garbage collected because the only remaining references to it
    /// are event handlers, resulting in memory leaks.  This is typically used
    /// for general purpose Silverlight control implementations where it can
    /// be tough for the application to know when to remove event handlers
    /// to avoid this.
    /// </para>
    /// <para>
    /// The event owner class will use the constructor to create a weak listener, 
    /// specifying the action to performed when the event is raised or the
    /// and the action to performed when the listener is detached.
    /// </para>
    /// <para>
    /// The owner class will call <see cref="RaiseEvent" /> to raise the event
    /// (which will cause the event action to be performed and should call
    /// <see cref="Detach" /> when the listener is no longer listening to the
    /// event.  Here's an example:
    /// </para>
    /// <code language="cs">
    /// public class MyControl : ItemsList {
    /// 
    ///     private WeakEventListener   weakItemsSourceListener = null;
    ///     
    ///     // Called when the ItemsSource dependency property is modified.
    ///     private void OnItemsSourceChanged(IEnumerable oldSource,IEnumerable newValue) {
    ///     
    ///         var oldChanged = oldSource as INotifyCollectionChanged
    ///         var newChanged = newSource as INotifyCollectionChanged;
    ///         
    ///         if (oldChanged != null) {
    ///         
    ///            weakItemsSourceListener.Detach();
    ///            weakItemsSourceListener = null;
    ///         }
    ///         
    ///         if (newChanged != null) {
    ///         
    ///             weakItemsSourceListener = new WeakEventListener&lt;MyControl,object,NotifyCollectionChangedEventArgs&gt;(
    ///                                               this,
    ///                                               (listener,source,args) => instance.OnCollectionChanged(source,args),
    ///                                               (listener) => newChanged -= weakItemsSourceListener.RaiseEvent);
    ///                                               
    ///             newChanged.CollectionChanged += weakItemsSourceListener.RaiseEvent;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class WeakEventListener<TListener, TSource, TArgs>
        where TSource : class
    {
        private WeakReference                                           weakListener;
        private Action<TListener, TSource, TArgs>                       eventAction;
        private Action<WeakEventListener<TListener, TSource, TArgs>>    detachAction;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="listener">The object listening to the event.</param>
        private WeakEventListener(TListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            this.weakListener = new WeakReference(listener);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="listener">The object listening to the event.</param>
        /// <param name="eventAction">The action to be performed when the event is raised.</param>
        /// <param name="detachAction">The action to be performed when the listener detaches from the event.</param>
        public WeakEventListener(TListener listener,
                                 Action<TListener, TSource, TArgs> eventAction,
                                 Action<WeakEventListener<TListener, TSource, TArgs>> detachAction)
            : this(listener)
        {
            this.eventAction  = eventAction;
            this.detachAction = detachAction;
        }

        /// <summary>
        /// Called by the event source to invoke an action. />.
        /// </summary>
        /// <param name="source">The event source/owner.</param>
        /// <param name="args">The event arguments.</param>
        public void RaiseEvent(TSource source, TArgs args)
        {
            TListener listener = (TListener)weakListener.Target;

            if (listener == null)
            {
                Detach();
                return;
            }

            if (eventAction != null)
                eventAction(listener, source, args);
        }

        /// <summary>
        /// Detaches from the event, invoking the detach action. /> 
        /// if present.
        /// </summary>
        public void Detach()
        {
            if (detachAction == null)
            {
                detachAction(this);
                detachAction = null;
            }
        }
    }
}
