//-----------------------------------------------------------------------------
// FILE:        PropertyChangeMap.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to associate two properties of an object implementing
//              INotifyPropertyChanged and/or INotifyPropertyChanging such
//              that changes to one property will cause automatic changes
//              notification for the associated property.

using System;
using System.ComponentModel;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Used to associate two properties of an object implementing 
    /// <b>INotifyPropertyChanged</b> and/or <b>INotifyPropertyChanging</b>
    /// such that changes to one property will cause automatic change
    /// notification for the associated property.
    /// </summary>
    /// <typeparam name="TEntity">
    /// Type of the entity that owns the properties being associated.
    /// This must be a reference a class that implements <see cref="IPropertyChange" />
    /// and at least one of <b>INotifyPropertyChanged</b> or
    /// <b>INotifyPropertyChanging</b> for non-Silverlight applications.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// This class is useful in two different situations.  In the first
    /// situation, you need to keep to logicially equivalent properties
    /// in sync.  One common case happens when you want to map a duration
    /// property expressed in hours or minutes using a primitive type
    /// in an entity generated from a database into a <see cref="TimeSpan" />
    /// property.   Use the <see cref="TwoWay" /> method to add a two-way 
    /// association such as this.
    /// </para>
    /// <para>
    /// The other situation is when you need to create a one-way association
    /// between one or more source properties and a dependant property.
    /// An example of this is when you'd like to associate the <b>FirstName</b>
    /// and <b>LastName</b> properties with a readonly <b>DisplayName</b> property,
    /// where a change to either the first or last name property will update
    /// the display name.  Use the <see cref="OneWay" /> method to add
    /// this kind of association.
    /// </para>
    /// <para>
    /// The map works by listening to the entity's <b>PropertyChange</b>
    /// and/or <b>INotifyPropertyChanging.PropertyChanging</b> events and
    /// then calling the entity's custom <see cref="IPropertyChange.OnPropertyChange" />
    /// method which will generate the approriate change notifications for the
    /// related target property.
    /// </para>
    /// <para>
    /// Note that this class can manage multiple property associations
    /// for a given entity instance.  Simply create an instance, passing the
    /// entity, and than call <see cref="OneWay" /> and/or <see cref="TwoWay" />
    /// as many times as is necessary.
    /// </para>
    /// <note>
    /// Entity associations should be created as early as possible in the lifecycle
    /// of the entity typically within the entity constructor or <b>OnCreated()</b>
    /// method.
    /// </note>
    /// <note>
    /// You should avoid saving <see cref="PropertyChangeMap{TEntity}" /> references 
    /// anywhere but within the associated entity, and in fact, it shouldn't really be
    /// necessary to hold a reference at all, once the associations are created since
    /// the lifecycle of the associations will match the lifetime of the entity.
    /// </note>
    /// <note>
    /// The current implementation is somewhat simplistic and does not support
    /// property association chains; where one property association triggers 
    /// another association.
    /// </note>
    /// </remarks>
    public sealed class PropertyChangeMap<TEntity>
        where TEntity : class, IPropertyChange
    {
        //---------------------------------------------------------------------
        // Private types

        private class OneWayAssociation
        {
            public string       TargetProperty;
            public string[]     SourceProperties;

            public OneWayAssociation(string targetProperty, params string[] sourceProperties)
            {
                this.TargetProperty = targetProperty;
                this.SourceProperties = sourceProperties;
            }
        }

        private class TwoWayAssociation
        {
            public string       Property1;
            public string       Property2;

            public TwoWayAssociation(string property1, string property2)
            {
                this.Property1 = property1;
                this.Property2 = property2;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string NullEntityMsg = "Entity cannot be null.";

        private TEntity         entity;             // The entity
        private List<object>    associations;       // The property associations
        private bool            processing;         // True we're processing a change notification.
                                                    // Used to avoid infinite recursion.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entity">The entity instance whose properties are being mapped.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if the entity doesn't implement either of <b>INotifyPropertyChanged</b>
        /// or <b>INotifyPropertyChanging</b>.
        /// </exception>
        public PropertyChangeMap(TEntity entity)
        {
            INotifyPropertyChanged changedInstance = entity as INotifyPropertyChanged;
#if !SILVERLIGHT
            INotifyPropertyChanging changingInstance = entity as INotifyPropertyChanging;
#endif

            if (entity == null)
                throw new ArgumentNullException("entity");

            if (changedInstance == null
#if !SILVERLIGHT
 && changingInstance == null
#endif
)
            {
                throw new ArgumentException("Entity must implement one of INotifyPropertyChanged or INotifyPropertyChanging.");
            }

            this.processing = false;
            this.associations = null;
            this.entity = entity;

            if (changedInstance != null)
                changedInstance.PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);

#if !SILVERLIGHT
            if (changingInstance != null)
                changingInstance.PropertyChanging += new PropertyChangingEventHandler(OnPropertyChanging);
#endif
        }

        /// <summary>
        /// Creates a one-way association between one or more source properties and
        /// a target property.
        /// </summary>
        /// <param name="targetProperty">Name of the target property.</param>
        /// <param name="sourceProperties">The source property names.</param>
        public void OneWay(string targetProperty, params string[] sourceProperties)
        {
            if (entity == null)
                throw new InvalidOperationException(NullEntityMsg);

            if (associations == null)
                associations = new List<object>();

            associations.Add(new OneWayAssociation(targetProperty, sourceProperties));
        }

        /// <summary>
        /// Creates a two-way association between two properties of the entity.
        /// </summary>
        /// <param name="property1">Name of the first property.</param>
        /// <param name="property2">Name of the second property.</param>
        /// <exception cref="ArgumentException">Thrown if there's a problem with one or more parameters.</exception>
        public void TwoWay(string property1, string property2)
        {
            if (entity == null)
                throw new InvalidOperationException(NullEntityMsg);

            if (property1 == null)
                throw new ArgumentNullException("property1");

            if (property2 == null)
                throw new ArgumentNullException("property2");

            if (property1 == property2)
                throw new ArgumentException("Cannot pass two identical property names.");

            if (associations == null)
                associations = new List<object>();

            associations.Add(new TwoWayAssociation(property1, property2));
        }

        /// <summary>
        /// Performs the actual associaton tasks by calling the entity's
        /// <see cref="IPropertyChange.OnPropertyChange" /> method.
        /// </summary>
        /// <param name="changing">
        /// <c>true</c> for <b>PropertyChanging</b>, false for <b></b>PropertyChanged.
        /// </param>
        /// <param name="propertyName">The name of the changed property.</param>
        private void OnChange(bool changing, string propertyName)
        {
            if (processing)
                return;     // Avoid infinite recursion

            if (string.IsNullOrWhiteSpace(propertyName))
                return;     // Ignore empty property names

            try
            {
                processing = true;

                if (associations == null)
                    return;     // No associations;

                // $todo(jeff.lill): delete this

                if (propertyName == "Duration")
                    changing = !!changing;

                foreach (var association in associations)
                {
                    OneWayAssociation oneWay;
                    TwoWayAssociation twoWay;

                    oneWay = association as OneWayAssociation;
                    if (oneWay != null)
                    {
                        foreach (var sourceProperty in oneWay.SourceProperties)
                            if (sourceProperty == propertyName)
                            {
                                entity.OnPropertyChange(changing, oneWay.TargetProperty, sourceProperty);
                                break;
                            }

                        continue;
                    }

                    twoWay = association as TwoWayAssociation;
                    if (twoWay != null)
                    {
                        if (twoWay.Property1 == propertyName)
                            entity.OnPropertyChange(changing, twoWay.Property2, propertyName);
                        else if (twoWay.Property2 == propertyName)
                            entity.OnPropertyChange(changing, twoWay.Property1, propertyName);

                        continue;
                    }
                }
            }
            finally
            {

                processing = false;
            }
        }

        //---------------------------------------------------------------------
        // Event handlers

        /// <summary>
        /// Handles <see cref="INotifyPropertyChanged" /> notifications.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            OnChange(false, args.PropertyName);
        }

#if !SILVERLIGHT

        /// <summary>
        /// Handles <see cref="INotifyPropertyChanging" /> notifications.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPropertyChanging(object sender, PropertyChangingEventArgs args)
        {
            OnChange(true, args.PropertyName);
        }

#endif // !SILVERLIGHT
    }
}
