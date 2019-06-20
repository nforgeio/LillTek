//-----------------------------------------------------------------------------
// FILE:        EnhancedDns.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements enhanced DNS functionality.

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): 
//
// Implement my own native lookups using the DnsResolver class
// to provide for other query types (MX, SRV,...) as well
// as to honor the TTLs returned.

// $todo(jeff.lill): Look into caching NAKs.

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Implements a System.Net.Dns compatible class that caches response as
    /// well as participates with the <see cref="AsyncTracker" /> class to provide 
    /// for tracking  async operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The .NET framework Dns class doesn't appear to cache the
    /// records it retrieves.  This implementation does cache the
    /// entries for significantly improved performance.  This implementation
    /// builds on the framework DNS class.  The problem with this 
    /// underlying class is that it does not return the TTL in the records 
    /// it returns.  The workaround is to cache response for a fixed
    /// time.  This is controlled by the <see cref="EnhancedDns.TTL" /> property.
    /// This is initialized to 15 minutes but can be changed during
    /// application intialization if necessary.
    /// </para>
    /// <para>
    /// The cache will be flushed automatically every 5 minutes.
    /// It can also be flushed explicitly via the Flush() method.
    /// </para>
    /// <para>
    /// The <see cref="AddHost" /> and <see cref="RemoveHosts" />
    /// methods are provided for unit testing.  These methods provide
    /// a mechanism for modifying the machine's HOSTS file.  <see cref="AddHost" />
    /// works by appending a special comment string to the HOSTS file (if it isn't
    /// already present and then writing the new HOST entry.  <see cref="RemoveHosts" />
    /// works by removing everything after the special comment string in
    /// the HOSTS file.  Both methods flush this class's DNS cache.
    /// </para>
    /// <note>
    /// Virus and Spyware detection software may need to be disabled for 
    /// this functionality to work properly.
    /// </note>
    /// </remarks>
    public static class EnhancedDns
    {
        /// <summary>
        /// Defines what we cache.
        /// </summary>
        private sealed class DnsRecord
        {
            /// <summary>
            /// The host name (uppercased).
            /// </summary>
            public string Host;

            /// <summary>
            /// The cached entry.
            /// </summary>
            public IPHostEntry Entry;

            /// <summary>
            /// Expiration time (SYS).
            /// </summary>
            public DateTime TTD;

            /// <summary>
            /// Constructs a DNS record.
            /// </summary>
            /// <param name="host">The host name (uppercased).</param>
            /// <param name="entry">The cached entry.</param>
            /// <param name="TTD">Expiration time (SYS).</param>
            public DnsRecord(string host, IPHostEntry entry, DateTime TTD)
            {
                this.Host  = host;
                this.Entry = entry;
                this.TTD   = TTD;
            }
        }

        /// <summary>
        /// Used to track async DNS operations.
        /// </summary>
        public sealed class EnhancedDnsResult : AsyncResult
        {
            /// <summary>
            /// The host name (uppercased).
            /// </summary>
            public string Host;

            /// <summary>
            /// The entry found.
            /// </summary>
            public IPHostEntry HostEntry;

            /// <summary>
            /// <c>true</c> if entry came from the cache.
            /// </summary>
            public bool Cached = false;

            /// <summary>
            /// Constructs an IAsyncResult instances to be used to track
            /// a DNS operation.
            /// </summary>
            /// <param name="owner">The object that "owns" the operation (or <c>null</c>).</param>
            /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
            /// <param name="state">Application defined state.</param>
            public EnhancedDnsResult(object owner, AsyncCallback callback, object state)
                : base(owner, callback, state)
            {

            }
        }

        private static object           syncLock = new object();
        private static Hashtable        cache;              // Cache of DnsRecords key by host name
        private static bool             enableCache;        // True to enabe caching
        private static TimeSpan         ttl;                // TTL for all retrieved entries
        private static GatedTimer       purgeTimer;         // Cache purging timer
        private static AsyncCallback    onGetHostByName;    // Async callbacks

        /// <summary>
        /// Initializes the cache.
        /// </summary>
        static EnhancedDns()
        {
            cache           = new Hashtable();
            enableCache     = true;
            ttl             = TimeSpan.FromMinutes(15);
            purgeTimer      = new GatedTimer(new TimerCallback(OnFlush), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            onGetHostByName = new AsyncCallback(OnGetHostByName);
        }

        /// <summary>
        /// Controls whether DNS responses are cached.
        /// </summary>
        /// <remarks>
        /// This defaults to true.  Note also that setting this to false will
        /// also clear the cache.
        /// </remarks>
        public static bool EnableCaching
        {
            get { return enableCache; }

            set
            {
                enableCache = value;
                Clear();
            }
        }

        /// <summary>
        /// The duration that retrieved entries will remain in the cache.
        /// </summary>
        /// <remarks>
        /// This defaults to 15 minutes.
        /// </remarks>
        public static TimeSpan TTL
        {
            get { return ttl; }
            set { ttl = value; }
        }

        /// <summary>
        /// Flushes extired entries from the cache.
        /// </summary>
        public static void Flush()
        {
            var delList = new ArrayList();
            var now     = SysTime.Now;

            lock (syncLock)
            {
                foreach (DnsRecord r in cache.Values)
                    if (r.TTD >= now)
                        delList.Add(r.Host);

                for (int i = 0; i < delList.Count; i++)
                    cache.Remove(delList[i]);
            }
        }

        /// <summary>
        /// Handles the purge timer callbacks.
        /// </summary>
        /// <param name="state">Not used.</param>
        private static void OnFlush(object state)
        {
            Flush();
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public static void Clear()
        {
            lock (syncLock)
                cache.Clear();
        }

        /// <summary>
        /// Resets the DNS state back to the default condition.
        /// </summary>
        public static void Reset()
        {
            lock (syncLock)
            {
                enableCache = true;
                ttl         = TimeSpan.FromMinutes(15);

                Flush();
            }
        }

        //---------------------------------------------------------------------
        // Implementation Note:
        //
        // I'm going to perform the cache lookup in BeginGetHostByName() and
        // if I find the entry and it hasn't expired, I'll set the HostEntry
        // field in the async result as well as setting Cached=true. EndGetHostByName() 
        // will be able to distinguish between a cached and non-cached situations
        // by looking at the Cached field.

        /// <summary>
        /// Initiates an asynchronous operation to perform an host name lookup.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="callback">The operation completion callback (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The asynchronous result used to track the operation.</returns>
        public static IAsyncResult BeginGetHostByName(string host, AsyncCallback callback, object state)
        {
            var         dnsAR = new EnhancedDnsResult("SafeDns", callback, state);
            DnsRecord   dnsRec = null;

            dnsAR.Host = host.ToUpper();    // Doing this once

            if (enableCache)
            {
                lock (syncLock)
                {
                    dnsRec = (DnsRecord)cache[dnsAR.Host];
                    if (dnsRec != null)
                    {
                        if (dnsRec.TTD <= SysTime.Now)
                        {
                            // Remove expired entries

                            cache.Remove(dnsRec.Host);
                            dnsRec = null;
                        }
                    }
                }
            }

            if (dnsRec == null)
            {
                Dns.BeginGetHostEntry(host, onGetHostByName, dnsAR);
                dnsAR.Started();
            }
            else
            {
                dnsAR.HostEntry = dnsRec.Entry;
                dnsAR.Cached    = true;

                dnsAR.Started();
                dnsAR.Notify();     // Indicate completion
            }

            return dnsAR;
        }

        /// <summary>
        /// Handles async host lookup completions.
        /// </summary>
        /// <param name="ar"></param>
        private static void OnGetHostByName(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onGetHostByName);
            if (ar == null)
                return;

            // Handle the completion

            EnhancedDnsResult dnsAR = (EnhancedDnsResult)ar.AsyncState;

            try
            {
                dnsAR.HostEntry = Dns.EndGetHostEntry(ar);
                dnsAR.Notify();
            }
            catch (Exception e)
            {
                dnsAR.Notify(e);
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginGetHostByName" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginGetHostByName" />.</param>
        /// <returns>The host's DNS inforinformation.</returns>
        public static IPHostEntry EndGetHostByName(IAsyncResult ar)
        {
            var dnsAR = (EnhancedDnsResult)ar;

            dnsAR.Wait();
            try
            {
                if (dnsAR.Exception != null)
                    throw dnsAR.Exception;

                // If the entry returned wasn't from the cache then add
                // it with the current TTL.

                if (enableCache && !dnsAR.Cached)
                {
                    DnsRecord dnsRec = new DnsRecord(dnsAR.Host, dnsAR.HostEntry, SysTime.Now + ttl);

                    lock (syncLock)
                        cache[dnsAR.Host] = dnsRec;
                }

                return dnsAR.HostEntry;
            }
            finally
            {
                dnsAR.Dispose();
            }
        }

        /// <summary>
        /// Performs a reverse lookup of an IP address.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <returns>The host information.</returns>
        /// <remarks>
        /// <note>
        /// The results of this operation are not cached.
        /// </note>
        /// </remarks>
        public static IPHostEntry GetHostByAddress(IPAddress address)
        {
            return Dns.GetHostEntry(address);
        }

        /// <summary>
        /// Performs a reverse lookup of an IP address.
        /// </summary>
        /// <param name="address">The IP address in dotted quad notation.</param>
        /// <returns>The host information.</returns>
        /// <remarks>
        /// <note>
        /// The results of this operation are not cached.
        /// </note>
        /// </remarks>
        public static IPHostEntry GetHostByAddress(string address)
        {
            return Dns.GetHostEntry(address);
        }

        /// <summary>
        /// Returns the DNS information for a host.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <returns>The host information.</returns>
        public static IPHostEntry GetHostByName(string host)
        {
            var ar = BeginGetHostByName(host, null, null);

            return EndGetHostByName(ar);
        }

        /// <summary>
        /// Returns the host name of the local computer.
        /// </summary>
        /// <returns></returns>
        public static string GetHostName()
        {
            return Dns.GetHostName();
        }

        private const string HostsPath = @"\drivers\etc\hosts";
        private const string Marker = "\r\n# LillTek Unit Testing Hosts. Do not add anything after this comment.\r\n";

        /// <summary>
        /// Appends a test host entry to the local HOSTS file.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="address">The IP address.</param>
        /// <remarks>
        /// <note>
        /// Virus and Spyware detection software may need to
        /// be disabled for this method to work properly.
        /// </note>
        /// </remarks>
        public static void AddHost(string host, IPAddress address)
        {
            lock (syncLock)
            {
                FileStream      fs = new FileStream(Environment.GetFolderPath(Environment.SpecialFolder.System) + HostsPath,
                                                    FileMode.OpenOrCreate, FileAccess.ReadWrite);
                string          contents;
                byte[]          raw;

                try
                {
                    if (fs.Length > 128 * 1024)
                        throw new InvalidOperationException("HOSTS file exceeds 128K");

                    raw = new byte[(int)fs.Length];
                    fs.Read(raw, 0, raw.Length);
                    contents = Helper.FromAnsi(raw);

                    if (contents.IndexOf(Marker) == -1)
                    {
                        raw = Helper.ToAnsi(Marker);
                        fs.Write(raw, 0, raw.Length);
                    }

                    raw = Helper.ToAnsi(string.Format("\r\n{0}\t{1}\r\n", address, host));
                    fs.Write(raw, 0, raw.Length);
                }
                finally
                {
                    fs.Close();
                }

                Flush();
            }
        }

        /// <summary>
        /// Removes all test host entries from the local HOSTs file.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Virus and Spyware detection software may need to
        /// be disabled for this method to work properly.
        /// </note>
        /// </remarks>
        public static void RemoveHosts()
        {
            lock (syncLock)
            {
                FileStream      fs = new FileStream(Environment.GetFolderPath(Environment.SpecialFolder.System) + HostsPath,
                                                    FileMode.OpenOrCreate, FileAccess.ReadWrite);
                string          contents;
                byte[]          raw;
                int             markerPos;

                try
                {
                    Clear();    // This makes sure that any cached entries for the for
                    // the hosts being removed are also deleted.

                    if (fs.Length > 128 * 1024)
                        throw new InvalidOperationException("HOSTS file exceeds 128K");

                    raw = new byte[(int)fs.Length];
                    fs.Read(raw, 0, raw.Length);
                    contents = Helper.FromAnsi(raw);

                    markerPos = contents.IndexOf(Marker);
                    if (markerPos == -1)
                        return;

                    fs.SetLength(0);
                    fs.Write(raw, 0, markerPos);
                }
                finally
                {
                    fs.Close();
                }

                Flush();
            }
        }
    }
}
