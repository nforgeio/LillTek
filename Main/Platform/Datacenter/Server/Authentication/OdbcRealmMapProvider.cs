//-----------------------------------------------------------------------------
// FILE:        OdbcRealmMapProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements IRealmMapProvider by loading authentication realm 
//              mappings from an ODBC datasource.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;

using LillTek.Common;
using LillTek.Data;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements <see cref="IRealmMapProvider" /> by loading authentication realm 
    /// mappings from an ODBC datasource
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider simply loads a list of provider mappings from a SQL
    /// database.  The database connection string and query are encoded
    /// into the configuration key named in a call to <see cref="Open" />.
    /// The configuration value must be formatted as:
    /// </para>
    /// <code language="none">
    ///     &lt;connection string&gt;$$&lt;query&gt;
    /// </code>
    /// <para>
    /// where <b>connection string</b> is the ODBC connection string and 
    /// <b>query</b> is the parameterless SQL query or stored procedure call 
    /// that returns the realm map as a result set.  The result set must be 
    /// formatted as:
    /// </para>
    /// <code language="none">
    /// Realm               ProviderType                                                                            Args                Query
    /// ------------------------------------------------------------------------------------------------------------------------------------------------
    /// lilltek.com         LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll     c:\passwords.txt
    /// auth.amex           LillTek.Datacenter.Server.OdbcAuthenticationExtension:LillTek.Datacenter.Server.dll                         {call Auth($(realm),$(account),$(password))}
    /// </code>
    /// <para>
    /// where <b>Realm</b> specifies the realm being mapped, <b>ProviderType</b> references the 
    /// type implementing <see cref="IAuthenticationExtension" /> that will be used to authenticate
    /// against the realm, <b>Args</b> are the arguments to tbe passed to authentication provider
    /// when it is instantiated and <b>Query</b> is the optional query to be used by the
    /// provider.
    /// </para>
    /// <note>
    /// The provider arguments may include environment variable macros.
    /// These macros will be expanded by <see cref="GetMap" /> before adding the
    /// entry to the realm map returned.  Environment variables are formatted as 
    /// <b>$(variable)</b>. See <see cref="EnvironmentVars" /> for a list of the
    /// built-in environment variables.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class OdbcRealmMapProvider : IRealmMapProvider, ILockable
    {
        private string                          conString = null;
        private string                          query     = null;
        private AuthenticationEngineSettings    engineSettings;

        /// <summary>
        /// Constructor.
        /// </summary>
        public OdbcRealmMapProvider()
        {
        }

        /// <summary>
        /// Establishes a session with the realm map provider.
        /// </summary>
        /// <param name="engineSettings">The associated authentication engine's settings.</param>
        /// <param name="key">
        /// The configuration key holding the ODBC connection string and query
        /// (see the remarks section for more information.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <b>key</b> parameter is passed as the fully qualified name of
        /// the configuration key that specifies the ODBC connection string as
        /// well as the SQL query to be used to retrieve the realm map from the
        /// data source.  The key value must be formatted as:
        /// </para>
        /// <code language="none">
        ///     &lt;connection string&gt;$$&lt;query&gt;
        /// </code>
        /// <para>
        /// where <b>connection string</b> is the ODBC connection string and 
        /// <b>query</b> is the SQL query or stored procedure call that returns
        /// the realm map as a result set.
        /// </para>
        /// <note>
        /// Every call to <see cref="Open" /> should be matched by a call to
        /// <see cref="Close" /> or <see cref="Dispose" />.
        /// </note>
        /// </remarks>
        /// <exception cref="AuthenticationException">Thrown if there's an opening the provider.</exception>
        public void Open(AuthenticationEngineSettings engineSettings, string key)
        {
            string      value;
            string[]    fields;

            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Provider is already open.");

                this.engineSettings = engineSettings;

                value = Config.Global.Get(key);
                if (value == null)
                    throw new AuthenticationException("Configuration key [{0}] not found.", key);

                fields = value.Split(new string[] { "$$" }, StringSplitOptions.None);
                if (fields.Length != 2)
                    throw new AuthenticationException("Expected [<ODBC connection string>$$<query>] in configuration key[{0}].", key);

                conString = fields[0].Trim();
                query     = fields[1].Trim();

                if (conString == string.Empty)
                    throw new AuthenticationException("Invalid ODBC connection string in key [{0}].", key);

                if (query == string.Empty)
                    throw new AuthenticationException("Missing query in key [{0}].", key);
            }
        }

        /// <summary>
        /// Closes the session with the realm map provider.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                conString = null;
                query = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the provider is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return conString != null; }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Queries the realm map provider for the current set of realm mappings.
        /// </summary>
        /// <returns>The list of realm mappings.</returns>
        /// <exception cref="AuthenticationException">Thrown if there's an error getting the map.</exception>
        public List<RealmMapping> GetMap()
        {
            OdbcConnection      dbCon  = null;
            OdbcDataReader      reader = null;
            List<RealmMapping>  map    = new List<RealmMapping>();
            OdbcCommand         cmd;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new AuthenticationException("Provider is closed.");

                dbCon = new OdbcConnection(conString);
                dbCon.Open();

                try
                {
                    cmd             = dbCon.CreateCommand();
                    cmd.CommandText = query;
                    cmd.CommandType = CommandType.Text;

                    reader = cmd.ExecuteReader();
                    while (reader.Read())
                        map.Add(new RealmMapping(engineSettings,
                                                 SqlHelper.AsString(reader["Realm"]),
                                                 Config.Parse(SqlHelper.AsString(reader["ProviderType"]), (System.Type)null),
                                                 ArgCollection.Parse(EnvironmentVars.Expand(SqlHelper.AsString(reader["Args"]))),
                                                 SqlHelper.AsString(reader["Query"])));
                }
                finally
                {
                    if (reader != null)
                        reader.Close();

                    dbCon.Close();
                }

                return map;
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
