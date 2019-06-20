//-----------------------------------------------------------------------------
// FILE:        _PropertyChangeMap.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the PropertyChangeMap class.

using System;
using System.Diagnostics;
using System.ComponentModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _PropertyChangeMap
    {
        //---------------------------------------------------------------------
        // Test entity

        public class Entity : INotifyPropertyChanged, INotifyPropertyChanging, IPropertyChange
        {
            private string firstName;
            private string lastName;
            private double ageSeconds;

            public string FirstName
            {
                get { return firstName; }

                set
                {
                    firstName = value;
                    RaisePropertyChanged("FirstName");
                    RaisePropertyChanging("FirstName");
                }
            }

            public string LastName
            {
                get { return lastName; }

                set
                {
                    lastName = value;
                    RaisePropertyChanged("LastName");
                    RaisePropertyChanging("LastName");
                }
            }

            public string Name
            {
                get
                {
                    string name = string.Empty;

                    if (!string.IsNullOrEmpty(this.FirstName))
                        name += this.FirstName;

                    if (!string.IsNullOrEmpty(this.LastName))
                        name += " " + this.LastName;

                    return name;
                }
            }

            public double AgeSeconds
            {
                get { return ageSeconds; }

                set
                {
                    ageSeconds = value;
                    RaisePropertyChanged("AgeSeconds");
                    RaisePropertyChanging("AgeSeconds");
                }
            }

            public TimeSpan Age
            {
                get { return TimeSpan.FromSeconds(ageSeconds); }

                set
                {
                    this.AgeSeconds = value.TotalSeconds;
                    RaisePropertyChanged("Age");
                    RaisePropertyChanging("Age");
                }
            }

            //-----------------------------------------------------------------
            // IPropertyChanged implementation

            /// <summary>
            /// Fired when an instance property is changed.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// Fires <see cref="PropertyChanged" /> when an instance property is changed.
            /// </summary>
            /// <param name="propertyName">The property name.</param>
            private void RaisePropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }

            //-----------------------------------------------------------------
            // IPropertyChanging implementation

            /// <summary>
            /// Fired when an instance property is changing.
            /// </summary>
            public event PropertyChangingEventHandler PropertyChanging;

            /// <summary>
            /// Fires <see cref="PropertyChanged" /> when an instance property is changed.
            /// </summary>
            /// <param name="propertyName">The property name.</param>
            private void RaisePropertyChanging(string propertyName)
            {
                if (PropertyChanging != null)
                    PropertyChanging(this, new PropertyChangingEventArgs(propertyName));
            }

            //-----------------------------------------------------------------
            // IPropertyChange implementation

            /// <summary>
            /// Called by <see cref="PropertyChangeMap" /> when a change noltification
            /// is raised for source property that is associated with a target property.
            /// This method must raise the appropriate change notifications for the
            /// target property.
            /// </summary>
            /// <param name="changing">
            /// <c>true</c> if <see cref="INotifyPropertyChanging.PropertyChanging" />
            /// was detected, <c>false</c> for <see cref="INotifyPropertyChanged.PropertyChanged" />
            /// </param>
            /// <param name="targetProperty">Name of the associated target property.</param>
            /// <param name="sourceProperty">Name of the changes source property.</param>
            public void OnPropertyChange(bool changing, string targetProperty, string sourceProperty)
            {
                if (!changing)
                {
                    RaisePropertyChanged(targetProperty);
                    RaisePropertyChanging(targetProperty);
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PropertyChangeMap_OneWay_Changed()
        {
            PropertyChangeMap<Entity> map;
            Entity entity;
            bool nameChanged;

            entity = new Entity();
            map = new PropertyChangeMap<Entity>(entity);

            map.OneWay("Name", "FirstName", "LastName");

            entity.PropertyChanged += new PropertyChangedEventHandler((s, a) =>
            {
                if (a.PropertyName == "Name")
                    nameChanged = true;
            });

            nameChanged = false;
            entity.FirstName = null;
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("", entity.Name);

            nameChanged = false;
            entity.LastName = null;
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("", entity.Name);

            nameChanged = false;
            entity.FirstName = "Jeff";
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("Jeff", entity.Name);

            nameChanged = false;
            entity.LastName = "Lill";
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("Jeff Lill", entity.Name);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PropertyChangeMap_OneWay_Changing()
        {
            PropertyChangeMap<Entity> map;
            Entity entity;
            bool nameChanging;

            entity = new Entity();
            map = new PropertyChangeMap<Entity>(entity);

            map.OneWay("Name", "FirstName", "LastName");

            entity.PropertyChanging += new PropertyChangingEventHandler((s, a) =>
            {
                if (a.PropertyName == "Name")
                    nameChanging = true;
            });

            nameChanging = false;
            entity.FirstName = null;
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("", entity.Name);

            nameChanging = false;
            entity.LastName = null;
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("", entity.Name);

            nameChanging = false;
            entity.FirstName = "Jeff";
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("Jeff", entity.Name);

            nameChanging = false;
            entity.LastName = "Lill";
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("Jeff Lill", entity.Name);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PropertyChangeMap_TwoWay_Changed()
        {
            PropertyChangeMap<Entity> map;
            Entity entity;
            bool ageChanged;
            bool ageSecondsChanged;

            entity = new Entity();
            map = new PropertyChangeMap<Entity>(entity);

            map.TwoWay("Age", "AgeSeconds");

            entity.PropertyChanged += new PropertyChangedEventHandler((s, a) =>
            {
                switch (a.PropertyName)
                {
                    case "Age":

                        ageChanged = true;
                        break;

                    case "AgeSeconds":

                        ageSecondsChanged = true;
                        break;
                }
            });

            ageChanged = false;
            ageSecondsChanged = false;

            entity.Age = TimeSpan.FromSeconds(2.0);
            Assert.IsTrue(ageChanged);
            Assert.IsTrue(ageSecondsChanged);
            Assert.AreEqual(TimeSpan.FromSeconds(2.0), entity.Age);
            Assert.AreEqual(2.0, entity.AgeSeconds);

            ageChanged = false;
            ageSecondsChanged = false;

            entity.AgeSeconds = 3.0;
            Assert.IsTrue(ageChanged);
            Assert.IsTrue(ageSecondsChanged);
            Assert.AreEqual(TimeSpan.FromSeconds(3.0), entity.Age);
            Assert.AreEqual(3.0, entity.AgeSeconds);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PropertyChangeMap_TwoWay_Changing()
        {
            PropertyChangeMap<Entity> map;
            Entity entity;
            bool ageChanging;
            bool ageSecondsChanging;

            entity = new Entity();
            map = new PropertyChangeMap<Entity>(entity);

            map.TwoWay("Age", "AgeSeconds");

            entity.PropertyChanging += new PropertyChangingEventHandler((s, a) =>
            {
                switch (a.PropertyName)
                {
                    case "Age":

                        ageChanging = true;
                        break;

                    case "AgeSeconds":

                        ageSecondsChanging = true;
                        break;
                }
            });

            ageChanging = false;
            ageSecondsChanging = false;

            entity.Age = TimeSpan.FromSeconds(2.0);
            Assert.IsTrue(ageChanging);
            Assert.IsTrue(ageSecondsChanging);
            Assert.AreEqual(TimeSpan.FromSeconds(2.0), entity.Age);
            Assert.AreEqual(2.0, entity.AgeSeconds);

            ageChanging = false;
            ageSecondsChanging = false;

            entity.AgeSeconds = 3.0;
            Assert.IsTrue(ageChanging);
            Assert.IsTrue(ageSecondsChanging);
            Assert.AreEqual(TimeSpan.FromSeconds(3.0), entity.Age);
            Assert.AreEqual(3.0, entity.AgeSeconds);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PropertyChangeMap_Multiple_Changed()
        {
            PropertyChangeMap<Entity> map;
            Entity entity;
            bool nameChanged;
            bool ageChanged;
            bool ageSecondsChanged;

            entity = new Entity();
            map = new PropertyChangeMap<Entity>(entity);

            map.OneWay("Name", "FirstName", "LastName");
            map.TwoWay("Age", "AgeSeconds");

            entity.PropertyChanged += new PropertyChangedEventHandler((s, a) =>
            {
                switch (a.PropertyName)
                {
                    case "Name":

                        nameChanged = true;
                        break;

                    case "Age":

                        ageChanged = true;
                        break;

                    case "AgeSeconds":

                        ageSecondsChanged = true;
                        break;
                }
            });

            nameChanged = false;
            entity.FirstName = null;
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("", entity.Name);

            nameChanged = false;
            entity.LastName = null;
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("", entity.Name);

            nameChanged = false;
            entity.FirstName = "Jeff";
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("Jeff", entity.Name);

            nameChanged = false;
            entity.LastName = "Lill";
            Assert.IsTrue(nameChanged);
            Assert.AreEqual("Jeff Lill", entity.Name);

            ageChanged = false;
            ageSecondsChanged = false;

            entity.Age = TimeSpan.FromSeconds(2.0);
            Assert.IsTrue(ageChanged);
            Assert.IsTrue(ageSecondsChanged);
            Assert.AreEqual(TimeSpan.FromSeconds(2.0), entity.Age);
            Assert.AreEqual(2.0, entity.AgeSeconds);

            ageChanged = false;
            ageSecondsChanged = false;

            entity.AgeSeconds = 3.0;
            Assert.IsTrue(ageChanged);
            Assert.IsTrue(ageSecondsChanged);
            Assert.AreEqual(TimeSpan.FromSeconds(3.0), entity.Age);
            Assert.AreEqual(3.0, entity.AgeSeconds);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PropertyChangeMap_Multiple_Changing()
        {
            PropertyChangeMap<Entity> map;
            Entity entity;
            bool nameChanging;
            bool ageChanging;
            bool ageSecondsChanging;

            entity = new Entity();
            map = new PropertyChangeMap<Entity>(entity);

            map.OneWay("Name", "FirstName", "LastName");
            map.TwoWay("Age", "AgeSeconds");

            entity.PropertyChanging += new PropertyChangingEventHandler((s, a) =>
            {
                switch (a.PropertyName)
                {
                    case "Name":

                        nameChanging = true;
                        break;

                    case "Age":

                        ageChanging = true;
                        break;

                    case "AgeSeconds":

                        ageSecondsChanging = true;
                        break;
                }
            });

            nameChanging = false;
            entity.FirstName = null;
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("", entity.Name);

            nameChanging = false;
            entity.LastName = null;
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("", entity.Name);

            nameChanging = false;
            entity.FirstName = "Jeff";
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("Jeff", entity.Name);

            nameChanging = false;
            entity.LastName = "Lill";
            Assert.IsTrue(nameChanging);
            Assert.AreEqual("Jeff Lill", entity.Name);

            ageChanging = false;
            ageSecondsChanging = false;

            entity.Age = TimeSpan.FromSeconds(2.0);
            Assert.IsTrue(ageChanging);
            Assert.IsTrue(ageSecondsChanging);
            Assert.AreEqual(TimeSpan.FromSeconds(2.0), entity.Age);
            Assert.AreEqual(2.0, entity.AgeSeconds);

            ageChanging = false;
            ageSecondsChanging = false;

            entity.AgeSeconds = 3.0;
            Assert.IsTrue(ageChanging);
            Assert.IsTrue(ageSecondsChanging);
            Assert.AreEqual(TimeSpan.FromSeconds(3.0), entity.Age);
            Assert.AreEqual(3.0, entity.AgeSeconds);
        }
    }
}

