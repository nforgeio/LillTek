//-----------------------------------------------------------------------------
// FILE:        ReliableMessenger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: ReliableMessenger related utility methods.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Messaging
{
    /// <summary>
    /// ReliableMessenger related utility methods.
    /// </summary>
    public static class ReliableMessenger
    {
        /// <summary>
        /// Message string for the exception thrown a closed router is passed to a cluster.
        /// </summary>
        internal const string RouterClosedMsg = "Router must be started first.";

        /// <summary>
        /// Message string for the exception thrown when an operation is attempted on a closed messenger.
        /// </summary>
        internal const string ClosedMsg = "Messenger is closed.";

        /// <summary>
        /// Message string for the exception thrown when the confirmation endpoint is not logical.
        /// </summary>
        internal const string ConfirmEPNotLogicalMsg = "[confirmEP] is not a logical endpoint";

        /// <summary>
        /// Instantiates and initializes an <see cref="IReliableMessenger" /> plug-in defined by a configuration setting
        /// and then opens it as a client side messenger.
        /// </summary>
        /// <param name="router">The message router to associate with the messenger.</param>
        /// <param name="argsKey">The fully qualified configuration key specifying the messenger arguments formatted for <see cref="ArgCollection" />.</param>
        /// <param name="confirmCallback">The callback for delivery confirmations or <c>null</c> to disable confirmations.</param>
        /// <returns>The messenger instance.</returns>
        /// <remarks>
        /// <para>
        /// The messenger arguments must include an argument named <b>messenger-type</b> which must be
        /// a reference to a <see cref="IReliableMessenger" /> implementation formatted as described
        /// in <see cref="Config.Parse(string,System.Type)" />.
        /// </para>
        /// <para>
        /// This utility method maps the messenger type reference to a .NET assembly and type and then 
        /// instantiates an instance and then opens it as a client side messenger.  The arguments loaded
        /// are then passed to the messenger's <see cref="IReliableMessenger.OpenClient" /> method.
        /// </para>
        /// <para>
        /// The optional <b>confirm-ep</b> argument may also be present in the arguments loaded from
        /// the configuration.  If present, this specifies the logical endpoint where delivery confirmations
        /// are to be addressed.  This is used internally by <see cref="IReliableMessenger" /> implementations
        /// for the server side of a messenger to send notifications back to a client side instance.
        /// </para>
        /// </remarks>
        public static IReliableMessenger OpenClient(MsgRouter router, string argsKey, DeliveryConfirmCallback confirmCallback)
        {
            IReliableMessenger  messenger;
            ArgCollection       args;
            string              messengerType;
            string              s;
            MsgEP               confirmEP;
            System.Type         type;

            if (!router.IsOpen)
                throw new InvalidOperationException(RouterClosedMsg);

            args = ArgCollection.Parse(Config.Global.Get(argsKey));

            messengerType = args["messenger-type"];
            if (messengerType == null)
                throw new ArgumentException("Messenger arguments must specify [messenger-type].");

            type = Config.Parse(messengerType, (System.Type)null);

            if (type == null || !typeof(IReliableMessenger).IsAssignableFrom(type))
                throw new ArgumentException(string.Format("Unable to map setting [{0}] into an IReliableMessenger.", messengerType));

            s = args["confirm-ep"];
            if (s != null)
            {
                try
                {
                    confirmEP = MsgEP.Parse(s);
                }
                catch
                {
                    throw new ArgumentException("[{0}] is not a valid endpoint for [confirm-ep].", s);
                }
            }
            else
                confirmEP = null;

            messenger = Helper.CreateInstance<IReliableMessenger>(type);
            messenger.OpenClient(router, confirmEP, args, confirmCallback);

            return messenger;
        }

        /// <summary>
        /// Instantiates and initializes an <see cref="IReliableMessenger" /> plug-in defined by a configuration setting
        /// and then opens it as a server side messenger.
        /// </summary>
        /// <param name="router">The message router to associate with the messenger.</param>
        /// <param name="argsKey">The fully qualified configuration key specifying the messenger arguments formatted for <see cref="ArgCollection" />.</param>
        /// <returns>The messenger instance.</returns>
        /// <remarks>
        /// <para>
        /// The messenger arguments must include an argument named <b>messenger-type</b> which must be
        /// a reference to a <see cref="IReliableMessenger" /> implementation formatted as described
        /// in <see cref="Config.Parse(string,System.Type)" />.
        /// </para>
        /// <para>
        /// This utility method maps the messenger type reference to a .NET assembly and type and then 
        /// instantiates an instance and then opens it as a client side messenger.  The arguments loaded
        /// are then passed to the messenger's <see cref="IReliableMessenger.OpenServer" /> method.
        /// </para>
        /// </remarks>
        public static IReliableMessenger OpenServer(MsgRouter router, string argsKey)
        {
            IReliableMessenger  messenger;
            ArgCollection       args;
            string              messengerType;
            System.Type         type;

            if (!router.IsOpen)
                throw new InvalidOperationException(RouterClosedMsg);

            args = ArgCollection.Parse(Config.Global.Get(argsKey));

            messengerType = args["messenger-type"];
            if (messengerType == null)
                throw new ArgumentException("Messenger arguments must specify [messenger-type].");

            type = Config.Parse(messengerType, (System.Type)null);

            if (type == null || !typeof(IReliableMessenger).IsAssignableFrom(type))
                throw new ArgumentException(string.Format("Unable to map setting [{0}] into an IReliableMessenger.", messengerType));

            messenger = Helper.CreateInstance<IReliableMessenger>(type);
            messenger.OpenServer(router, args);

            return messenger;
        }
    }
}
