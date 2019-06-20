//-----------------------------------------------------------------------------
// FILE:        SysLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Global system event log class.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace LillTek.Common
{
#if MOBILE_DEVICE
    /// <summary>
    /// Implements a global system event logging interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the static <see cref="LogProvider" /> property to associate a custom 
    /// <see cref="ISysLogProvider" /> implementation with this class.  By default, 
    /// DEBUG builds will associate a <b>DebugSysLogProvider</b> instance while 
    /// RELEASE builds will associate a NULL provider.
    /// </para>
    /// <para>
    /// The following methods are available for submitting information to the
    /// event log:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="LogInformation(string,object[])" /></term>
    ///         <description>Logs an informational entry.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogWarning(string,object[])" /></term>
    ///         <description>Logs a warning.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogError(string,object[])" /></term>
    ///         <description>Logs an error.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogErrorStackDump(string,object[])" /></term>
    ///         <description>Logs an error, including a dump of the current stack.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogException(Exception)" /></term>
    ///         <description>Logs an exception.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogException(Exception,string,object[])" /></term>
    ///         <description>Logs an exception with additional information.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogSecuritySuccess(string,object[])" /></term>
    ///         <description>Logs a successful security related change or access attempt.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogSecurityFailure(string,object[])" /></term>
    ///         <description>Logs a failed security related change or access.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Trace(string,string,object[])" /></term>
    ///         <description>Logs debugging related information.</description>
    ///     </item>
    /// </list>
    /// </remarks>
#else
    /// <summary>
    /// Implements a global system event logging interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the static <see cref="LogProvider" /> property to associate a custom 
    /// <see cref="ISysLogProvider" /> implementation with this class.  By default, 
    /// DEBUG builds will associate a <b>DebugSysLogProvider</b> instance while 
    /// RELEASE builds will associate a NULL provider.
    /// </para>
    /// <para>
    /// The following methods are available for submitting information to the
    /// event log:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="LogInformation(string,object[])" /></term>
    ///         <description>Logs an informational entry.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogWarning(string,object[])" /></term>
    ///         <description>Logs a warning.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogError(string,object[])" /></term>
    ///         <description>Logs an error.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogErrorStackDump(string,object[])" /></term>
    ///         <description>Logs an error, including a dump of the current stack.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogException(Exception)" /></term>
    ///         <description>Logs an exception.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogException(Exception,string,object[])" /></term>
    ///         <description>Logs an exception with additional information.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogSecuritySuccess(string,object[])" /></term>
    ///         <description>Logs a successful security related change or access attempt.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LogSecurityFailure(string,object[])" /></term>
    ///         <description>Logs a failed security related change or access.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Trace(string,SysLogLevel,string,object[])" /></term>
    ///         <description>Logs debugging related information.</description>
    ///     </item>
    /// </list>
    /// <para>
    /// <see cref="SysLog" /> provides a built-in mechanism for throttling the
    /// submission of duplicate entries to the log.  The class maintains an
    /// internal table of recent log entries and checks to see if the an entry
    /// has already been logged before submitting it to the event log.  The
    /// <see cref="CacheTime" /> property specifies how long an entry should
    /// be cached.  This defaults to <b>1 hour</b>.  Set <see cref="CacheTime" /> 
    /// to <see cref="TimeSpan.Zero" /> to disable this behavior such that all entries 
    /// will be logged immediately.
    /// </para>
    /// <para>
    /// Log entries are cached based on their type and the following property
    /// global settings: <see cref="CacheInformation" /> (<c>false</c>),
    /// <see cref="CacheWarnings" /> (default <c>true</c>), <see cref="CacheErrors" /> (default <c>true</c>), 
    /// <see cref="CacheExceptions" /> (default <c>true</c>), <see cref="CacheSecurity" /> (default <c>false</c>), 
    /// and <see cref="CacheDebug" /> (default <c>false</c>).
    /// </para>
    /// <note>
    /// <see cref="CacheTime" /> is loaded automatically from the <b>Diagnostics.SysLog.CacheTime</b>
    /// configuration setting the first time the <see cref="SysLog" /> is accessed.  The 
    /// individual event type cache settings are also loaded from the configuration settings as
    /// <b>Diagnostics.SysLog.CacheInformation</b>, <b>Diagnostics.SysLog.CacheWarnings</b>, etc.
    /// </note>
    /// <note>
    /// Calls to <see cref="Trace(string,SysLogLevel,string,object[])" /> are not throttled by this mechanism.
    /// </note>
    /// </remarks>
#endif
    public sealed class SysLog
    {
        //---------------------------------------------------------------------
        // Private classes

#if !MOBILE_DEVICE

        /// <summary>
        /// Duplicated from LillTek.Cryptography assembly.  Implements the MD5 
        /// algorithm to hash data into a digest.
        /// </summary>
        private static class MD5Hasher
        {
            /// <summary>
            /// Size of a MD5 hashed digest in bytes.
            /// </summary>
            public const int DigestSize = 16;

            // The inner and outer XOR constants defined in RFC2104.

            private const int iPad = 0x36;
            private const int oPad = 0x5C;

            /// <summary>
            /// Computes the MD5 hash of the data buffer passed and then folds it
            /// into a 64-bit number via XOR.
            /// </summary>
            /// <param name="data">The data to be hashed.</param>
            /// <returns>The single folded hash of the data.</returns>
            /// <remarks>
            /// <para>
            /// A normal MD5 hash returns 16 bytes of data.  This method folds
            /// these 8 bytes into 8 bytes by taking the first 4 bytes of data
            /// and XORing these with the last 8 bytes and then converting
            /// the result into a 64-bit integer.
            /// </para>
            /// </remarks>
            public static long FoldOnce(byte[] data)
            {
                byte[]  hash;
                long    result;

                hash = Compute(data);
                result = 0;
                result |= (long)(byte)(hash[0] ^ hash[08]) << 56;
                result |= (long)(byte)(hash[1] ^ hash[09]) << 48;
                result |= (long)(byte)(hash[2] ^ hash[10]) << 40;
                result |= (long)(byte)(hash[3] ^ hash[11]) << 32;
                result |= (long)(byte)(hash[4] ^ hash[12]) << 24;
                result |= (long)(byte)(hash[5] ^ hash[13]) << 16;
                result |= (long)(byte)(hash[6] ^ hash[14]) << 8;
                result |= (long)(byte)(hash[7] ^ hash[15]);

                return result;
            }

            /// <summary>
            /// Computes the MD5 hash of the data buffer passed and then double folds it
            /// into a 32-bit number via XOR.
            /// </summary>
            /// <param name="data">The data to be hashed.</param>
            /// <returns>The double folded hash of the data.</returns>
            /// <remarks>
            /// A normal MD5 hash returns 16 bytes of data.  This method folds
            /// these 8 bytes into 8 bytes by taking the first 4 bytes of data
            /// and XORing these with the last 8 bytes and folding the result
            /// again, producing a 32-bit integer.
            /// </remarks>
            public static int FoldTwice(byte[] data)
            {
                long v;

                v = FoldOnce(data);
                return (int)(v >> 32) ^ (int)v;
            }

            /// <summary>
            /// Folds the 64-bit integer passed into a 32-bit integer.
            /// </summary>
            /// <param name="v">The 64-bit value to be folded.</param>
            /// <returns>The 32-bit result.</returns>
            public static int Fold(long v)
            {
                return (int)(v >> 32) ^ (int)v;
            }

            /// <summary>
            /// Hashes data from a buffer and returns the result.
            /// </summary>
            /// <param name="data">The input buffer.</param>
            /// <returns>The hashed digest.</returns>
            public static byte[] Compute(byte[] data)
            {
                return new MD5CryptoServiceProvider().ComputeHash(data, 0, data.Length);
            }

            /// <summary>
            /// Computes the MD5 hash of a string encoded as UTF-8.
            /// </summary>
            /// <param name="data">The string to be hashed.</param>
            /// <returns>The hashed digest.</returns>
            public static byte[] Compute(string data)
            {
                return Compute(Helper.ToUTF8(data));
            }

            /// <summary>
            /// Uses the HMAC/MD5 algorithm to hash data from a buffer and returns the result.
            /// </summary>
            /// <param name="key">The secret key.</param>
            /// <param name="data">The input buffer.</param>
            /// <param name="pos">Index of the first byte to be hashed.</param>
            /// <param name="length">The number of bytes to hash.</param>
            /// <returns>The hashed digest.</returns>
            public static byte[] Compute(byte[] key, byte[] data, int pos, int length)
            {
                byte[]              xorBuf = new byte[64];
                byte[]              hash;
                EnhancedBlockStream es;

                // If the key length is greater than 64 bytes then hash the key
                // first to get it down to a reasonable size.

                if (key.Length > 64)
                    key = Compute(key);

                // Pad the key with zeros to 64 bytes in length.

                if (key.Length < 64)
                {
                    byte[] newKey = new byte[64];

                    for (int i = 0; i < newKey.Length; i++)
                    {
                        if (i < key.Length)
                            newKey[i] = key[i];
                        else
                            newKey[i] = 0;
                    }

                    key = newKey;
                }

                // XOR the key with iPad and put the result in xorBuf

                for (int i = 0; i < 64; i++)
                    xorBuf[i] = (byte)(key[i] ^ iPad);

                // Hash the result of the data appended to xorBuf

                es = new EnhancedBlockStream(new Block(xorBuf), new Block(data, pos, length));
                hash = Compute(es, xorBuf.Length + length);

                // XOR the key with oPad and put the result in xorBuf

                for (int i = 0; i < 64; i++)
                    xorBuf[i] = (byte)(key[i] ^ oPad);

                // The result is the hash of the combination of hash appended to xorBuf

                es = new EnhancedBlockStream(new Block(xorBuf), new Block(hash));

                return Compute(es, xorBuf.Length + hash.Length);
            }

            /// <summary>
            /// Uses the HMAC/MD5 algorithm to hash data from a buffer and returns the result.
            /// </summary>
            /// <param name="key">The secret key.</param>
            /// <param name="data">The input buffer.</param>
            /// <returns>The hashed digest.</returns>
            public static byte[] Compute(byte[] key, byte[] data)
            {
                return Compute(key, data, 0, data.Length);
            }

            /// <summary>
            /// Hashes data from a buffer and returns the result.
            /// </summary>
            /// <param name="data">The input buffer.</param>
            /// <param name="pos">Index of the first byte to be hashed.</param>
            /// <param name="length">The number of bytes to hash.</param>
            /// <returns>The hashed digest.</returns>
            public static byte[] Compute(byte[] data, int pos, int length)
            {
                return new MD5CryptoServiceProvider().ComputeHash(data, pos, length);
            }

            /// <summary>
            /// Hashes data from a stream.
            /// </summary>
            /// <param name="es">The input stream.</param>
            /// <param name="length">The number of bytes to hash.</param>
            /// <returns>The hashed digest.</returns>
            /// <remarks>
            /// The method will hash length bytes of the stream from the current position
            /// and the stream position will be restored before the method
            /// returns.
            /// </remarks>
            public static byte[] Compute(EnhancedStream es, long length)
            {
                MD5CryptoServiceProvider    md5 = new MD5CryptoServiceProvider();
                long                        streamPos;
                byte[]                      buf;
                int                         cb;

                streamPos = es.Position;
                buf = new byte[8192];

                while (length > 0)
                {
                    cb = (int)(length > buf.Length ? buf.Length : length);
                    if (es.Read(buf, 0, cb) < cb)
                        throw new InvalidOperationException("Read past end of stream.");

                    md5.TransformBlock(buf, 0, cb, buf, 0);
                    length -= cb;
                }

                md5.TransformFinalBlock(buf, 0, 0);
                es.Seek(streamPos, SeekOrigin.Begin);

                return md5.Hash;
            }

            /// <summary>
            /// Uses the HMAC/MD5 algorithm to hash data from a stream.
            /// </summary>
            /// <param name="key">The secret key.</param>
            /// <param name="es">The input stream.</param>
            /// <param name="length">The number of bytes to hash.</param>
            /// <returns>The hashed digest.</returns>
            /// <remarks>
            /// The method will hash length bytes of the stream from the current position.
            /// and the stream position will be restored before the method
            /// returns.
            /// </remarks>
            public static byte[] Compute(byte[] key, EnhancedStream es, int length)
            {
                byte[]      hash;
                long        pos;

                pos  = es.Position;
                hash = Compute(key, es.ReadBytes(length));
                es.Seek(pos, SeekOrigin.Begin);

                return hash;
            }
        }

        /// <summary>
        /// Holds an MD5 hash code.
        /// </summary>
        private struct MD5Key
        {
            private byte[] hash;

            public MD5Key(byte[] hash)
            {
                if (hash.Length != MD5Hasher.DigestSize)
                    throw new ArgumentException("Bad MD5 hash.");

                this.hash = hash;
            }

            public override int GetHashCode()
            {
                int     h   = 0;
                int     pos = 0;

                for (int i = 0; i < MD5Hasher.DigestSize / 4; i++)
                    h ^= Helper.ReadInt32(hash, ref pos);

                return h;
            }

            public override bool Equals(object obj)
            {
                MD5Key comp;

                if (!(obj is MD5Key))
                    return false;

                comp = (MD5Key)obj;
                for (int i = 0; i < hash.Length; i++)
                    if (hash[i] != comp.hash[i])
                        return false;

                return true;
            }
        }

        /// <summary>
        /// Holds information about a cached log entry.
        /// </summary>
        private sealed class CachedEntry
        {
            /// <summary>
            /// Indicates the log entry type.
            /// </summary>
            public SysLogEntryType EntryType;

            /// <summary>
            /// Extended log information.
            /// </summary>
            public ISysLogEntryExtension Extension;

            /// <summary>
            /// The log message (or <c>null</c>).
            /// </summary>
            public string Message;

            /// <summary>
            /// The exception (or <c>null</c>).
            /// </summary>
            public Exception Exception;

            /// <summary>
            /// Time the entry was first logged (UTC).
            /// </summary>
            public DateTime FirstTime;

            /// <summary>
            /// Time the last entry was logged (UTC).
            /// </summary>
            public DateTime LastTime;

            /// <summary>
            /// Scheduled time for the cached entry to be purged (SYS).
            /// </summary>
            public DateTime TTD;

            /// <summary>
            /// The number of times the entry was logged.
            /// </summary>
            public int Count;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
            /// <param name="entryType">Indicates the log entry type.</param>
            /// <param name="message">The log message (or <c>null</c>).</param>
            /// <param name="e">The exception (or <c>null</c>).</param>
            public CachedEntry(ISysLogEntryExtension extension, SysLogEntryType entryType, string message, Exception e)
            {
                this.EntryType = entryType;
                this.Extension = extension;
                this.Message   = message;
                this.Exception = e;
                this.FirstTime =
                this.LastTime  = DateTime.UtcNow;
                this.TTD       = SysTime.Now + cacheTime;
                this.Count     = 1;
            }
        }

#endif // !MOBILE_DEVICE

        //---------------------------------------------------------------------
        // Implementation
        //
        // The log entry cache is implemented with a hash table of CachedEntry
        // instances, keyed by the MD5 hash of the log entry (created by the
        // ComputeMD5Hash() method).  Each CachedEntry holds the information necessary
        // to regenerate the log entry, the first and last times the entry was logged,
        // the number of times the entry was logged as well as the time to purge
        // the cached entry.
        //
        // If throttling is enabled (CacheTime > 0) then here's the algorithm
        // used for logging every event except for Trace():
        //
        //      1. Application submits the event.
        //
        //      2. If throttling is disabled, the event is logged immediately
        //         and the steps below are skipped
        //
        //      3. ComputeMD5Hash() is called to get the hash for the entry
        //
        //      4. The cache is searched for the entry.  If no entry is
        //         present, then the event is logged and an entry is added.
        //
        //      5. If an entry is present, then its count will be incremented
        //         and the last log time will be set to now (no entry is
        //         logged).
        //
        // The cache is checked every minute on a background thread for entries
        // that need to be flushed.  This is determined by looking at the entry's
        // TTD field.
        //
        // Just before purging a cached entry, its log count field is examined.
        // If this is greater than one, a new entry is added to the log with the
        // same message, but adding the number of times it was logged as well
        // as the first and last log times.

#if DEBUG && !MOBILE_DEVICE
        private static ISysLogProvider  provider = new DebugSysLogProvider();
#else
        private static ISysLogProvider  provider = null;
#endif

#if !MOBILE_DEVICE

        private static TimeSpan                         cacheTime = TimeSpan.FromMinutes(60);
        private static Dictionary<MD5Key, CachedEntry>  cache = new Dictionary<MD5Key, CachedEntry>();
        private static AsyncCallback                    onTimer = new AsyncCallback(OnCacheTimer);

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SysLog()
        {

            var config = new Config("Diagnostics.SysLog");

            SysLog.CacheInformation = config.Get("CacheInformation", false);
            SysLog.CacheWarnings    = config.Get("CacheWarnings", true);
            SysLog.CacheErrors      = config.Get("CacheErrors", true);
            SysLog.CacheExceptions  = config.Get("CacheExceptions", true);
            SysLog.CacheSecurity    = config.Get("CacheSecurity", false);
            SysLog.CacheDebug       = config.Get("CacheDebug", false);

            cacheTime = config.Get("CacheTime", cacheTime);
            AsyncTimer.BeginTimer(TimeSpan.FromMinutes(1), onTimer, null);
        }

        /// <summary>
        /// Computes an MD5 hash code for the log entry
        /// </summary>
        /// <param name="extension">The extended entry information (or <c>null</c>).</param>
        /// <param name="entryType">The log entry type.</param>
        /// <param name="message">The log message (or <c>null</c>).</param>
        /// <param name="e">The logged exception (or <c>null</c>).</param>
        /// <returns>The computed hash.</returns>
        private static MD5Key ComputeMD5Hash(ISysLogEntryExtension extension, SysLogEntryType entryType, string message, Exception e)
        {
            StringBuilder sb = new StringBuilder(1024);

            if (extension != null)
                sb.Append(extension.Format());

            sb.Append('\t');

            sb.Append(entryType.ToString());
            sb.Append('\t');

            if (message != null)
                sb.Append(message);

            sb.Append('\t');

            if (e != null)
            {
                sb.Append(e.GetType().FullName);
                sb.Append('\t');
                sb.Append(e.Message);
                sb.Append('\t');
                sb.Append(e.StackTrace.ToString());
            }

            return new MD5Key(MD5Hasher.Compute(sb.ToString()));
        }

        /// <summary>
        /// Handles the caching for a log entry.
        /// </summary>
        /// <param name="extension">Extended log entry information or (<c>null</c>).</param>
        /// <param name="entryType">The log entry type.</param>
        /// <param name="message">The log message (or <c>null</c>).</param>
        /// <param name="e">The logged exception (or <c>null</c>).</param>
        /// <returns><c>true</c> if the entry is in the cache and should not be logged.</returns>
        private static bool IsCached(ISysLogEntryExtension extension, SysLogEntryType entryType, string message, Exception e)
        {
            lock (cache)
            {
                if (cacheTime == TimeSpan.Zero)
                    return false;

                switch (entryType)
                {
                    case SysLogEntryType.Trace:

                        if (!SysLog.CacheDebug)
                            return false;

                        break;

                    case SysLogEntryType.Error:

                        if (!SysLog.CacheErrors)
                            return false;

                        break;

                    case SysLogEntryType.Exception:

                        if (!SysLog.CacheExceptions)
                            return false;

                        break;

                    case SysLogEntryType.Information:

                        if (!SysLog.CacheInformation)
                            return false;

                        break;

                    case SysLogEntryType.SecurityFailure:
                    case SysLogEntryType.SecuritySuccess:

                        if (!SysLog.CacheSecurity)
                            return false;

                        break;

                    case SysLogEntryType.Warning:

                        if (!SysLog.CacheWarnings)
                            return false;

                        break;

                    default:

                        Debug.WriteLine("Unexpected log entry type.");
                        break;
                }

                MD5Key key = ComputeMD5Hash(extension, entryType, message, e);
                CachedEntry entry;

                if (cache.TryGetValue(key, out entry))
                {
                    entry.Count++;
                    entry.LastTime = DateTime.UtcNow;
                    return true;
                }

                entry = new CachedEntry(extension, entryType, message, e);
                cache.Add(key, entry);
                return false;
            }
        }

        /// <summary>
        /// Logs the cached log entry if it indicates that it was throttled.
        /// </summary>
        /// <param name="entry">The cached entry.</param>
        private static void LogCachedEntry(CachedEntry entry)
        {
            if (entry.Count <= 1)
                return;

            StringBuilder sb = new StringBuilder(1024);
            string message;

            sb.AppendLine();
            sb.AppendLine("*********************");
            sb.AppendLine("** Throttled Event;");
            sb.AppendFormat("** First Time: {0} UTC\r\n", entry.FirstTime);
            sb.AppendFormat("** Last Time:  {0} UTC\r\n", entry.LastTime);
            sb.AppendFormat("** Count:      {0}\r\n", entry.Count);
            sb.AppendLine("*********************");

            if (entry.Message != null)
            {
                sb.Append("\r\nMessage:\r\n\r\n");
                sb.Append(entry.Message);
            }

            message = sb.ToString();
            switch (entry.EntryType)
            {
                case SysLogEntryType.Information: provider.LogInformation(entry.Extension, message); break;
                case SysLogEntryType.Warning: provider.LogWarning(entry.Extension, message); break;
                case SysLogEntryType.Error: provider.LogError(entry.Extension, message); break;
                case SysLogEntryType.SecuritySuccess: provider.LogSecuritySuccess(entry.Extension, message); break;
                case SysLogEntryType.SecurityFailure: provider.LogSecurityFailure(entry.Extension, message); break;
                case SysLogEntryType.Exception: provider.LogException(entry.Extension, entry.Exception, message); break;
                default: provider.LogWarning(entry.Extension, string.Format("** Internal Warning: Unexpected Log Entry Type [{0}]\r\n\r\n", entry.EntryType) + message); break;
            }
        }

        /// <summary>
        /// Handles the background cache maintenance.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" />.</param>
        private static void OnCacheTimer(IAsyncResult ar)
        {
            AsyncTimer.EndTimer(ar);

            try
            {
                lock (cache)
                {

                    // Handle expired cache entries

                    List<MD5Key>    delList = new List<MD5Key>();
                    DateTime        now     = SysTime.Now;

                    foreach (MD5Key key in cache.Keys)
                    {
                        var entry = cache[key];

                        if (entry.TTD <= now)
                        {

                            // Expired

                            delList.Add(key);
                            LogCachedEntry(entry);
                        }
                    }

                    foreach (MD5Key key in delList)
                        cache.Remove(key);
                }
            }
            finally
            {
                AsyncTimer.BeginTimer(TimeSpan.FromMinutes(1), onTimer, null);
            }
        }

        /// <summary>
        /// Specifies the duration which logged information will be cached to
        /// avoid flooding the event log.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="SysLog" /> caches logged entries for 15 minutes by default
        /// and does not log the same information during this time.  This property
        /// controls the cache interval.  Set this to <see cref="TimeSpan.Zero" />
        /// to disable caching.
        /// </para>
        /// </remarks>
        public static TimeSpan CacheTime
        {
            get
            {
                lock (cache)
                    return cacheTime;
            }

            set
            {
                lock (cache)
                {
                    if (value == TimeSpan.Zero)
                        cache.Clear();

                    cacheTime = value;
                }
            }
        }

        /// <summary>
        /// Indicates whether <see cref="SysLogEntryType.Information" /> log
        /// entries are cached if caching is enabled.  Defaults to <c>false</c>.
        /// </summary>
        public static bool CacheInformation { get; set; }

        /// <summary>
        /// Indicates whether <see cref="SysLogEntryType.Warning" /> log
        /// entries are cached if caching is enabled.  Defaults to <c>true</c>.
        /// </summary>
        public static bool CacheWarnings { get; set; }

        /// <summary>
        /// Indicates whether <see cref="SysLogEntryType.Error" /> log
        /// entries are cached if caching is enabled.  Defaults to <c>true</c>.
        /// </summary>
        public static bool CacheErrors { get; set; }

        /// <summary>
        /// Indicates whether <see cref="SysLogEntryType.Exception" /> log
        /// entries are cached if caching is enabled.  Defaults to <c>true</c>.
        /// </summary>
        public static bool CacheExceptions { get; set; }

        /// <summary>
        /// Indicates whether <see cref="SysLogEntryType.SecuritySuccess" />
        /// and <see cref="SysLogEntryType.SecurityFailure" /> log
        /// entries are cached if caching is enabled.  Defaults to <c>false</c>.
        /// </summary>
        public static bool CacheSecurity { get; set; }

        /// <summary>
        /// Indicates whether <see cref="SysLogEntryType.Trace" /> log
        /// entries are cached if caching is enabled.  Defaults to <c>false</c>.
        /// </summary>
        public static bool CacheDebug { get; set; }

#else

        /// <summary>
        /// Handles the caching for a log entry.
        /// </summary>
        /// <param name="extension">Extended log entry information or (<c>null</c>).</param>
        /// <param name="entryType">The log entry type.</param>
        /// <param name="message">The log message (or <c>null</c>).</param>
        /// <param name="e">The logged exception (or <c>null</c>).</param>
        /// <returns><c>true</c> if the entry is in the cache and should not be logged.</returns>
        private static bool IsCached(ISysLogEntryExtension extension, SysLogEntryType entryType, string message, Exception e) 
        {
            return false;   // Caching is not implemented for Silverlight
        }

#endif // !MOBILE_DEVICE

        /// <summary>
        /// Manages the installed log provider implementation.  This may be
        /// set to <c>null</c> to disable logging.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is not threadsafe.  Initialize your provider
        /// when your application starts and then leave this property alone.
        /// </note>
        /// </remarks>
        public static ISysLogProvider LogProvider
        {

            get { return provider; }
            set { provider = value; }
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// Applications should call this before they terminate.
        /// </summary>
        public static void Flush()
        {
            if (provider == null)
                return;

#if !MOBILE_DEVICE

            lock (cache)
            {
                foreach (CachedEntry entry in cache.Values)
                    LogCachedEntry(entry);

                cache.Clear();
            }
#endif

            provider.Flush();
        }

        /// <summary>
        /// Logs an informational entry.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogInformation(string format, params object[] args)
        {
            LogInformation(null, format, args);
        }

        /// <summary>
        /// Logs an informational entry with optional extended log information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogInformation(ISysLogEntryExtension extension, string format, params object[] args)
        {
            if (provider == null)
                return;

            string message = args.Length > 0 ? string.Format(format, args) : format;

            if (!IsCached(extension, SysLogEntryType.Information, message, null))
                provider.LogInformation(extension, message);
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogWarning(string format, params object[] args)
        {
            LogWarning(null, format, args);
        }

        /// <summary>
        /// Logs a warning with optional extended log information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogWarning(ISysLogEntryExtension extension, string format, params object[] args)
        {
            if (provider == null)
                return;

            var message = args.Length > 0 ? string.Format(format, args) : format;

            if (!IsCached(extension, SysLogEntryType.Warning, message, null))
                provider.LogWarning(extension, message);
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogError(string format, params object[] args)
        {
            LogError(null, format, args);
        }

        /// <summary>
        /// Logs an error with optional extended log information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogError(ISysLogEntryExtension extension, string format, params object[] args)
        {
            if (provider == null)
                return;

            string message = args.Length > 0 ? string.Format(format, args) : format;

            if (!IsCached(extension, SysLogEntryType.Error, message, null))
                provider.LogError(extension, message);
        }

        /// <summary>
        /// Logs an error, including a dump of the current stack.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogErrorStackDump(string format, params object[] args)
        {
            if (provider == null)
                return;

            StringBuilder   sb = new StringBuilder();
            string          message;

            if (args.Length > 0)
                sb.AppendFormat(format, args);
            else
                sb.Append(format);

            sb.Append("\r\nStack:\r\n\r\n");
            sb.Append(new CallStack(1, true).ToString());

            message = sb.ToString();

            if (!IsCached(null, SysLogEntryType.Error, message, null))
                provider.LogError(null, message);
        }

        /// <summary>
        /// Logs an error, including a dump of the current stack and with 
        /// optional extended log information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogErrorStackDump(ISysLogEntryExtension extension, string format, params object[] args)
        {
            if (provider == null)
                return;

            StringBuilder   sb = new StringBuilder();
            string          message;

            if (args.Length > 0)
                sb.AppendFormat(format, args);
            else
                sb.Append(format);

            sb.Append("\r\nStack:\r\n\r\n");
            sb.Append(new CallStack(1, true).ToString());

            message = sb.ToString();

            if (!IsCached(extension, SysLogEntryType.Error, message, null))
                provider.LogError(extension, message);
        }

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="e">The exception being logged.</param>
        public static void LogException(Exception e)
        {
            LogException(null, e);
        }

        /// <summary>
        /// Logs an exception with optional extended log information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        public static void LogException(ISysLogEntryExtension extension, Exception e)
        {
            if (provider == null)
                return;

            if (e is ThreadAbortException)
                return;     // These really aren't errors.

            if (!IsCached(extension, SysLogEntryType.Exception, null, e))
                provider.LogException(extension, e);
        }

        /// <summary>
        /// Logs an exception with an additional message.
        /// </summary>
        /// <param name="e">The exception being logged.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogException(Exception e, string format, params object[] args)
        {
            LogException(null, e, format, args);
        }

        /// <summary>
        /// Logs an exception with an additional message and
        /// optional extended log information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogException(ISysLogEntryExtension extension, Exception e, string format, params object[] args)
        {
            if (provider == null)
                return;

            if (e is ThreadAbortException)
                return;     // These really aren't errors.

            var message = args.Length > 0 ? string.Format(format, args) : format;

            if (!IsCached(extension, SysLogEntryType.Exception, message, e))
                provider.LogException(extension, e, message);
        }

        /// <summary>
        /// Logs a successful security related change or access attempt.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogSecuritySuccess(string format, params object[] args)
        {
            LogSecuritySuccess(null, format, args);
        }

        /// <summary>
        /// Logs a successful security related change or access attempt
        /// with optional extended information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogSecuritySuccess(ISysLogEntryExtension extension, string format, params object[] args)
        {
            if (provider == null)
                return;

            var message = args.Length > 0 ? string.Format(format, args) : format;

            if (!IsCached(extension, SysLogEntryType.SecuritySuccess, message, null))
                provider.LogSecuritySuccess(extension, message);
        }

        /// <summary>
        /// Logs a failed security related change or access.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogSecurityFailure(string format, params object[] args)
        {
            LogSecurityFailure(null, format, args);
        }

        /// <summary>
        /// Logs a failed security related change or access with
        /// optinal extended information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void LogSecurityFailure(ISysLogEntryExtension extension, string format, params object[] args)
        {
            if (provider == null)
                return;

            var message = args.Length > 0 ? string.Format(format, args) : format;

            if (!IsCached(extension, SysLogEntryType.SecurityFailure, message, null))
                provider.LogSecurityFailure(extension, message);
        }

        /// <summary>
        /// Logs debugging related information.
        /// </summary>
        /// <param name="component">Identifies the component described by the trace.</param>
        /// <param name="level">Specifies the relative importance of the trace.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void Trace(string component, SysLogLevel level, string format, params object[] args)
        {
            if (level != SysLogLevel.High)
                return;

            Trace(null, component, level, format, args);
        }

        /// <summary>
        /// Logs debugging related information with optional extended information.
        /// </summary>
        /// <param name="extension">Extended log information (or <c>null</c>).</param>
        /// <param name="component">Identifies the component described by the trace.</param>
        /// <param name="level">Specifies the relative importance of the trace.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public static void Trace(ISysLogEntryExtension extension, string component, SysLogLevel level, string format, params object[] args)
        {
            if (level != SysLogLevel.High)
                return;

            if (provider == null)
                return;

            var message = args.Length > 0 ? string.Format(format, args) : format;

            provider.Trace(extension, component, message);
        }
    }
}
