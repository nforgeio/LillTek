//-----------------------------------------------------------------------------
// FILE:        FileRealmMapProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements IRealmMapProvider by loading authentication realm 
//              mappings from the application configuration.

using System;
using System.Collections.Generic;
using System.IO;

using LillTek.Common;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements <see cref="IRealmMapProvider" /> by loading authentication realm mappings 
    /// from a file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider dynamically loads list of provider mappings from an
    /// ANSI encoded text file.  The mappings are specified one per line in
    /// the file.  The format for each mapping is:
    /// </para>
    /// <code language="none">
    ///     &lt;realm&gt;$$&lt;extension typeref&gt;$$&lt;args&gt;$$&lt;query&gt;
    /// </code>
    /// <para>
    /// where <b>realm</b> identifies the authentication realm, <b>extension typeref</b> 
    /// specifies the type implementing <see cref="IAuthenticationExtension" /> formatted as
    /// specified for <see cref="Config.Parse(string,System.Type)" />, <b>args</b>
    /// string.  Note the used of <b>$$</b> as field separators.
    /// </para>
    /// <para>
    /// Empty lines and comment lines beginning with "//" are ignored.  Note that
    /// environment variables embedded in the realm mappings will be expanded before
    /// the mapping is processed.  Environment variables are formatted as <b>$(variable)</b>.
    /// See <see cref="EnvironmentVars" /> for a list of the built-in environment
    /// variables.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class FileRealmMapProvider : IRealmMapProvider, ILockable
    {
        private string                          path = null;
        private AuthenticationEngineSettings    engineSettings;

        /// <summary>
        /// Constructor.
        /// </summary>
        public FileRealmMapProvider()
        {
        }

        /// <summary>
        /// Establishes a session with the realm map provider.
        /// </summary>
        /// <param name="engineSettings">The associated authentication engine's settings.</param>
        /// <param name="path">
        /// The fully qualified path to the ANSI encoded mapping file.
        /// </param>
        /// <remarks>
        /// <para>
        /// This provider simply loads a static list of provider mappings from an
        /// ANSI encoded text file.  The mappings are specified one per line in
        /// the file.  The format for each mapping is:
        /// </para>
        /// <code language="none">
        ///     &lt;realm&gt;$$&lt;extension typeref&gt;$$&lt;args&gt;$$&lt;query&gt;
        /// </code>
        /// <para>
        /// where <b>realm</b> identifies the authentication realm, <b>extension typeref</b> 
        /// specifies the type implementing <see cref="IAuthenticationExtension" /> formatted as
        /// specified for <see cref="Config.Parse(string,System.Type)" />, <b>args</b>
        /// are the provider arguments and <b>query</b> is the optional provider query
        /// string.  Note the use of <b>$$</b> as field separators.
        /// </para>
        /// <para>
        /// Empty lines and comment lines beginning with "//" are ignored.  Note that
        /// environment variables embedded in the realm mappings will be expanded before
        /// the mapping is processed.  Environment variables are formatted as <b>$(variable)</b>.
        /// See <see cref="EnvironmentVars" /> for a list of the build-in environment
        /// variables.
        /// </para>
        /// <code language="none">
        /// // This is a comment line
        /// 
        /// lilltek.com$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$path=c:\lilltek.txt$$
        /// test.com$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$path=c:\test.txt$$
        /// </code>
        /// <note>
        /// Every call to <see cref="Open" /> should be matched by a call to
        /// <see cref="Close" /> or <see cref="Dispose" />.
        /// </note>
        /// </remarks>
        /// <exception cref="AuthenticationException">Thrown if there's an error loading the map.</exception>
        public void Open(AuthenticationEngineSettings engineSettings, string path)
        {
            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Provider is already open.");

                this.path           = path;
                this.engineSettings = engineSettings;
            }
        }

        /// <summary>
        /// Closes the session with the realm map provider.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                path = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the provider is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return path != null; }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Attempts to load the realm map from the text file.
        /// </summary>
        /// <returns>The new realm map.</returns>
        private List<RealmMapping> LoadMap()
        {
            StreamReader        reader;
            List<RealmMapping>  realmMap;
            int                 lineNum;

            reader = new StreamReader(path, Helper.AnsiEncoding);

            try
            {
                lineNum  = 0;
                realmMap = new List<RealmMapping>();

                while (true)
                {
                    string          map;
                    string[]        fields;
                    string          realm;
                    System.Type     providerType;
                    string          args;
                    string          query;

                    map = reader.ReadLine();
                    if (map == null)
                        break;

                    lineNum++;

                    map = map.Trim();
                    if (map.Length == 0 || map.StartsWith("//"))
                        continue;

                    map = EnvironmentVars.Expand(map);

                    fields = map.Split(new string[] { "$$" }, StringSplitOptions.None);
                    if (fields.Length != 4)
                        throw new AuthenticationException("{0}({1}): Four realm map fields expected: [{2}]", Path.GetFileName(path), lineNum, map);

                    realm = fields[0].Trim();
                    args  = fields[2].Trim();
                    query = fields[3].Trim();

                    providerType = Config.Parse(fields[1], (System.Type)null);
                    if (providerType == null)
                        throw new AuthenticationException("{0}({1}): Unable to instantiate provider class: [{2}]", Path.GetFileName(path), lineNum, fields[1]);

                    for (int i = 0; i < realmMap.Count; i++)
                        if (String.Compare(realmMap[i].Realm, realm, true) == 0)
                            throw new AuthenticationException("{0}({1}): Duplicate realm: {2}", Path.GetFileName(path), realm);

                    realmMap.Add(new RealmMapping(engineSettings, realm, providerType, new ArgCollection(args), query));
                }
            }
            finally
            {
                reader.Close();
            }

            return realmMap;
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

                return LoadMap();
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
