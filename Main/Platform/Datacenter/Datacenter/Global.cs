//-----------------------------------------------------------------------------
// FILE:        Global.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Assembly globals.

using System;
using System.Net;
using System.Reflection;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Implements global assembly methods.
    /// </summary>
    public static class Global
    {
        private static object   syncLock      = new object();
        private static bool     msgRegistered = false;  // True if the messages have been registered

        /// <summary>
        /// Handles the registration of the assembly's message types with the 
        /// LillTek.Messaging subsystem.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Client applications using the LillTek.Datacenter classes do not need to
        /// call this directly.  This initialization will be handled internally by
        /// the library.  Server applications will need to call this though, to ensure
        /// that the message types defined within this assembly are registered.
        /// </para>
        /// <note>
        /// This method ensures that messages are registered only once.  It's
        /// not a big performance hit to call this multiple times.
        /// </note>
        /// </remarks>
        public static void RegisterMsgTypes()
        {
            lock (syncLock)
            {
                if (msgRegistered)
                    return;

                Msg.LoadTypes(Assembly.GetExecutingAssembly());
            }
        }
    }
}
