//-----------------------------------------------------------------------------
// FILE:        SharedMemOutbox.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the SecurityAttributes class which defines methods
//              that return security descriptors to be used for passing to 
//              low-level Windows APIs.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Enumerates the possible types of security descriptor.
    /// </summary>
    public enum SecurityAccess
    {
        /// <summary>
        /// All accounts granted full access.
        /// </summary>
        Unrestricted,

        /// <summary>
        /// Current account granted full access.
        /// </summary>
        CurrentAccount
    }

    /// <summary>
    /// This class abstracts some of the nonsense Windows requires for creating
    /// a security descriptor.  The constructor initializes the desired attributes and 
    /// then it can be retrieved via the <see cref="AttributesPtr" /> property.  
    /// Call <see cref="Close" /> when the attributes are no longer required.  
    /// </summary>
    public unsafe class SecurityAttributes
    {
        private IntPtr pSA = IntPtr.Zero;      // LPSECURITY_ATTRIBUTES
        private IntPtr pSD = IntPtr.Zero;      // LPSECURITY_DESCRIPTOR

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="access">The type of descriptor to create.</param>
        public SecurityAttributes(SecurityAccess access)
        {
#if WINFULL
            switch (access)
            {
                case SecurityAccess.Unrestricted:

                    pSD = Marshal.AllocHGlobal(new IntPtr(WinApi.SECURITY_DESCRIPTOR_MIN_LENGTH));
                    if (pSD == IntPtr.Zero)
                        throw new Exception("Security descriptor cannot be allocated.");

                    if (!WinApi.InitializeSecurityDescriptor(pSD, WinApi.SECURITY_DESCRIPTOR_REVISION))
                    {

                        Marshal.FreeHGlobal(pSD);
                        pSD = IntPtr.Zero;
                        throw new Exception("Security descriptor cannot be initialized.");
                    }

                    if (!WinApi.SetSecurityDescriptorDacl(pSD, true, IntPtr.Zero, false))
                    {
                        Marshal.FreeHGlobal(pSD);
                        pSD = IntPtr.Zero;
                        throw new Exception("Cannot set the discretionary ACL.");
                    }

                    WinApi.SECURITY_ATTRIBUTES sa;

                    sa = new WinApi.SECURITY_ATTRIBUTES(pSD);
                    pSA = Marshal.AllocHGlobal(sizeof(WinApi.SECURITY_ATTRIBUTES));
                    if (pSA == IntPtr.Zero)
                        throw new Exception("Security attributes cannot be allocated.");

                    Marshal.StructureToPtr(sa, pSA, false);
                    break;

                case SecurityAccess.CurrentAccount:

                    pSA = IntPtr.Zero;
                    pSD = IntPtr.Zero;
                    break;

                default:

                    Assertion.Fail("Unexpected security access type.");
                    break;
            }
#endif // WINFULL
        }

        /// <summary>
        /// Finalizer.  Note that <see cref="Close" /> should be called promptly by user code rather 
        /// than waiting for this to be called by the GC.
        /// </summary>
        ~SecurityAttributes()
        {
            Close();
        }

        /// <summary>
        /// This property returns the unmanaged security attributes pointer.
        /// </summary>
        public IntPtr AttributesPtr
        {
            get
            {
                return pSA;
            }
        }

        /// <summary>
        /// This method releases any unmanaged resources associated with the descriptor.
        /// </summary>
        public void Close()
        {
#if WINFULL
            if (pSA != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pSA);
                pSA = IntPtr.Zero;
            }

            if (pSD != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pSD);
                pSD = IntPtr.Zero;
            }
#endif // WINFULL

            GC.SuppressFinalize(this);
        }
    }
}

