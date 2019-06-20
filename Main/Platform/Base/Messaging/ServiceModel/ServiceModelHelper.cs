//-----------------------------------------------------------------------------
// FILE:        ServiceModelHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: LillTek/WCF Service Model related utilities.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.ServiceModel
{
    /// <summary>
    /// LillTek/WCF Service Model related utilities.
    /// </summary>
    public static class ServiceModelHelper
    {
        /// <summary>
        /// Indicates whether AsyncResult tracing is to be enabled for DEBUG builds.
        /// </summary>
        internal const bool AsyncTrace = false;

        /// <summary>
        /// The number of payload size samples to track when estimating the
        /// buffer required to serialize the next message.
        /// </summary>
        internal const int PayloadEstimatorSampleCount = 10;

        /// <summary>
        /// Maximum bytes allowed for deserializing SOAP message headers.
        /// </summary>
        internal const int MaxXmlHeaderSize = 16 * 1024;  // $todo(jeff.lill): This should probably be a common channel
        //                   binding parameter
        /// <summary>
        /// Maximum queued accepted channels by a given listener.
        /// </summary>
        internal const int MaxAcceptedChannels = 100;

        /// <summary>
        /// Maximum queued messages by a given listener/channel.
        /// </summary>
        internal const int MaxAcceptedMessages = 1000;

        /// <summary>
        /// Default channel/channel manager background task polling time.
        /// </summary>
        internal static readonly TimeSpan DefaultBkTaskInterval = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// Maximum amount of time a request can remain queued in an IReplyChannel or IReplySession
        /// channel listener before being aborted. 
        /// </summary>
        internal static readonly TimeSpan MaxRequestQueueTime = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Converts the WCF URI into the corresponding LillTek Messaging
        /// <see cref="MsgEP" />.
        /// </summary>
        /// <param name="uri">The WCF URI.</param>
        /// <returns>The <see cref="MsgEP" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the parameter is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the URI scheme is not valid or the URI is otherwise unsuitable.</exception>
        /// <remarks>
        /// <para>
        /// This method converts WCF URI with schemes such as <b>lilltek.logical</b>, and 
        /// <b>lilltek.abstract</b> into the equivalent <see cref="MsgEP" /> values.
        /// </para>
        /// <note>
        /// The LillTek Messaging <b>physical://</b> addressing scheme is not supported and LillTek endpoint
        /// URIs may not specify a port number.
        /// </note>
        /// </remarks>
        public static MsgEP ToMsgEP(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (!uri.IsDefaultPort)
                throw new ArgumentException("LillTek Messaging endpoint URIs may not include a port number.");

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "lilltek.physical":

                    throw new ArgumentException("The LillTek Messaging [lilltek.physical] addressing scheme is not currently supported for WCF transports.", "uri");

                case "lilltek.logical":
                case "lilltek.abstract":

                    break;      // OK

                default:

                    throw new ArgumentException(string.Format("Invalid LillTek Messaging WCF Transport scheme [{0}].  Valid schemes are [lilltek.logical] and [lilltek.abstract]", uri.Scheme), "uri");
            }

            return (MsgEP)uri.ToString().Substring(8); // strip the leading "lilltek:"
        }

        /// <summary>
        /// Converts the WCF URI into the corresponding LillTek Messaging
        /// <see cref="MsgEP" />.
        /// </summary>
        /// <param name="uri">The WCF URI.</param>
        /// <returns>The <see cref="MsgEP" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the parameter is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the URI scheme is not valid or the URI is otherwise unsuitable.</exception>
        /// <remarks>
        /// <para>
        /// This method converts WCF URI with schemes such as <b>lilltek.logical</b>, and 
        /// <b>lilltek.abstract</b> into the equivalent <see cref="MsgEP" /> values.
        /// </para>
        /// <note>
        /// The LillTek Messaging <b>physical://</b> addressing scheme is nokt supported.
        /// </note>
        /// </remarks>
        public static MsgEP ToMsgEP(string uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            return ToMsgEP(new Uri(uri));
        }

        /// <summary>
        /// Verify that a <see cref="Uri" /> is valid for a LillTek Messaging
        /// based transport channel.
        /// </summary>
        /// <param name="uri">The <see cref="Uri" /> to be tested.</param>
        /// <exception cref="ArgumentNullException">Thrown if the parameter is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the <see cref="Uri" /> has an unsupported scheme or has an explicit port number.</exception>"
        public static void ValidateEP(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (!uri.IsDefaultPort)
                throw new ArgumentException("LillTek Messaging endpoint URIs may not include a port number.");

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "lilltek.logical":
                case "lilltek.abstract":

                    return;

                case "lilltek.physical":

                    throw new ArgumentException("The LillTek Messaging [lilltek.physical] addressing scheme is not currently supported for WCF transports.", "uri");

                default:

                    throw new ArgumentException(string.Format("Invalid LillTek Messaging WCF Transport scheme [{0}].  Valid schemes are [lilltek.logical] and [lilltek.abstract]", uri.Scheme), "uri");
            }
        }

        /// <summary>
        /// Generates a globally unique WCF channel endpoint for LillTek Messaging
        /// based transports.
        /// </summary>
        /// <returns>The unique <see cref="Uri" />.</returns>
        public static Uri CreateUniqueUri()
        {
            return new Uri("lilltek.logical://wcf/" + Guid.NewGuid().ToString("D"));
        }

        /// <summary>
        /// Verifies that the <paramref name="timeout"/> passed is reasonable.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        /// <exception cref="ArgumentException">Thrown if the timeout is negative.</exception>
        /// <returns>The validated timeout time span.</returns>
        /// <remarks>
        /// <para>
        /// Note that the method ensures that the timespan will not be so large such that:
        /// </para>
        /// <code language="cs">
        /// SysTime.Now + timeout &gt; DateTime.MaxValue
        /// </code>
        /// </remarks>
        public static TimeSpan ValidateTimeout(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
                throw new ArgumentException("Invalid timeout: Cannot be negative.", "timeout");

            DateTime now = SysTime.Now;

            if (timeout >= DateTime.MaxValue - now)
                timeout = DateTime.MaxValue - now - TimeSpan.FromDays(356);

            return timeout;
        }

        /// <summary>
        /// Returns a normalized exception suiteble for WCF.
        /// </summary>
        /// <param name="e">The exception to be converted.</param>
        /// <returns>The generated <see cref="CommunicationException" />.</returns>
        /// <remarks>
        /// <para>
        /// This method returns the following exceptions without change:
        /// </para>
        /// <list type="bullet">
        ///     <item><see cref="CommunicationException" /></item>
        ///     <item><see cref="TimeoutException" /></item>
        ///     <item><see cref="ObjectDisposedException" /></item>
        /// </list>
        /// <para>
        /// <see cref="CancelException" /> will be converted into a
        /// <see cref="CommunicationCanceledException" />.
        /// </para>
        /// <para>
        /// All other exceptions will be returned as a <see cref="CommunicationException" />.
        /// </para>
        /// </remarks>
        internal static Exception GetCommunicationException(Exception e)
        {
            if (e is CommunicationException ||
                e is TimeoutException ||
                e is ObjectDisposedException ||
                e is InvalidOperationException)
            {
                return e;
            }

            if (e is CancelException)
                return new CommunicationCanceledException(e.Message, e);

            return new CommunicationException(e.Message, e);
        }

        /// <summary>
        /// Creates an <see cref="ObjectDisposedException" /> for an object instance.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>The exception.</returns>
        internal static ObjectDisposedException CreateObjectDisposedException(object instance)
        {
            return new ObjectDisposedException(instance.GetType().FullName + ": Has been closed.");
        }
    }
}
