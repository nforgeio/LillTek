//-----------------------------------------------------------------------------
// FILE:        RegKey.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Wraps the Win32 registry APIs in a managed class.

using System;
using System.Text;
using System.Collections;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// Wraps the Win32 registry APIs in a managed class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note that this method supports only REG_SZ and REG_DWORD registry values.
    /// Some of these APIs accept the a key path.  This path specifies the path
    /// to a key and optionally, a value.  The format of this is:
    /// </para>
    /// <code language="none">
    /// RootKey\Key0\Key1\...\Key2[:Value]
    /// </code>   
    /// <para>
    /// where RootKey is HKEY_LOCAL_MACHINE etc. Key0... are the key names.  The
    /// :Value section is optional and names the key value.
    /// </para>
    /// </remarks>
    public sealed class RegKey : IDisposable
    {

        //---------------------------------------------------------------------
        // Static members

        private const string MsgBadPath     = "Invalid registry key path [{0}].";
        private const string MsgPathOnly    = "Path [{0}] must not include a value name.";
        private const string MsgKeyNotFound = "Key [{0}] not found.";
        private const string MsgNoValueName = "Path [{0}] does not include a value name.";

        /// <summary>
        /// Extracts the key components of the key/value path from the string passed.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="rootKey">Returns as one of the WinApi.HKEY_LOCAL_MACHINE,...</param>
        /// <param name="segments">Returns as the key segments.</param>
        /// <param name="valueName">Returns as the value name (or <c>null</c>).</param>
        private static void GetKeyName(string path, out uint rootKey, out string[] segments, out string valueName)
        {
            int         p, pEnd;
            string      v;
            char[]      sep = new char[] { '\\', ':' };
            ArrayList   segs = new ArrayList();

            valueName = null;

            // Parse the root key

            p = 0;
            pEnd = path.IndexOf('\\');
            if (pEnd == -1)
                throw new ArgumentException(string.Format(MsgBadPath, path));

            v = path.Substring(p, pEnd).Trim();
            switch (v.ToUpper())
            {
                case "HKEY_CLASSES_ROOT":       rootKey = WinApi.HKEY_CLASSES_ROOT; break;
                case "HKEY_CURRENT_USER":       rootKey = WinApi.HKEY_CURRENT_USER; break;
                case "HKEY_LOCAL_MACHINE":      rootKey = WinApi.HKEY_LOCAL_MACHINE; break;
                case "HKEY_USERS":              rootKey = WinApi.HKEY_USERS; break;
#if WINFULL
                case "HKEY_PERFORMANCE_DATA":   rootKey = WinApi.HKEY_PERFORMANCE_DATA; break;
                case "HKEY_CURRENT_CONFIG":     rootKey = WinApi.HKEY_CURRENT_CONFIG; break;
                case "HKEY_DYN_DATA":           rootKey = WinApi.HKEY_DYN_DATA; break;
#endif
                default:

                    throw new ArgumentException(string.Format(MsgBadPath, path));
            }

            p = pEnd + 1;

            // Parse the key segments and the value (if there is one).

            while (true)
            {
                pEnd = path.IndexOfAny(sep, p);
                if (pEnd == -1)
                {
                    v = path.Substring(p);
                    if (v != string.Empty)
                        segs.Add(v);

                    break;
                }

                v = path.Substring(p, pEnd - p);
                if (v != string.Empty)
                    segs.Add(v);

                if (path[pEnd] == ':')
                {
                    valueName = path.Substring(pEnd + 1);
                    break;
                }

                p = pEnd + 1;
            }

            segments = new string[segs.Count];
            for (int i = 0; i < segs.Count; i++)
                segments[i] = (string)segs[i];
        }

        /// <summary>
        /// Assembles the path parameter passed into a path string.
        /// </summary>
        /// <param name="rootKey">The root key.</param>
        /// <param name="segments">The path segments.</param>
        /// <param name="value">The value name (or <c>null</c>).</param>
        /// <returns>The formatted path string.</returns>
        public static string ToPath(uint rootKey, string[] segments, string value)
        {
            var sb = new StringBuilder();

            switch (rootKey)
            {
                case WinApi.HKEY_CLASSES_ROOT:      sb.Append("HKEY_CLASSES_ROOT"); break;
                case WinApi.HKEY_CURRENT_USER:      sb.Append("HKEY_CURRENT_USER"); break;
                case WinApi.HKEY_LOCAL_MACHINE:     sb.Append("HKEY_LOCAL_MACHINE"); break;
                case WinApi.HKEY_USERS:             sb.Append("HKEY_USERS"); break;
#if WINFULL
                case WinApi.HKEY_PERFORMANCE_DATA:  sb.Append("HKEY_PERFORMANCE_DATA"); break;
                case WinApi.HKEY_CURRENT_CONFIG:    sb.Append("HKEY_CURRENT_CONFIG"); break;
                case WinApi.HKEY_DYN_DATA:          sb.Append("HKEY_DYN_DATA"); break;
#endif
                default:

                    throw new ArgumentException(string.Format("Invalid root key [{0}].", rootKey));
            }

            for (int i = 0; i < segments.Length; i++)
            {
                sb.Append("\\");
                sb.Append(segments[i]);
            }

            if (value != null)
                sb.Append(":" + value);

            return sb.ToString();
        }

        /// <summary>
        /// Opens the registry key specified.
        /// </summary>
        /// <param name="rootKey">The root key value.</param>
        /// <param name="segments">The path segments.</param>
        /// <returns>The key opened or <c>null</c> if the key does not exist.</returns>
        public static RegKey Open(uint rootKey, string[] segments)
        {
            StringBuilder   sb = new StringBuilder();
            IntPtr          hKey;

            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                    sb.Append('\\');

                sb.Append(segments[i]);
            }

            if (WinApi.RegOpenKeyEx(new IntPtr((int)rootKey), sb.ToString(), 0, WinApi.KEY_ALL_ACCESS, out hKey) != WinApi.ERROR_SUCCESS)
                return null;

            return new RegKey(hKey);
        }

        /// <summary>
        /// Opens the registry key specified by the path passed.
        /// </summary>
        /// <param name="path">The key path.</param>
        /// <returns>The key opened or <c>null</c> if the key does not exist.</returns>
        public static RegKey Open(string path)
        {
            uint        rootKey;
            string[]    segments;
            string      valueName;

            GetKeyName(path, out rootKey, out segments, out valueName);

            if (valueName != null)
                throw new ArgumentException(string.Format(MsgPathOnly, path));

            return Open(rootKey, segments);
        }

        /// <summary>
        /// Returns <c>true</c> if a registry key exists at the specified path.
        /// </summary>
        /// <param name="path">The key path.</param>
        /// <returns><c>true</c> if the key exists.</returns>
        public static bool Exists(string path)
        {
            RegKey      key = null;
            uint        rootKey;
            string[]    segments;
            string      valueName;

            GetKeyName(path, out rootKey, out segments, out valueName);

            try
            {
                key = Open(rootKey, segments);
                if (key == null)
                    return false;

                if (valueName == null)
                    return true;

                return key.Get(valueName) != null;
            }
            finally
            {
                if (key != null)
                    key.Close();
            }
        }

        /// <summary>
        /// Opens the registry key specified by the path specified, creating
        /// it and any parent keys as necessary.
        /// </summary>
        /// <param name="path">The key path.</param>
        /// <returns>The created key.</returns>
        public static RegKey Create(string path)
        {
            StringBuilder   sb = new StringBuilder();
            IntPtr          hKey;
            uint            rootKey;
            string[]        segments;
            string          valueName;
            uint            disposition;

            GetKeyName(path, out rootKey, out segments, out valueName);

            if (valueName != null)
                throw new ArgumentException(string.Format(MsgPathOnly, path));

            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                    sb.Append('\\');

                sb.Append(segments[i]);
            }

            if (WinApi.RegCreateKeyEx(new IntPtr((int)rootKey), sb.ToString(), 0, null, 0, WinApi.KEY_ALL_ACCESS, IntPtr.Zero, out hKey, out disposition) != WinApi.ERROR_SUCCESS)
                throw new InvalidOperationException(string.Format("Cannot create the key [{0}].", path));

            return new RegKey(hKey);
        }

        /// <summary>
        /// Deletes the key or value specified by the path passed.
        /// </summary>
        /// <param name="path">The key/value path.</param>
        /// <remarks>
        /// <note>
        /// Child keys will also be recursively deleted.  This method
        /// does nothing if the key or value is not present.
        /// </note>
        /// </remarks>
        public static void Delete(string path)
        {
            StringBuilder   sb = new StringBuilder();
            uint            rootKey;
            string[]        segments;
            string          valueName;
            RegKey          key;

            GetKeyName(path, out rootKey, out segments, out valueName);

            if (!Exists(path))
                return;

            if (valueName != null)
            {
                key = Open(rootKey, segments);
                if (key == null)
                    throw new InvalidOperationException(string.Format("Parent key does not exist in [{0}].", path));

                try
                {
                    key.DeleteValue(valueName);
                }
                finally
                {
                    key.Close();
                }
            }
            else
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    if (i > 0)
                        sb.Append('\\');

                    sb.Append(segments[i]);
                }

                if (WinApi.RegDeleteKey(new IntPtr((int)rootKey), sb.ToString()) != WinApi.ERROR_SUCCESS)
                    throw new InvalidOperationException(string.Format("Delete of [{0}] failed.", path));
            }
        }

        /// <summary>
        /// Returns one the WinApi.REG_xxx constants specifying the 
        /// type of the registry value at the path specified or
        /// WinApi.REG_NONE if the value does not exist.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <returns>One of the WinApi.REG_xxx constants.</returns>
        public static uint GetValueType(string path)
        {
            uint        rootKey;
            string[]    segments;
            string      valueName;
            RegKey      key;

            try
            {
                GetKeyName(path, out rootKey, out segments, out valueName);
                if (valueName == null)
                    throw new ArgumentException(string.Format(MsgNoValueName));

                key = Open(rootKey, segments);
                if (key == null)
                    throw new InvalidOperationException(string.Format(MsgKeyNotFound, path));

                try
                {
                    return key.GetTypeOf(valueName);
                }
                finally
                {
                    key.Close();
                }
            }
            catch
            {
                return WinApi.REG_NONE;
            }
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <returns>Returns the key value or <c>null</c>.</returns>
        public static string GetValue(string path)
        {
            uint        rootKey;
            string[]    segments;
            string      valueName;
            RegKey      key;

            try
            {
                GetKeyName(path, out rootKey, out segments, out valueName);
                if (valueName == null)
                    throw new ArgumentException(string.Format(MsgNoValueName, path));

                key = Open(rootKey, segments);
                if (key == null)
                    throw new InvalidOperationException(string.Format(MsgKeyNotFound, path));

                try
                {
                    return key.Get(valueName);
                }
                finally
                {
                    key.Close();
                }
            }
            catch
            {

                return null;
            }
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public static string GetValue(string path, string def)
        {
            string value;

            value = GetValue(path);
            if (value != null)
                return value;
            else
                return def;
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public static int GetValue(string path, int def)
        {
            string value;

            value = GetValue(path);
            if (value == null)
                return def;

            return Config.Parse(value, def);
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public static bool GetValue(string path, bool def)
        {
            string value;

            value = GetValue(path);
            if (value == null)
                return def;

            return Config.Parse(value, def);
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public static TimeSpan GetValue(string path, TimeSpan def)
        {
            string value;

            value = GetValue(path);
            if (value == null)
                return def;

            return Config.Parse(value, def);
        }

        /// <summary>
        /// Sets the named registry value to the value passed, creating
        /// registry keys as necessary.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="value">The value to be set.</param>
        public static void SetValue(string path, string value)
        {
            uint        rootKey;
            string[]    segments;
            string      valueName;
            RegKey      key;

            GetKeyName(path, out rootKey, out segments, out valueName);
            if (valueName == null)
                throw new ArgumentException(string.Format(MsgNoValueName, path));

            key = Create(ToPath(rootKey, segments, null));
            if (key == null)
                throw new InvalidOperationException(string.Format(MsgKeyNotFound, path));

            try
            {
                key.Set(valueName, value);
            }
            finally
            {

                key.Close();
            }
        }

        /// <summary>
        /// Sets the named registry value to the value passed, creating
        /// registry keys as necessary.  The value will be saved using
        /// the REG_DWORD type.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="value">The value to be set.</param>
        public static void SetDWORDValue(string path, int value)
        {
            uint        rootKey;
            string[]    segments;
            string      valueName;
            RegKey      key;

            GetKeyName(path, out rootKey, out segments, out valueName);
            if (valueName == null)
                throw new ArgumentException(string.Format(MsgNoValueName, path));

            key = Create(ToPath(rootKey, segments, null));
            if (key == null)
                throw new InvalidOperationException(string.Format(MsgKeyNotFound, path));

            try
            {
                key.SetDWORD(valueName, value);
            }
            finally
            {
                key.Close();
            }
        }

        /// <summary>
        /// Sets the named registry value to the value passed, creating
        /// registry keys as necessary.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="value">The value to be set.</param>
        public static void SetValue(string path, int value)
        {
            SetValue(path, value.ToString());
        }

        /// <summary>
        /// Sets the named registry value to the value passed, creating
        /// registry keys as necessary.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="value">The value to be set.</param>
        public static void SetValue(string path, bool value)
        {
            SetValue(path, value ? "true" : "false");
        }

        /// <summary>
        /// Sets the named registry value to the value passed, creating
        /// registry keys as necessary.
        /// </summary>
        /// <param name="path">The key path and value name.</param>
        /// <param name="value">The value to be set.</param>
        public static void SetValue(string path, TimeSpan value)
        {
            long ms = (long)value.TotalMilliseconds;

            SetValue(path, ms.ToString() + "ms");
        }

        /// <summary>
        /// Flushes any changes to the specified registry root to the hive.
        /// </summary>
        /// <param name="rootKey">One of the WinApi.HKEY_LOCAL_MACHINE,... values.</param>
        public void Flush(uint rootKey)
        {
            WinApi.RegFlushKey(new IntPtr((int)rootKey));
        }

        //---------------------------------------------------------------------
        // Instance members

        private IntPtr hKey;   // The unmanaged key handle

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hKey">The unmanaged key handle.</param>
        private RegKey(IntPtr hKey)
        {
            this.hKey = hKey;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~RegKey()
        {
            Close();
        }

        /// <summary>
        /// Releases all resource associated with the instance.
        /// </summary>
        public void Dispose()
        {
            if (hKey != IntPtr.Zero)
            {
                WinApi.RegCloseKey(hKey);
                hKey = IntPtr.Zero;

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Releases all resource associated with the instance.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Flushes changes to the key to the hive.
        /// </summary>
        public void Flush()
        {
            WinApi.RegFlushKey(hKey);
        }

        /// <summary>
        /// Returns the type of the value passed or WinApi.REG_NONE
        /// if the value doesn't exist.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <returns>Returns the key value or WinApi.REG_NONE.</returns>
        public uint GetTypeOf(string name)
        {
            byte[]  buf = new byte[2048];
            int     err;
            uint    type;
            uint    cbOut;

            cbOut = (uint)buf.Length;
            err   = WinApi.RegQueryValueEx(hKey, name, null, out type, buf, ref cbOut);
            if (err != WinApi.ERROR_SUCCESS)
                return WinApi.REG_NONE;

            return type;
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <returns>Returns the key value or <c>null</c>.</returns>
        public string Get(string name)
        {
            byte[]  buf = new byte[2048];
            int     err;
            uint    type;
            uint    cbOut;

            cbOut = (uint)buf.Length;
            err = WinApi.RegQueryValueEx(hKey, name, null, out type, buf, ref cbOut);
            if (err != WinApi.ERROR_SUCCESS)
                return null;

            switch (type)
            {
                case WinApi.REG_DWORD:

                    return (buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24)).ToString();

                case WinApi.REG_SZ:

                    return Encoding.UTF8.GetString(buf, 0, (int)(cbOut - 1));

                default:

                    throw new NotImplementedException(string.Format("Unsupported registry type [{0}].", type));
            }
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public string Get(string name, string def)
        {
            string value;

            value = Get(name);
            if (value != null)
                return value;
            else
                return def;
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public int Get(string name, int def)
        {
            string value;

            value = Get(name);
            if (value == null)
                return def;

            return Config.Parse(value, def);
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public bool Get(string name, bool def)
        {
            string value;

            value = Get(name);
            if (value == null)
                return def;

            return Config.Parse(value, def);
        }

        /// <summary>
        /// Returns the named value from the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="def">The default value</param>
        /// <returns>The key value or the default value.</returns>
        public TimeSpan Get(string name, TimeSpan def)
        {
            string value;

            value = Get(name);
            if (value == null)
                return def;

            return Config.Parse(value, def);
        }

        /// <summary>
        /// Sets the named value in the key passed, creating a registry
        /// entry with the REG_DWORD type.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="value">The value to be set.</param>
        /// <returns>The key value or the default value.</returns>
        public void SetDWORD(string name, int value)
        {
            byte[]  buf;
            int     err;

            buf = new byte[4];
            buf[0] = (byte)(value);
            buf[1] = (byte)(value >> 8);
            buf[2] = (byte)(value >> 16);
            buf[3] = (byte)(value >> 24);

            err = WinApi.RegSetValueEx(hKey, name, 0, WinApi.REG_DWORD, buf, (uint)buf.Length);
            if (err != WinApi.ERROR_SUCCESS)
                throw new InvalidOperationException(string.Format("Error setting registry value [{0}={1}].", name, value));
        }

        /// <summary>
        /// Sets the named value in the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="value">The value to be set.</param>
        /// <returns>The key value or the default value.</returns>
        public void Set(string name, string value)
        {
            int err;

            value += (char)0;
            err    = WinApi.RegSetValueEx(hKey, name, 0, WinApi.REG_SZ, value, (uint)value.Length);

            if (err != WinApi.ERROR_SUCCESS)
                throw new InvalidOperationException(string.Format("Error setting registry value [{0}={1}].", name, value));
        }

        /// <summary>
        /// Sets the named value in the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="value">The value to be set.</param>
        /// <returns>The key value or the default value.</returns>
        public void Set(string name, int value)
        {
            Set(name, value.ToString());
        }

        /// <summary>
        /// Sets the named value in the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="value">The value to be set.</param>
        /// <returns>The key value or the default value.</returns>
        public void Set(string name, bool value)
        {
            Set(name, value ? "true" : "false");
        }

        /// <summary>
        /// Sets the named value in the key passed.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="value">The value to be set.</param>
        /// <returns>The key value or the default value.</returns>
        public void Set(string name, TimeSpan value)
        {
            long ms = (long)value.TotalMilliseconds;

            Set(name, ms.ToString() + "ms");
        }

        /// <summary>
        /// Deletes the named value from the key.
        /// </summary>
        /// <param name="name">The value name.</param>
        public void DeleteValue(string name)
        {
            if (WinApi.RegDeleteValue(hKey, name) != WinApi.ERROR_SUCCESS)
                throw new InvalidOperationException(string.Format("Delete key [{0}] failed.", name));
        }
    }
}
