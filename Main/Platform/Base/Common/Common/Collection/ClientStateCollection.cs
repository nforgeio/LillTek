//-----------------------------------------------------------------------------
// FILE:        ClientStateCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides persistent storage for client side application state
//              in Silverlight isolated storage.

using System;
using System.IO;

#if SILVERLIGHT
using System.IO.IsolatedStorage;
#endif

using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Common
{
#if SILVERLIGHT
    /// <summary>
    /// Provides persistent storage for client side application state
    /// in isolated storage for Silverlight applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applications will use this class for maintaining application
    /// state as name/value pairs persisted to the file system.  This
    /// class inherits from <see cref="ArgCollection" /> and thus
    /// exposes methods to conviently save and retrieve values with
    /// many common data types.
    /// </para>
    /// <para>
    /// The constructor opens and parses the isolated file, if one
    /// exists.  One constructor override allows for the explicit 
    /// specification of the file name, the other override simply
    /// default to <b>ClientState.ini</b>.
    /// </para>
    /// <para>
    /// The application state is stored as UTF-8 encoded name/value 
    /// pairs.  The name is separated from the value by an equal
    /// sign <b>(=)</b> and the pairs are separated from each other
    /// with a <b>LF</b> character.  This means that property names
    /// may not include an equal sign.
    /// </para>
    /// <para>
    /// Changes to the client state are maintained in memory.  Call
    /// <see cref="Save" /> to persist any changes.
    /// </para>
    /// </remarks>
#else
    /// <summary>
    /// Provides persistent storage for client side application state
    /// in a text file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applications will use this class for maintaining application
    /// state as name/value pairs persisted to the file system.  This
    /// class inherits from <see cref="ArgCollection" /> and thus
    /// exposes methods to conviently save and retrieve values with
    /// many common data types.
    /// </para>
    /// <para>
    /// The constructor opens and parses the isolated file, if one
    /// exists.  One constructor override allows for the explicit 
    /// specification of the file name, the other override simply
    /// default to <b>ClientState.txt</b>.
    /// </para>
    /// <para>
    /// The application state is stored as UTF-8 encoded name/value 
    /// pairs.  The name is separated from the value by an equal
    /// sign <b>(=)</b> and the pairs are separated from each other
    /// with a <b>LF</b> character.  This means that property names
    /// may not include an equal sign.
    /// </para>
    /// <para>
    /// Changes to the client state are maintained in memory.  Call
    /// <see cref="Save" /> to persist any changes.
    /// </para>
    /// </remarks>
#endif
    public class ClientStateCollection : ArgCollection
    {

        private string path;

#if SILVERLIGHT
        /// <summary>
        /// Opened the default client state file called <b>ClientState.ini</b>.
        /// </summary>
        public ClientStateCollection() 
            : this("ClientState.ini") 
        {
        }
#endif

#if SILVERLIGHT
        /// <summary>
        /// Opens the client state file whose path in isolated storage is specified.
        /// </summary>
        /// <param name="path">The isolated file path.</param>
#else
        /// <summary>
        /// Opens the client state file whose path is specified.
        /// </summary>
        /// <param name="path">The file path.</param>
#endif
        public ClientStateCollection(string path)
            : base('=', '\n')
        {
            this.path = path;

            try
            {
#if SILVERLIGHT
                using (var store = IsolatedStorageFile.GetUserStoreForApplication()) {

                    if (!store.FileExists(path))
                        return;

                    using (var reader = new StreamReader(store.OpenFile(path,FileMode.Open,FileAccess.Read),Encoding.UTF8)) {
                    
                        base.Load(reader.ReadToEnd());
                    }
                }
#else
                Helper.CreateFileTree(path);

                if (File.Exists(path))
                {
                    using (var reader = new StreamReader(path, Encoding.UTF8))
                    {
                        base.Load(reader.ReadToEnd());
                    }
                }
#endif
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Returns the instance cast into the base <see cref="ArgCollection" /> type.
        /// This is use for derived classes that need to access extension methods.
        /// </summary>
        protected ArgCollection BaseArgs
        {
            get { return (ArgCollection)this; }
        }

        /// <summary>
        /// Writes the current client state to the file system.
        /// </summary>
        public void Save()
        {
            try
            {
#if SILVERLIGHT
                using (var store = IsolatedStorageFile.GetUserStoreForApplication()) 
                {
                    using (var writer = new StreamWriter(store.OpenFile(path,FileMode.Create,FileAccess.ReadWrite))) 
                    {
                        writer.Write(base.ToString());
                    }
                }
#else
                Helper.CreateFileTree(path);

                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    writer.Write(base.ToString());
                }
#endif
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }
    }
}
