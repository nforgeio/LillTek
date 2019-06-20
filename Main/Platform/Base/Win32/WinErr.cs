//-----------------------------------------------------------------------------
// FILE:        WinErr.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Win32 error codes

using System;
using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace LillTek.Windows
{
    /// <summary>
    /// Defines Win32 error codes.
    /// </summary>
    public static class WinErr
    {
        /// <summary>
        /// Extracts the Win32 error code from an HRESULT.
        /// </summary>
        /// <param name="hr">The HRESULT.</param>
        /// <returns>The Win32 error code.</returns>
        public static int GetErrorCode(int hr)
        {
            return hr & 0x0000FFFF;
        }

        /// <summary>
        /// Extracts the Win32 facility code from an HRESULT.
        /// </summary>
        /// <param name="hr">The HRESULT.</param>
        /// <returns>The Win32 facility code.</returns>
        public static int GetFacility(int hr)
        {
            return (hr >> 16) & 0x00000FFF;
        }

        #region HRESULT Facility Codes
        public const int FACILITY_WINDOWSUPDATE = 36;
        public const int FACILITY_WINDOWS_CE = 24;
        public const int FACILITY_WINDOWS = 8;
        public const int FACILITY_URT = 19;
        public const int FACILITY_UMI = 22;
        public const int FACILITY_SXS = 23;
        public const int FACILITY_STORAGE = 3;
        public const int FACILITY_STATE_MANAGEMENT = 34;
        public const int FACILITY_SSPI = 9;
        public const int FACILITY_SCARD = 16;
        public const int FACILITY_SETUPAPI = 15;
        public const int FACILITY_SECURITY = 9;
        public const int FACILITY_RPC = 1;
        public const int FACILITY_WIN32 = 7;
        public const int FACILITY_CONTROL = 10;
        public const int FACILITY_NULL = 0;
        public const int FACILITY_METADIRECTORY = 35;
        public const int FACILITY_MSMQ = 14;
        public const int FACILITY_MEDIASERVER = 13;
        public const int FACILITY_INTERNET = 12;
        public const int FACILITY_ITF = 4;
        public const int FACILITY_HTTP = 25;
        public const int FACILITY_DPLAY = 21;
        public const int FACILITY_DISPATCH = 2;
        public const int FACILITY_DIRECTORYSERVICE = 37;
        public const int FACILITY_CONFIGURATION = 33;
        public const int FACILITY_COMPLUS = 17;
        public const int FACILITY_CERT = 11;
        public const int FACILITY_BACKGROUNDCOPY = 32;
        public const int FACILITY_ACS = 20;
        public const int FACILITY_AAF = 18;
        #endregion

        #region Common Win32 Error Codes
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_INVALID_FUNCTION = 1;
        public const int ERROR_FILE_NOT_FOUND = 2;
        public const int ERROR_PATH_NOT_FOUND = 3;
        public const int ERROR_TOO_MANY_OPEN_FILES = 4;
        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_ARENA_TRASHED = 7;
        public const int ERROR_NOT_ENOUGH_MEMORY = 8;
        public const int ERROR_INVALID_BLOCK = 9;
        public const int ERROR_BAD_ENVIRONMENT = 10;
        public const int ERROR_BAD_FORMAT = 11;
        public const int ERROR_INVALID_ACCESS = 12;
        public const int ERROR_INVALID_DATA = 13;
        public const int ERROR_OUTOFMEMORY = 14;
        public const int ERROR_INVALID_DRIVE = 15;
        public const int ERROR_CURRENT_DIRECTORY = 16;
        public const int ERROR_NOT_SAME_DEVICE = 17;
        public const int ERROR_NO_MORE_FILES = 18;
        public const int ERROR_WRITE_PROTECT = 19;
        public const int ERROR_BAD_UNIT = 20;
        public const int ERROR_NOT_READY = 21;
        public const int ERROR_BAD_COMMAND = 22;
        public const int ERROR_CRC = 23;
        public const int ERROR_BAD_LENGTH = 24;
        public const int ERROR_SEEK = 25;
        public const int ERROR_NOT_DOS_DISK = 26;
        public const int ERROR_SECTOR_NOT_FOUND = 27;
        public const int ERROR_OUT_OF_PAPER = 28;
        public const int ERROR_WRITE_FAULT = 29;
        public const int ERROR_READ_FAULT = 30;
        public const int ERROR_GEN_FAILURE = 31;
        public const int ERROR_SHARING_VIOLATION = 32;
        public const int ERROR_LOCK_VIOLATION = 33;
        public const int ERROR_WRONG_DISK = 34;
        public const int ERROR_SHARING_BUFFER_EXCEEDED = 36;
        public const int ERROR_HANDLE_EOF = 38;
        public const int ERROR_HANDLE_DISK_FULL = 39;
        public const int ERROR_NOT_SUPPORTED = 50;
        public const int ERROR_REM_NOT_LIST = 51;
        public const int ERROR_DUP_NAME = 52;
        public const int ERROR_BAD_NETPATH = 53;
        public const int ERROR_NETWORK_BUSY = 54;
        public const int ERROR_DEV_NOT_EXIST = 55;
        public const int ERROR_TOO_MANY_CMDS = 56;
        public const int ERROR_ADAP_HDW_ERR = 57;
        public const int ERROR_BAD_NET_RESP = 58;
        public const int ERROR_UNEXP_NET_ERR = 59;
        public const int ERROR_BAD_REM_ADAP = 60;
        public const int ERROR_PRINTQ_FULL = 61;
        public const int ERROR_NO_SPOOL_SPACE = 62;
        public const int ERROR_PRINT_CANCELLED = 63;
        public const int ERROR_NETNAME_DELETED = 64;
        public const int ERROR_NETWORK_ACCESS_DENIED = 65;
        public const int ERROR_BAD_DEV_TYPE = 66;

        #endregion
    }
}
