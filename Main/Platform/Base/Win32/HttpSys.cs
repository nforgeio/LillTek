//-----------------------------------------------------------------------------
// FILE:        HttpSys.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides low-level access to the Windows HTTP.SYS layer.

using System;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace LillTek.Windows
{
#if WINFULL

    /// <summary>
    /// Provides low-level access to the Windows HTTP.SYS layer.
    /// </summary>
    public static class HttpSys
    {
        /// <summary>
        /// Adds an HTTP.SYS prefix registration for a Windows account.
        /// </summary>
        /// <param name="uriPrefix">The URI prefix with optional wildcards.</param>
        /// <param name="account">The Windows account name.</param>
        public static void AddPrefixReservation(string uriPrefix, string account)
        {
            string                          sddl;
            HTTP_SERVICE_CONFIG_URLACL_SET  configInfo;
            HTTPAPI_VERSION                 httpApiVersion;
            int                             errorCode;

            uriPrefix                = ValidateUriPrefix(uriPrefix);
            sddl                     = CreateSDDL(account);
            configInfo.Key.UrlPrefix = uriPrefix;
            configInfo.Param.Sddl    = sddl;
            httpApiVersion           = new HTTPAPI_VERSION(1, 0);

            errorCode = HttpInitialize(httpApiVersion, HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
            if (errorCode != 0)
                throw GetException("HttpInitialize", errorCode);

            try
            {
                // Do our best to delete any existing ACL

                HttpDeleteServiceConfigurationAcl(IntPtr.Zero, HttpServiceConfigUrlAclInfo,
                                                  ref configInfo, Marshal.SizeOf(typeof(HTTP_SERVICE_CONFIG_URLACL_SET)), IntPtr.Zero);

                errorCode = HttpSetServiceConfigurationAcl(IntPtr.Zero, HttpServiceConfigUrlAclInfo,
                                                           ref configInfo, Marshal.SizeOf(typeof(HTTP_SERVICE_CONFIG_URLACL_SET)), IntPtr.Zero);
                if (errorCode != 0)
                    throw GetException("HttpSetServiceConfigurationAcl", errorCode);
            }
            finally
            {
                errorCode = HttpTerminate(HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
                if (errorCode != 0)
                    throw GetException("HttpTerminate", errorCode);
            }
        }

        /// <summary>
        /// Removes an HTTP.SYS prefix registration if it is present.
        /// </summary>
        /// <param name="uriPrefix">The URI prefix with optional wildcards.</param>
        /// <param name="account">The Windows account name.</param>
        public static void RemovePrefixReservation(string uriPrefix, string account)
        {
            string                          sddl;
            HTTP_SERVICE_CONFIG_URLACL_SET  configInfo;
            HTTPAPI_VERSION                 httpApiVersion;
            int                             errorCode;

            uriPrefix                = ValidateUriPrefix(uriPrefix);
            sddl                     = CreateSDDL(account);
            configInfo.Key.UrlPrefix = uriPrefix;
            configInfo.Param.Sddl    = sddl;
            httpApiVersion           = new HTTPAPI_VERSION(1, 0);

            errorCode = HttpInitialize(httpApiVersion, HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
            if (errorCode != 0)
                throw GetException("HttpInitialize", errorCode);

            try
            {
                // Do our best to delete any existing ACL

                HttpDeleteServiceConfigurationAcl(IntPtr.Zero, HttpServiceConfigUrlAclInfo,
                                                  ref configInfo, Marshal.SizeOf(typeof(HTTP_SERVICE_CONFIG_URLACL_SET)), IntPtr.Zero);
            }
            finally
            {
                errorCode = HttpTerminate(HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
                if (errorCode != 0)
                    throw GetException("HttpTerminate", errorCode);
            }
        }

        //---------------------------------------------------------------------
        // Utilities

        /// <summary>
        /// Validates a URI prefix.
        /// </summary>
        /// <param name="uriPrefix">The prefix to be checked.</param>
        /// <returns>The prefix, possibly with minor corrections.</returns>
        /// <exception cref="FormatException">Thrown if the prefix is not valid.</exception>
        public static string ValidateUriPrefix(string uriPrefix)
        {
            int hostPos;

            uriPrefix = uriPrefix.ToLower();

            if (uriPrefix.StartsWith("http://"))
                hostPos = 7;
            else if (uriPrefix.StartsWith("https://"))
                hostPos = 8;
            else
                throw new FormatException("URI prefix must have a [http://] or [https://] scheme.");

            if (uriPrefix.IndexOf(':', hostPos) == -1)
                throw new FormatException("URI prefix must include a port number.");

            if (!uriPrefix.EndsWith("/"))
                uriPrefix += "/";

            return uriPrefix;
        }

        private static Exception GetException(string fcn, int errorCode)
        {
            return new Exception(string.Format("{0} failed: {1}", fcn, GetWin32ErrorMessage(errorCode)));
        }

        private static string CreateSDDL(string account)
        {
            var sid = new NTAccount(account).Translate(typeof(SecurityIdentifier));

            // DACL that Allows Generic eXecute for the user
            // specified by account.
            // 
            // See help for HTTP_SERVICE_CONFIG_URLACL_PARAM
            // for details on what this means

            return string.Format("D:(A;;GX;;;{0})", sid);
        }

        private static string GetWin32ErrorMessage(int errorCode)
        {
            int         hr = HRESULT_FROM_WIN32(errorCode);
            Exception   x  = Marshal.GetExceptionForHR(hr);

            return x.Message;
        }

        private static int HRESULT_FROM_WIN32(int errorCode)
        {
            if (errorCode <= 0)
                return errorCode;

            return (int)((0x0000FFFFU & ((uint)errorCode)) | (7U << 16) | 0x80000000U);
        }

        //---------------------------------------------------------------------
        // P-Invoke proxies

        const int HttpServiceConfigUrlAclInfo = 2;
        const int HTTP_INITIALIZE_CONFIG = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct HTTPAPI_VERSION
        {
            public HTTPAPI_VERSION(short maj, short min)
            {
                Major = maj; Minor = min;
            }
            short Major;
            short Minor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HTTP_SERVICE_CONFIG_URLACL_KEY
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string UrlPrefix;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HTTP_SERVICE_CONFIG_URLACL_PARAM
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Sddl;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HTTP_SERVICE_CONFIG_URLACL_SET
        {
            public HTTP_SERVICE_CONFIG_URLACL_KEY Key;
            public HTTP_SERVICE_CONFIG_URLACL_PARAM Param;
        }

        [DllImport("httpapi.dll", ExactSpelling = true,
                EntryPoint = "HttpSetServiceConfiguration")]
        private static extern int HttpSetServiceConfigurationAcl(
            IntPtr mustBeZero, int configID,
            [In] ref HTTP_SERVICE_CONFIG_URLACL_SET configInfo,
            int configInfoLength, IntPtr mustBeZero2);

        [DllImport("httpapi.dll", ExactSpelling = true,
                EntryPoint = "HttpDeleteServiceConfiguration")]
        private static extern int HttpDeleteServiceConfigurationAcl(
            IntPtr mustBeZero, int configID,
            [In] ref HTTP_SERVICE_CONFIG_URLACL_SET configInfo,
            int configInfoLength, IntPtr mustBeZero2);

        [DllImport("httpapi.dll")]
        private static extern int HttpInitialize(
            HTTPAPI_VERSION version,
            int flags, IntPtr mustBeZero);

        [DllImport("httpapi.dll")]
        private static extern int HttpTerminate(int flags,
            IntPtr mustBeZero);
    }

#endif  // WINFULL
}
