//-----------------------------------------------------------------------------
// FILE:        ConfigRealmMapProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements IRealmMapProvider by loading authentication realm 
//              mappings from the application configuration.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements <see cref="IRealmMapProvider" /> by loading authentication realm mappings 
    /// from the application configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider simply loads a static list of provider mappings from the
    /// application's configuration settings.  The mappings are loaded from
    /// the configuration setting array specified by the <b>key</b> parameter.
    /// Each element in the array specifies a single realm mapping.  The format
    /// of each is mapping is:
    /// </para>
    /// <code language="none">
    ///     &lt;realm&gt;$$&lt;extension typeref&gt;$$&lt;args&gt;$$&lt;query&gt;
    /// </code>
    /// <para>
    /// where <b>realm</b> identifies the authentication realm, <b>extension typeref</b> 
    /// specifies the type implementing <see cref="IAuthenticationExtension" /> formatted as
    /// specified for <see cref="Config.Parse(string,System.Type)" />, <b>key</b>
    /// are the provider arguments and <b>query</b> is the optional provider query
    /// string.  Note the used of <b>$$</b> as field separators.
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
    public sealed class ConfigRealmMapProvider : IRealmMapProvider, ILockable
    {
        private List<RealmMapping>              realmMap = null;
        private AuthenticationEngineSettings    engineSettings;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ConfigRealmMapProvider()
        {
        }

        /// <summary>
        /// Establishes a session with the realm map provider.
        /// </summary>
        /// <param name="engineSettings">The associated authentication engine's settings.</param>
        /// <param name="key">
        /// This is simply the name of the configuration array that holds the
        /// static realm mappings.
        /// </param>
        /// <remarks>
        /// <para>
        /// This provider simply loads a static list of provider mappings from the
        /// application's configuration settings.  The mappings are loaded from
        /// the configuration key array specified by the <b>key</b> parameter.
        /// Each element in the array specifies a single realm mapping.  The format
        /// of each is mapping is:
        /// </para>
        /// <code language="none">
        ///     &lt;realm&gt;$$&lt;extension typeref&gt;$$&lt;args&gt;$$&lt;query&gt;
        /// </code>
        /// <para>
        /// where <b>realm</b> identifies the authentication realm, <b>extension typeref</b> 
        /// specifies the type implementing <see cref="IAuthenticationExtension" /> formatted as
        /// specified for <see cref="Config.Parse(string,System.Type)" />, <b>key</b>
        /// are the provider arguments and <b>query</b> is the optional provider query
        /// string.  Note the use of <b>$$</b> as field separators.  Here's an example
        /// configuration:
        /// </para>
        /// <code language="none">
        /// realm-map[0] = lilltek.com$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$path=c:\lilltek.txt$$
        /// realm-map[1] = test.com$$LillTek.Datacenter..Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$path=c:\test.txt$$
        /// </code>
        /// <note>
        /// Every call to <see cref="Open" /> should be matched by a call to
        /// <see cref="Close" /> or <see cref="Dispose" />.
        /// </note>
        /// </remarks>
        /// <exception cref="AuthenticationException">Thrown if there's an error loading the map.</exception>
        public void Open(AuthenticationEngineSettings engineSettings, string key)
        {
            string[] configMap;

            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Provider is already open.");

                this.engineSettings = engineSettings;

                configMap = Config.Global.GetArray(key);
                if (configMap == null)
                    throw new AuthenticationException("Configuration key [{0}] not found.", key);

                realmMap = new List<RealmMapping>();
                foreach (string map in configMap)
                {
                    string[]        fields;
                    string          realm;
                    System.Type     providerType;
                    string          args;
                    string          query;

                    fields = map.Split(new string[] { "$$" }, StringSplitOptions.None);
                    if (fields.Length != 4)
                        throw new AuthenticationException("Four realm map fields expected: [{0}]", map);

                    realm = fields[0].Trim();
                    args  = fields[2].Trim();
                    query = fields[3].Trim();

                    if (realm.Length == 0)
                        throw new AuthenticationException("<realm> field cannot be empty.");

                    providerType = Config.Parse(fields[1], (System.Type)null);
                    if (providerType == null)
                        throw new AuthenticationException("Unable to instantiate provider class: [{0}]", fields[1]);

                    for (int i = 0; i < realmMap.Count; i++)
                        if (String.Compare(realmMap[i].Realm, realm, true) == 0)
                            throw new AuthenticationException("Duplicate realm: {0}", realm);

                    realmMap.Add(new RealmMapping(engineSettings, realm, providerType, new ArgCollection(EnvironmentVars.Expand(args)), query));
                }
            }
        }

        /// <summary>
        /// Closes the session with the realm map provider.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                realmMap = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the provider is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return realmMap != null; }
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
            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new AuthenticationException("Provider is not open.");

                return realmMap;
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
