//-----------------------------------------------------------------------------
// FILE:        MsgSessionAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the [MsgSession] attribute used to expose the desired
//              session properties for a message handler method to the 
//              messaging library.

using System;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the [MsgSession] attribute used to expose the desired
    /// session properties for a message handler method to the 
    /// messaging library.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Overview</u></b></para>
    /// <para>
    /// This attribute can be added to a message handler method already tagged
    /// with a [MsgHandler] attribute.  One of the <see cref="Type" /> or <see cref="TypeRef" />
    /// properties must specify the class implementing <see cref="ISession" /> to be
    /// instantiated to handle sessions for the tagged endpoint.  Use <see cref="Type" />
    /// to specify a built-in session implementation or <see cref="TypeRef" /> to specify
    /// a custom session class.
    /// </para>
    /// <code language="cs">
    /// [MsgSession(Type=SessionType.Query,Idempotent=true)]
    /// [MsgHandler]
    /// public void MyHandler1(MyMsgType1 msg) 
    /// {
    /// }
    /// </code>
    /// <note>
    /// Message handlers associated with logical and physical 
    /// endpoints can be tagged with <c>[MsgSession]</c>.
    /// </note>
    /// <para><b><u>Idempotent Sessions</u></b></para>
    /// <para>
    /// Pass <b>Idempotent=true</b> if the router's session manager
    /// should work to ensure itempotent session constraints on
    /// session dispatched to the tagged method.
    /// </para>
    /// <para><b><u>Session Specific Parameters</u></b></para>
    /// <para>
    /// The <see cref="Parameters" /> property can be used to passed additional 
    /// session-specific parameters to sessions created for the message
    /// handler. This property is formatted as a string of <b>name=value</b> pairs
    /// separated by semicolons.  This parameter is designed to be be able
    /// to be parsed by the <see cref="ArgCollection" /> class.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MsgSessionAttribute : System.Attribute
    {
        private static TimeSpan DefKeepAliveTime = TimeSpan.FromSeconds(5.0);

        /// <summary>
        /// Set to <c>true</c> if the session manager should cache information
        /// about this session including any replies for a period of time
        /// after the session completes to insure that sessions are 
        /// idempotent.  Enabling this incurs some significant overhead, so 
        /// this should be set only for sessions that really require this
        /// (defaults to <c>false</c>).
        /// </summary>
        public bool Idempotent;

        /// <summary>
        /// Specifies the interval the server side of the session should use
        /// when transmitting <see cref="SessionKeepAliveMsg" /> messages back 
        /// to the client.  This can be a standard timespan value as defined 
        /// in <see cref="Config" /> or else the fully qualified name of a 
        /// configuration timespan setting formatted as described by 
        /// <see cref="Config.GetConfigRef" />.
        /// </summary>
        /// <remarks>
        /// This value defaults to <b>5 seconds</b>.  Note that 0 is not a valid keep-alive
        /// timespan.  Specifying 0 or an invalid value will result with the
        /// <see cref="KeepAliveTime" /> property returning the default.
        /// </remarks>
        public string KeepAlive;

        /// <summary>
        /// Returns the interval the server side of the session should use
        /// when transmitting <see cref="SessionKeepAliveMsg" /> messages back 
        /// to the client.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property performs the conversion of the <see cref="KeepAlive" /> 
        /// string setting to a timespan value or the configuration lookup as necessary.
        /// </para>
        /// <para>
        /// This value defaults to <b>5 seconds</b>.
        /// </para>
        /// </remarks>
        public TimeSpan KeepAliveTime
        {
            get { return Config.ParseValue(KeepAlive, DefKeepAliveTime); }
        }

        /// <summary>
        /// Specifies the timeout value to be used within a session, indicating the
        /// maximum time either side of the session should wait to see normal message traffic
        /// or a keep-alive message.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This defaults to <c>null</c>, which specifies that the <see cref="SessionTimeoutTime" /> property
        /// will return a timespan twice times as great as <see cref="KeepAliveTime" />.  This can be a standard timespan 
        /// value as defined in <see cref="Config.Parse(string,TimeSpan)" /> or a configuration reference formatted 
        /// as described in <see cref="Config.ParseValue(string,TimeSpan)" />.
        /// </para>
        /// </remarks>
        public string SessionTimeout;

        /// <summary>
        /// Returns the timeout value to be communicated back to the client side
        /// of the session.  This represents the maximum time the client should
        /// wait without receiving a <see cref="SessionKeepAliveMsg" /> from the server 
        /// before throwing a <see cref="TimeoutException" />.
        /// </summary>
        /// <remarks>
        /// By default, this returns a timespan of twice the value of <see cref="KeepAliveTime" />.
        /// An exception will be thrown if <see cref="SessionTimeout" /> is set to a value less
        /// than <see cref="KeepAliveTime" />.  This can be a standard timespan 
        /// value as defined in <see cref="Config.Parse(string,TimeSpan)" /> or
        /// a configuration reference formatted as described in <see cref="Config.ParseValue(string,TimeSpan)" />.
        /// </remarks>
        public TimeSpan SessionTimeoutTime
        {
            get { return Config.ParseValue(SessionTimeout, Helper.Multiply(KeepAliveTime, 2)); }
        }

        /// <summary>
        /// Indicates that the handler will handle responses asynchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This defaults to <c>false</c>.
        /// </para>
        /// <para>
        /// This information can be used by session implementations to modify their
        /// behavior.  For example, <see cref="QuerySession" /> implementation uses 
        /// this to determine whether or not returning from the message handler indicates
        /// that the session should be closed.
        /// </para>
        /// </remarks>
        public bool IsAsync;

        /// <summary>
        /// Specifies the maximum time an asynchronous session (one marked with <see cref="IsAsync"/><b>=true</b>)
        /// will remain active.  Set this to "infinite" or a valid <see cref="TimeSpan" /> string
        /// as specified in <see cref="Config.Parse(string,TimeSpan)" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This defaults to "infinite" specifying <see cref="TimeSpan.MaxValue" />.
        /// </para>
        /// <para>
        /// Prudent applications with asynchronous session message handlers will set
        /// this to a reasonable value so that a session can automatically terminate
        /// the session and all activities related to it (such as sending keepalive
        /// messages).
        /// </para>
        /// <para>
        /// Advanced applications may choose to implement this tracking themselves
        /// and leave this value at the maximum.
        /// </para>
        /// </remarks>
        public string MaxAsyncKeepAlive;

        /// <summary>
        /// Returns the maximum time an asynchronous session (one marked with <see cref="IsAsync"/><b>=true</b>)
        /// will remain active.  A value of <see cref="TimeSpan.MaxValue" /> indicates
        /// that the operation should never timeout.
        /// </summary>
        /// <remarks>
        /// By default, this returns a timespan of twice the value of <see cref="KeepAliveTime" />.
        /// An exception willbe thrown if <see cref="SessionTimeout" /> is set to a value less
        /// than <see cref="KeepAliveTime" />.  This can be a standard timespan 
        /// value as defined in <see cref="Config.Parse(string,TimeSpan)" /> or
        /// a configuration reference formatted as described in 
        /// <see cref="Config.ParseValue(string,TimeSpan)" />.
        /// </remarks>
        public TimeSpan MaxAsyncKeepAliveTime
        {
            get
            {
                TimeSpan value;

                if (String.Compare(MaxAsyncKeepAlive, "infinite", true) == 0)
                    return TimeSpan.MaxValue;

                value = Config.ParseValue(MaxAsyncKeepAlive, TimeSpan.Zero);
                if (value == TimeSpan.Zero)
                    throw new ArgumentException(string.Format("Invalid [MaxAsyncKeepAlive] value: {0}", MaxAsyncKeepAlive));

                return value;
            }
        }

        /// <summary>
        /// Sets the string encoding the assembly and type to be instantiated
        /// to implement sessions created on the tagged endpoint.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can be encoded as a type reference:
        /// </para>
        /// <code language="none">&lt;type&gt;:&lt;assembly path&gt;</code>
        /// <para>
        /// Specifying the fully qualified type name and the path to the assembly
        /// file.  The string can also be encoded as a configuration reference to
        /// a type reference.
        /// </para>
        /// </remarks>
        public string TypeRef;

        /// <summary>
        /// Specifies the built-in session type to use to implement sessions
        /// created for the tagged endpoint.
        /// </summary>
        public SessionTypeID Type;

        private System.Type sessionType;    // Holds the type instance 

        /// <summary>
        /// Returns the <see cref="ISession" /> type instance to be created to implement the server
        /// side session for the tagged endpoint.
        /// </summary>
        public System.Type SessionType
        {
            get
            {
                System.Type type;

                if (sessionType != null)
                    return sessionType;

                if (TypeRef == null && Type == SessionTypeID.Custom)
                    throw new ArgumentException("One of [Type] or [TypeRef] must be specified.");

                if (TypeRef != null)
                {
                    type = Config.ParseValue(TypeRef, typeof(int));
                    if (type == typeof(int))
                        throw new ArgumentException("[TypeRef] could not be mapped to a type.");

                    if (!type.IsInstanceOfType(typeof(ISession)))
                        throw new ArgumentException(string.Format("[{0}] does not implement ISession.", type.FullName));

                    return sessionType = type;
                }

                switch (Type)
                {
                    case SessionTypeID.Unknown:

                        return sessionType = typeof(SessionBase);

                    case SessionTypeID.Query:

                        return sessionType = typeof(QuerySession);

                    case SessionTypeID.Duplex:

                        return sessionType = typeof(DuplexSession);

                    case SessionTypeID.ReliableTransfer:

                        return sessionType = typeof(ReliableTransferSession);

                    default:

                        throw new NotImplementedException("Missing built-in session type mapping.");
                }
            }
        }

        private string          parameters; // The unparsed parameters
        private ArgCollection   custom;     // Holds the parsed Parameters

        /// <summary>
        /// Returns the collection of custom session parameters specified by
        /// the <see cref="Parameters" /> property.
        /// </summary>
        public ArgCollection Custom
        {
            get { return custom; }
        }

        /// <summary>
        /// Specifies an optional set of session parameters expressed as a series of
        /// name=value pairs separated by semicolons.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful for specifying custom parameters for custom ISession implementations.
        /// These parameters will be parsed into a <see cref="ArgCollection" /> instance by the
        /// <see cref="MsgDispatcher" /> instance associated with the router and passed to the
        /// session's <see cref="ISession.InitServer" /> method in the <see cref="SessionHandlerInfo.Parameters" />
        /// property.
        /// </para>
        /// <para>
        /// This property will be parsed when the setter is called and the values retrieved
        /// will be available to sessions via the <see cref="Custom" /> property.
        /// </para>
        /// <para>
        /// This defaults as the empty string (no parameters).
        /// </para>
        /// </remarks>
        public string Parameters
        {
            get { return parameters; }

            set
            {
                parameters = value;
                custom = ArgCollection.Parse(value);
            }
        }

        /// <summary>
        /// This constructor initializes the attribute to tag a non-default, non-idempotent,
        /// physical message handler.
        /// </summary>
        public MsgSessionAttribute()
        {
            this.Idempotent        = false;
            this.KeepAlive         = "5s";
            this.SessionTimeout    = null;
            this.IsAsync           = false;
            this.MaxAsyncKeepAlive = "infinite";

            this.sessionType       = null;
            this.Type              = SessionTypeID.Custom;
            this.TypeRef           = null;

            this.parameters        = string.Empty;
            this.custom            = new ArgCollection();
        }
    }
}
