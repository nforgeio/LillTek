//-----------------------------------------------------------------------------
// FILE:        Msg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base Msg class

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements the base class for all messages that can be sent or
    /// received by this class library.
    /// </summary>
    /// <remarks>
    /// <para><u><b>Message Serialization</b></u></para>
    /// <para>
    /// Messages are serialized to and from byte arrays that can be transmitted
    /// over a network connection.  Messages are serialized into three sections.
    /// The preamble and header fields are considered to be the message header.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Field</th>        
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top"><td>Preamble</td><td>A fixed size section containing the message magic number, the message format, and the total message size.</td></tr>
    /// <tr valign="top"><td>TypeID</td><td>A variable length UTF8 encoded string describing the type ID of the message.</td></tr>
    /// <tr valign="top"><td>Version</td><td>The message format version (defaults to 0).</td></tr>
    /// <tr valign="top"><td>TTL</td><td>Similar to a TCP time-to-live value.  Routers will decrement this every time a message is routed Messages with <b>TTL=0</b> will be discarded.</td></tr>
    /// <tr valign="top"><td>Flags</td><td>Message flag bits (see the <see cref="MsgFlag" /> enumeration).</td></tr>
    /// <tr valign="top"><td>ToEP</td><td>The string encoding the message target endpoint (optional).</td></tr>
    /// <tr valign="top"><td>FromEP</td><td>The string encoding the message source endpoint (optional).</td></tr>
    /// <tr valign="top"><td>ReceiptEP</td><td>The string encoding the message return receipt endpoint (optional).</td></tr>
    /// <tr valign="top"><td>MsgID</td><td>A GUID uniquely identifying this message (optional).</td></tr>
    /// <tr valign="top"><td>SessionID</td><td>A GUID uniquely identifying the session this message belongs to (optional).</td></tr>
    /// <tr valign="top">
    ///     <td>Extended Headers</td>
    ///     <td>
    ///     This is an optional variable size section that holds headers
    ///     that extend the basic messaging protocol.  This section is
    ///     present if the <see cref="MsgFlag.ExtensionHeaders" /> flag
    ///     is set.
    ///     </td>
    /// </tr>
    /// <tr valign="top"><td>Payload</td><td>The variable length message payload.</td></tr>
    /// </table>
    /// </div>
    /// <code language="none">
    ///      Preamble
    ///  +-------------+
    ///  |     BYTE    |     Magic number 0x88
    ///  |-------------|
    ///  |     BYTE    |     Message Format (0)
    ///  |-------------|
    ///  |    DWORD    |     Total message size
    ///  +-------------+
    ///
    ///   Header Fields
    ///  +-------------+
    ///  |     WORD    |     Type name length
    ///  |-------------|
    ///  |    BYTE[]   |     UTF8 encoded type ID string
    ///  |-------------|
    ///  |     BYTE    |     Message Version
    ///  |-------------|
    ///  |     BYTE    |     Message TTL
    ///  |-------------|
    ///  |    DWORD    |     Message Flags
    ///  |-------------|
    ///  |     WORD    |     ToEP Length
    ///  |-------------|
    ///  |    BYTE[]   |     UTF8 encoded ToEP string
    ///  |-------------|
    ///  |     WORD    |     FromEP Length
    ///  |-------------|
    ///  |    BYTE[]   |     UTF8 encoded FromEP string 
    ///  |-------------|
    ///  |     WORD    |     ReceiptEP Length
    ///  |-------------|
    ///  |    BYTE[]   |     UTF8 encoded ReceiptEP string
    ///  |-------------|
    ///  |    BYTE[]   |     16-byte MsgID GUID (optional: see <b>MsgFlags.MsgID</b>)
    ///  |-------------|
    ///  |    BYTE[]   |     16-byte SessionID GUID (optional: see <b>MsgFlags.SessionID</b>)
    ///  |-------------|
    ///  |     WORD    |     Length of the security token (optional: see <b>MsgFlags.SecurityToken</b>)
    ///  |-------------|
    ///  |    BYTE[]   |     Security token bytes (optional: see <b>MsgFlags.SecurityToken</b>)
    ///  +-------------+
    /// 
    ///  Extended Headers
    ///  +-------------+
    ///  |     BYTE    |     Number of header records
    ///  +-------------+
    /// 
    ///    Each Header
    ///  +-------------+
    ///  |     BYTE    |    Header Type ID
    ///  |-------------|
    ///  |     WORD    |    Header Length
    ///  |-------------|
    ///  |    BYTE[]   |    Binary Header Data
    ///  +-------------+
    ///     
    ///      Payload
    ///  +-------------+
    ///  |    BYTE[]   |     Payload
    ///  +-------------+
    /// </code>
    /// <note>
    /// All integers are stored in network (big endian) byte order.  <c>null</c> strings
    /// and byte arrays are encoded via a length word of <c>ushort.MaxValue</c>, where
    /// a negative length indicates a <c>null</c> string or array.
    /// </note>
    /// <para>
    /// The static <see cref="Msg.Load" /> method is responsible for instantiating and reading
    /// each known message type from an input buffer.  To do this, the Msg class
    /// must be made aware of all the message types it may be called on to
    /// instantiate.  This is done by calling <see cref="Msg.LoadTypes" /> at startup for
    /// each assembly defining message classes.  <see cref="LoadTypes" /> will reflect
    /// the assembly, looking for types derived from <see cref="Msg" />.  The method will
    /// create a mapping between the string returned from the message type's
    /// static <b>GetTypeID()</b> and the type itself.  For this to work, every
    /// type must implement a parameterless constructor.
    /// </para>
    /// </remarks>
    public class Msg : ISizedItem
    {
        //---------------------------------------------------------------------
        // Static members

        private const string    msgBadMsgFormat   = "Invalid message format.";
        private const string    msgNoInstantiate  = "Error instantiating the message. Make sure the class has a constructor with no parameters.";
        private const string    msgTypeIDMismatch = "TypeID mismatch.";
        private const string    msgUnknownFormat  = "Unsupported message format.";

        internal const byte     magic        = 0x88;   // Message magic number
        internal const int      cbPreamble   = 6;      // Byte length of a message preamble
        internal const int      msgLenOffset = 2;      // Byte offset of the message length DWORD in the preamble

        private static System.Type  baseType;                   // Type of this class
        private static object       syncLock = new object();
        private static byte[]       preamble = new byte[cbPreamble];

        // Maps message type IDs MsgInfo records

        private static Dictionary<string, MsgInfo> typeIDMap;

        // Maps message types to MsgInfo records

        private static Dictionary<System.Type, MsgInfo> typeMap;

        /// <summary>
        /// Used for relating message type IDs to the actual system type.
        /// </summary>
        private sealed class MsgInfo
        {
            public string       TypeID;         // The message's type ID
            public System.Type  Type;           // The message type

            public MsgInfo(string typeID, System.Type type)
            {
                this.TypeID = typeID;
                this.Type   = type;
            }
        }

        /// <summary>
        /// Static constructor initializes the message type map table.
        /// </summary>
        static Msg()
        {
            baseType  = typeof(Msg);
            typeIDMap = new Dictionary<string, MsgInfo>();
            typeMap   = new Dictionary<Type, MsgInfo>();

            // Load any message type mappings from this assembly

            LoadTypes(Assembly.GetExecutingAssembly());
        }

        /// <summary>
        /// This message scans the assembly passed for message types
        /// derived from this class and adds them to the set of message
        /// types that <see cref="Load" /> will be able to instantiate.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <remarks>
        /// <para>
        /// This works as follows:
        /// </para>
        /// <list type="number">
        ///     <item>
        ///     The method reflects all types in the assembly looking
        ///     for those types that derive from Msg and are not
        ///     tagged with a <c>[MsgIgnore]</c> attribute.
        ///     </item>  
        ///     <item>
        ///     For each type found, the method looks for a static
        ///     method called GetTypeID().  If found this method 
        ///     is called to retrieve the type ID string.  If not
        ///     found, the type's fully qualified name will be used
        ///     as the type ID.
        ///     </item>    
        ///     <item>
        ///     A record will be saved within a static class table
        ///     mapping the typeID to the assembly for use by
        ///     the static <see cref="Load" /> method.
        ///     </item>
        /// </list>       
        /// <para>
        /// Finally, note that all message classes must be declared as
        /// public to work properly.
        /// </para>
        /// </remarks>
        public static void LoadTypes(Assembly assembly)
        {
            Type[]              types;
            Type                ancestor;
            bool                isMsg;
            MethodInfo          getTypeID;
            string              typeID;
            MsgInfo             msgInfo;
            ConstructorInfo[]   constructors;
            bool                defConstructor;

            types = assembly.GetTypes();
            foreach (Type type in types)
            {
                isMsg = false;
                ancestor = type;

                while (ancestor != null)
                {
                    if (ancestor == baseType)
                    {
                        isMsg = true;
                        break;
                    }

                    ancestor = ancestor.BaseType;
                }

                if (!isMsg)
                    continue;

                if (type.GetCustomAttributes(typeof(MsgIgnoreAttribute), false).Length != 0)
                    continue;   // Skip types tagged with [MsgIgnore]

                getTypeID = type.GetMethod("GetTypeID",
                                           BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
                                           null, new Type[0], null);
                if (getTypeID != null)
                    typeID = (string)getTypeID.Invoke(null, null);
                else
                    typeID = type.FullName;

                // Verify that the type has a default constructor

                defConstructor = false;
                constructors   = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);

                foreach (ConstructorInfo constructor in constructors)
                    if (constructor.GetParameters().Length == 0)
                    {
                        defConstructor = true;
                        break;
                    }

                if (!defConstructor)
                    throw new MsgException("Message type [{0}] does not have a default constructor.", type.FullName);

                msgInfo = new MsgInfo(typeID, type);

                lock (syncLock)
                {
                    if (!typeMap.ContainsKey(type))
                        typeMap.Add(type, msgInfo);

                    if (!typeIDMap.ContainsKey(typeID))
                        typeIDMap.Add(typeID, msgInfo);
                }
            }
        }

        /// <summary>
        /// Used by unit tests to clear the <see cref="Msg" /> class' static type map.
        /// </summary>
        public static void ClearTypes()
        {
            lock (syncLock)
            {
                typeMap.Clear();
                typeIDMap.Clear();

                LoadTypes(Assembly.GetExecutingAssembly());
            }
        }

        /// <summary>
        /// Used by unit tests to load a specific message type into the 
        /// message type map.  This method ignores [MsgIgnore] attributes.
        /// </summary>
        /// <param name="type">The message type.</param>
        internal static void LoadType(System.Type type) 
        {
            Type        ancestor;
            bool        isMsg;
            MethodInfo  getTypeID;
            string      typeID;
            MsgInfo     msgInfo;

            isMsg    = false;
            ancestor = type;

            while (ancestor != null) 
            {
                if (ancestor == baseType) 
                {
                    isMsg = true;
                    break;
                }

                ancestor = ancestor.BaseType;
            }

            if (!isMsg)
                throw new ArgumentException("Type must derive from [Msg].");

            getTypeID = type.GetMethod("GetTypeID",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
                                        null,new Type[0],null);
            if (getTypeID != null)
                typeID = (string) getTypeID.Invoke(null, null);
            else
                typeID = type.FullName;

            msgInfo = new MsgInfo(typeID,type);

            lock (syncLock)
            {
                if (!typeMap.ContainsKey(type))
                    typeMap.Add(type,msgInfo);

                if (!typeIDMap.ContainsKey(typeID))
                    typeIDMap.Add(typeID,msgInfo);
            }
        }

        /// <summary>
        /// Used by unit tests to map a message type ID to the corresponding
        /// system type.
        /// </summary>
        /// <param name="typeID">The type ID to look for.</param>
        /// <returns>The mapped message type or <c>null</c>.</returns>
        internal static System.Type MapTypeID(string typeID) 
        {
            MsgInfo     msgInfo;

            lock (syncLock)
                typeIDMap.TryGetValue(typeID, out msgInfo);

            if (msgInfo == null)
                return null;

            return msgInfo.Type;
        }

        /// <summary>
        /// Validates that the message preamble passed is valid and
        /// then returns the total size of the message.
        /// </summary>
        /// <param name="preamble">The preamble buffer.</param>
        internal static int MsgSize(byte[] preamble)
        {
            try
            {
                if (preamble[0] != magic)
                    throw new MsgException(msgBadMsgFormat);

                int pos;

                pos = msgLenOffset;
                return Helper.ReadInt32(preamble, ref pos);
            }
            catch (Exception e)
            {
                throw new MsgException(msgBadMsgFormat, e);
            }
        }

        /// <summary>
        /// Serializes the message passed into a byte array.
        /// </summary>
        /// <param name="msg">The message to be serialized.</param>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <returns>The size of the message written in bytes.</returns>
        /// <remarks>
        /// The buffer returned includes the preamble, the message type,
        /// and the payload.
        /// </remarks>
        public static int Save(EnhancedStream es, Msg msg)
        {
            string          typeID;
            int             cbMsg;
            MsgInfo         msgInfo;
            EnvelopeMsg     envelopeMsg;
            int             startPos;

            // Write the type and payload first to determine the message
            // size and then go back and write the preamble.  Note that
            // for envelope messages, we're going to get the typeID from the
            // message instance, rather than looking it up in the type map.

            envelopeMsg = msg as EnvelopeMsg;
            if (envelopeMsg != null)
                typeID = envelopeMsg.TypeID;
            else
            {
                lock (syncLock)
                    typeMap.TryGetValue(msg.GetType(), out msgInfo);

                if (msgInfo == null)
                    throw new MsgException("Unregistered message class [{0}].", msg.GetType().FullName);

                typeID = msgInfo.TypeID;
            }

            startPos = (int)es.Position;

            es.Write(preamble, 0, preamble.Length); // Leave room for the preamble
            es.WriteString16(typeID);
#if DEBUG
            msg.writeBase = false;                  // $hack(jeff.lill): Strictly speaking, this isn't threadsafe
#endif
            msg.WriteBase(es);
#if DEBUG
            Assertion.Test(msg.writeBase, "Derived [Msg] classes must call [base.WriteBase()].");
#endif
            msg.WritePayload(es);
            cbMsg = (int)es.Position;

            es.Position = startPos;
            es.WriteByte(magic);                    // Magic number
            es.WriteByte(0);                        // Message format
            es.WriteInt32(cbMsg);                   // Total message length

            es.Position = startPos + cbMsg;
            return cbMsg;
        }

        /// <summary>
        /// Instantiates a message from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the message is to be read from.</param>
        /// <returns>The instantiated message.</returns>
        /// <remarks>
        /// <note>
        /// The buffer passed must include the preamble,
        /// message type, and the payload data.
        /// </note>
        /// </remarks>
        public static Msg Load(EnhancedStream es)
        {
            MsgInfo     msgInfo;
            Msg         msg;
            string      typeID;
            int         cbMsg;
            int         cbPayload;

            try
            {
                // Magic number

                if (es.ReadByte() != magic)
                    throw new MsgException(msgBadMsgFormat);

                // Message format

                if (es.ReadByte() != 0)
                    throw new MsgException(msgUnknownFormat);

                // Total message length

                cbMsg = es.ReadInt32();

                // Message type ID

                typeID = es.ReadString16();
                lock (syncLock)
                    typeIDMap.TryGetValue(typeID, out msgInfo);

                if (msgInfo == null)
                {
                    // This can happen when routing a message whose underlying type
                    // is not registered (and likely) doesn't even exist in the
                    // current application.  So we're going to instantiate a
                    // EnvelopeMsg instead to hold this message's state so it can
                    // still be routed.

                    msg = new EnvelopeMsg(typeID);
                }
                else
                {
                    try
                    {
                        msg = Helper.CreateInstance<Msg>(msgInfo.Type);
                        if (msg == null)
                            throw new MsgException(msgNoInstantiate);
                    }
                    catch (Exception e)
                    {
                        throw new MsgException(msgNoInstantiate, e);
                    }
                }
#if DEBUG
                msg.readBase = false;   // $hack(jeff.lill): Strictly speaking, this isn't threadsafe
#endif
                msg.ReadFrom(es);
#if DEBUG
                Assertion.Test(msg.readBase, "Derived [Msg] classes must call [base.ReadFrom()].");
#endif
                cbPayload = cbMsg - (int)es.Position;
                msg.ReadPayload(es, cbPayload);

                if ((int)es.Position != cbMsg)
                    throw new MsgException(msgBadMsgFormat);

                return msg;
            }
            catch (MsgException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new MsgException(msgBadMsgFormat, e);
            }
        }

        //---------------------------------------------------------------------
        // Instance members.

        private int                 version;        // The message version number
        private MsgFlag             flags;          // Message flag bits
        private int                 ttl;            // Message time-to-live (hops remaining)
        private MsgEP               toEP;           // Destination address for messages to be sent (or null)
        private MsgEP               fromEP;         // Source address for received messages (or null)
        private MsgEP               receiptEP;      // Destination for any generated ReceiptMsgs (or null)
        private Guid                msgID;          // Unique message ID (or Guid.Empty)
        private Guid                sessionID;      // Unique session ID (or null)
#if WINFULL
        private ISession            session;        // Associated session (or null)
#endif
        private MsgHeaderCollection extHeaders;     // Extension headers (or null)
        private byte[]              token;          // Security token bytes (or null)
#if WINFULL
        private IMsgChannel         recvChannel;    // Channel the message was received on (or null)
#endif
        private byte[]              msgFrame;       // Used internally to hold the serialized form of a message
#if DEBUG
        private bool                readBase;       // Used to verify that derived classes called 
        private bool                writeBase;      // ReadFrom() and WriteBase() properly.
        private bool                inUse;          // True if the message is considered to be
                                                    // owned by the messaging library.
#endif

        /// <summary>
        /// Constructor.
        /// </summary>
        public Msg()
        {
            this.version     = 0;
            this.flags       = 0;
            this.ttl         = 0;
            this.toEP        = null;
            this.fromEP      = null;
            this.receiptEP   = null;
            this.msgID       = Guid.Empty;
            this.sessionID   = Guid.Empty;
#if WINFULL
            this.session     = null;
#endif
            this.token       = null;
            this.extHeaders  = null;
#if WINFULL
            this.recvChannel = null;
#endif
            this.msgFrame    = null;
#if DEBUG
            this.inUse       = false;
#endif
        }

#if WINFULL
        /// <summary>
        /// Returns a <see cref="MsgRequestContext" /> structure that holds enough
        /// information about query request messages so that a reply can be submitted
        /// back to the router without having to retain the request.
        /// </summary>
        /// <returns>The <see cref="MsgRequestContext" /> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the message passed does not have all of the headers necessary to be a request.</exception>
        public MsgRequestContext CreateRequestContext()
        {
            const string errorMsg = "Message is not part of a [QuerySession] or [DuplexSession].";

            if (session == null)
                throw new ArgumentException(errorMsg);

            DuplexSession   duplexSession;
            QuerySession    querySession;

            querySession = session as QuerySession;
            if (querySession != null)
                return new MsgRequestContext(session.Router, this);

            duplexSession = session as DuplexSession;
            if (duplexSession != null)
                return new MsgRequestContext(duplexSession, this);

            throw new ArgumentException(errorMsg);
        }
#endif

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="_MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>Returns a clone of the message.</returns>
        /// <remarks>
        /// <para>
        /// This base method works by serializing and then deseralizing a new copy of
        /// the message to a buffer and then generating a new <see cref="_MsgID" /> GUID.  This
        /// implementation will be relatively costly in terms of processor and
        /// memory resources.
        /// </para>
        /// <para>
        /// Derived messages can choose to override this and implement a shallow
        /// clone instead, using the <see cref="CopyBaseFields" /> method.  Note that
        /// the <see cref="CopyBaseFields" /> method ensures that a new <see cref="_MsgID" />
        /// property is regenerated if the original ID field is not empty.
        /// </para>
        /// </remarks>
        public virtual Msg Clone()
        {
            var     es = new EnhancedBlockStream(1024, 1024);
            Msg     clone;

            Msg.Save(es, this);
            es.Position = 0;
            clone = Msg.Load(es);

            if (this.msgID != Guid.Empty)
                clone.msgID = Helper.NewGuid();

            return clone;
        }

        /// <summary>
        /// Shallow copies the base fields from the source message to this instance.
        /// </summary>
        /// <param name="source">The source message.</param>
        /// <param name="regenMsgID">
        /// Pass as <c>true</c> to renegerate the <see cref="_MsgID" /> property if the 
        /// source message ID property is not empty.
        /// </param>
        /// <remarks>
        /// Use this in overriden <see cref="Clone" /> method implementations
        /// to ensure that the base message fields are copied properly.
        /// </remarks>
        protected virtual void CopyBaseFields(Msg source, bool regenMsgID)
        {
            this.version   = source.version;
            this.flags     = source.flags;
            this.ttl       = source.ttl;
            this.toEP      = source.toEP;
            this.fromEP    = source.fromEP;
            this.receiptEP = source.receiptEP;
            this.sessionID = source.sessionID;
            this.token     = source.token;

            if (source.msgID == Guid.Empty)
                this.msgID = Guid.Empty;
            else if (regenMsgID)
                this.msgID = Helper.NewGuid();
            else
                this.msgID = source.msgID;

            if (source.extHeaders != null)
                this.extHeaders = source.extHeaders.Clone();
        }

        /// <summary>
        /// The message version number.
        /// </summary>
        /// <remarks>
        /// This value will be serialized will all messages and is to be used
        /// to deal somewhat intelligently with the inevitable changes to the
        /// protocol.  This is saved as a 16-bit integer and defaults to 0.
        /// </remarks>
        public int _Version
        {
            get { return version; }
            set { version = value; }
        }

        /// <summary>
        /// Message flag bits.
        /// </summary>
        public MsgFlag _Flags
        {
            get { return flags; }
            set { flags = value; }
        }

        /// <summary>
        /// The number of router hops remaining for this message (0..255).
        /// </summary>
        /// <remarks>
        /// This defaults to 0 for newly created messages.  This indicates to
        /// the MsgRouter transmission methods that it should set the message
        /// TTL to a router specific default value.  Setting a non-zero TTL
        /// will override the router setting.
        /// </remarks>
        public int _TTL
        {
            get { return ttl; }
            set { ttl = value; }
        }

        /// <summary>
        /// The message's destination endpoint (or <c>null</c>).
        /// </summary>
        public MsgEP _ToEP
        {
            get { return toEP; }
            set { toEP = value; }
        }

        /// <summary>
        /// The message's source endpoint (or <c>null</c>).
        /// </summary>
        public MsgEP _FromEP
        {
            get { return fromEP; }
            set { fromEP = value; }
        }

        /// <summary>
        /// The endpoint where a return ReceiptMsg should be sent
        /// by a MsgRouter instance when a message is dispatched
        /// to a message handler (or <c>null</c>).
        /// </summary>
        public MsgEP _ReceiptEP
        {
            get { return receiptEP; }
            set { receiptEP = value; }
        }

        /// <summary>
        /// The globally unique message ID (or Guid.Empty).
        /// </summary>
        public Guid _MsgID
        {
            get { return msgID; }

            set
            {
                msgID = value;

                if (value == Guid.Empty)
                    flags &= ~MsgFlag.MsgID;
                else
                    flags |= MsgFlag.MsgID;
            }
        }

        /// <summary>
        /// The globally unique session ID (or Guid.Empty).
        /// </summary>
        public Guid _SessionID
        {
            get { return sessionID; }

            set
            {
                sessionID = value;

                if (value == Guid.Empty)
                    flags &= ~MsgFlag.SessionID;
                else
                    flags |= MsgFlag.SessionID;
            }
        }

        /// <summary>
        /// The security token bytes or <c>null</c>.
        /// </summary>
        public byte[] _SecurityToken
        {
            get { return token; }

            set
            {
                token = value;

                if (value == null)
                    flags &= ~MsgFlag.SecurityToken;
                else
                    flags |= MsgFlag.SecurityToken;
            }
        }

        /// <summary>
        /// Returns the set of the messages's extension headers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property will allocate and return an empty
        /// collection if necessary.
        /// </note>
        /// </remarks>
        public MsgHeaderCollection _ExtensionHeaders
        {
            get
            {
                if (extHeaders == null)
                    extHeaders = new MsgHeaderCollection();

                return extHeaders;
            }
        }

        /// <summary>
        /// <c>true</c> if the message is considered to be owned by the
        /// messaging library.  This is used by DEBUG builds to test
        /// for illegal attempts to reuse a message instance.
        /// </summary>
        internal bool _InUse
        {
            get
            {
#if DEBUG
                return inUse;
#else
                return false;
#endif
            }

            set
            {
#if DEBUG
                inUse = value;
#endif
            }
        }

        /// <summary>
        /// Returns the serialized size of the message if known, 0 otherwise.
        /// </summary>
        public int Size
        {
            get { return msgFrame == null ? 0 : msgFrame.Length; }
        }

#if WINFULL
        /// <summary>
        /// The associated session (or <c>null</c>).
        /// </summary>
        public ISession _Session
        {
            get { return session; }
            set { session = value; }
        }

        /// <summary>
        /// The channel the message was received on (or <c>null</c>).
        /// </summary>
        public IMsgChannel _ReceiveChannel
        {
            get { return recvChannel; }
            set { recvChannel = value; }
        }

        /// <summary>
        /// Used internally by some <see cref="IMsgChannel" /> implementations
        /// to save the serialized form of a message before transmission for 
        /// performance.
        /// </summary>
        internal byte[] _MsgFrame
        {
            get { return msgFrame; }
            set { msgFrame = value; }
        }

        /// <summary>
        /// Initializes the channel endpoints of the message's TO 
        /// endpoint, creating the endpoint object if necessary.
        /// </summary>
        /// <param name="toChannelEP">The channel endpoint.</param>
        public void _SetToChannel(ChannelEP toChannelEP)
        {
            if (this.toEP == null)
                this._ToEP = new MsgEP(toChannelEP);

            this.toEP.ChannelEP = toChannelEP;
        }

        /// <summary>
        /// Initializes the channel endpoints of the message's FROM 
        /// endpoint, creating the endpoint object if necessary.
        /// </summary>
        /// <param name="fromChannelEP">The channel endpoint.</param>
        public void _SetFromChannel(ChannelEP fromChannelEP)
        {
            if (this.fromEP == null)
                this.fromEP = new MsgEP(fromChannelEP);

            this.fromEP.ChannelEP = fromChannelEP;
        }
#endif

#if WINFULL
        /// <summary>
        /// Adds trace summary information about this message to the StringBuilder
        /// passed
        /// </summary>
        /// <param name="router">The associated router (or <c>null</c>).</param>
        /// <param name="sb">The string builder.</param>
        /// <remarks>
        /// The string returned should be a relatively short, single line of text.
        /// This base implementation will return the type name of the message class.
        /// </remarks>
        [Conditional("TRACE")]
        public virtual void _TraceSummary(MsgRouter router, StringBuilder sb)
        {
            sb.Append(router.GetType().Name + ": ");

            if (router != null)
                sb.AppendFormat("app={0}", router.AppName);
        }
#endif

#if WINFULL
        /// <summary>
        /// Add detailed trace information about this message to the
        /// StringBuilder passed.
        /// </summary>
        /// <param name="router">The associated router (or <c>null</c>).</param>
        /// <param name="sb">The string builder.</param>
        /// <remarks>
        /// <para>
        /// The string returned may include multiple lines of text broken
        /// via CRLF sequences.  The text shouldn't be much longer than
        /// 1300 bytes to avoid being clipped when transmitted via
        /// a <see cref="NetTrace.Write(string, int, string, string, string)" /> call.
        /// </para>
        /// <para>
        /// This base implementation will include details about the 
        /// message's header fields.
        /// </para>
        /// </remarks>
        [Conditional("TRACE")]
        public virtual void _TraceDetails(MsgRouter router, StringBuilder sb)
        {

            const string format =
@"Router:  {0} ({1})
TypeID:  {2}
To:      {3}
From:    {4}
Receipt: {5}
Flags:   {6}
TTL:     {7}
Ver:     {8}
MsgID:   {9}
Session: {10}
Token:   {11}
-------------------
";
            MsgInfo msgInfo;
            string sMsg;
            string sTo        = toEP == null ? string.Empty : toEP.ToString();
            string sFrom      = fromEP == null ? string.Empty : fromEP.ToString();
            string sReceipt   = receiptEP == null ? string.Empty : receiptEP.ToString();
            string sFlags     = flags.ToString();
            string sTTL       = ttl.ToString();
            string sVersion   = version.ToString();
            string sMsgID     = msgID == Guid.Empty ? string.Empty : msgID.ToString();
            string sSessionID = sessionID == Guid.Empty ? string.Empty : sessionID.ToString();
            string sToken     = token == null ? string.Empty : Helper.ToHex(token);

            lock (syncLock)
                typeMap.TryGetValue(this.GetType(), out msgInfo);

            if (msgInfo == null)
                sMsg = this.GetType().Name;
            else
            {
                var envelopeMsg = this as EnvelopeMsg;

                if (envelopeMsg != null)
                    sMsg = envelopeMsg.TypeID + " (Envelope)";
                else
                    sMsg = msgInfo.TypeID;
            }

            sb.AppendFormat(null, format, router != null ? router.AppName : string.Empty, router != null ? router.GetType().Name : string.Empty, sMsg,
                                  sTo, sFrom, sReceipt, sFlags, sTTL, sVersion, sMsgID, sSessionID, sToken);
        }

        /// <summary>
        /// Writes information about this message to the trace source.
        /// </summary>
        /// <param name="router">The associated router (or <c>null</c>).</param>
        /// <param name="detail">The detail level (0..255).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="summaryAppend">Text to be appended to the trace summary (or <c>null</c>).</param>
        /// <param name="args">Optional lines of text to be appended to the trace details.</param>
        [Conditional("TRACE")]
        public void _Trace(MsgRouter router, int detail, string tEvent, string summaryAppend, params string[] args)
        {
            StringBuilder   sb;
            string          summary;
            string          details;

            sb = new StringBuilder(120);
            _TraceSummary(router, sb);
            summary = sb.ToString();

            if (summaryAppend != null)
                summary += summaryAppend;

            sb = new StringBuilder(512);
            _TraceDetails(router, sb);
            details = sb.ToString();

            for (int i = 0; i < args.Length; i++)
                sb.Append(args[i] + "\r\n");

            NetTrace.Write(MsgRouter.TraceSubsystem, detail, tEvent + ": [" + this.GetType().Name + "]", summary, details);
        }

        /// <summary>
        /// Writes information about this message and the exception passed to the
        /// trace source.
        /// </summary>
        /// <param name="router">The associated router (or <c>null</c>).</param>
        /// <param name="e">The exception.</param>
        [Conditional("TRACE")]
        public void _Trace(MsgRouter router, Exception e)
        {
            const string format =
@"Exception: {0}
Message:   {1}
Stack:

";
            StringBuilder   sb;
            string          summary;
            string          details;

            sb = new StringBuilder(120);
            _TraceSummary(router, sb);
            summary = sb.ToString();

            sb = new StringBuilder(512);
            sb.AppendFormat(null, format, e.GetType().ToString(), e.Message);
            sb.AppendFormat(e.StackTrace);
            sb.Append("----------\r\n");

            _TraceDetails(router, sb);
            details = sb.ToString();

            NetTrace.Write(MsgRouter.TraceSubsystem, 0, "Exception: [" + this.GetType().Name + "]", summary, details);
        }
#endif

        /// <summary>
        /// Serializes the payload of the base classes into the memory
        /// stream.
        /// </summary>
        /// <param name="es">The stream where the output is to be written.</param>
        /// <remarks>
        /// Classes that are designed to be derived from should implement
        /// this method to serialize their content.  Note that the base.WriteBase()
        /// method should be called before doing this to ensure that any 
        /// ancestor classes will be serialized properly.
        /// </remarks>
        protected virtual void WriteBase(EnhancedStream es)
        {
#if DEBUG
            this.writeBase = true;
#endif
            if (extHeaders != null && extHeaders.Count > 0)
                flags |= MsgFlag.ExtensionHeaders;
#if DEBUG
            // Verify the consistency of the MsgID and SessionID flags and fields.

            if (msgID == Guid.Empty)
            {
                if ((flags & MsgFlag.MsgID) != 0)
                {
                    SysLog.LogWarning("Message [{0}] has [MsgFlag.MsgID] set but has no [MsgID].  [MsgFlag.MsgID] flag will be cleared.");
                    flags &= ~MsgFlag.MsgID;
                }
            }
            else
            {
                if ((flags & MsgFlag.MsgID) == 0)
                {
                    SysLog.LogWarning("Message [{0}] has [MsgFlag.MsgID] cleared but has a [MsgID].  [MsgFlag.MsgID] will be set.");
                    flags |= MsgFlag.MsgID;
                }
            }

            if (sessionID == Guid.Empty)
            {
                if ((flags & MsgFlag.SessionID) != 0)
                {
                    SysLog.LogWarning("Message [{0}] has [MsgFlag.SessionID] set but has no [SessionID].  [MsgFlag.SessionID] flag will be cleared.");
                    flags &= ~MsgFlag.SessionID;
                }
            }
            else
            {
                if ((flags & MsgFlag.SessionID) == 0)
                {
                    SysLog.LogWarning("Message [{0}] has [MsgFlag.SessionID] cleared but has a [SessionID].  [MsgFlag.SessionID] will be set.");
                    flags |= MsgFlag.SessionID;
                }
            }

            if (token == null)
            {
                if ((flags & MsgFlag.SecurityToken) != 0)
                {
                    SysLog.LogWarning("Message [{0}] has [MsgFlag.SecurityToken] set but has no [SecurityToken].  [MsgFlag.SecurityToken] flag will be cleared.");
                    flags &= ~MsgFlag.SessionID;
                }
            }
            else
            {
                if ((flags & MsgFlag.SecurityToken) == 0)
                {
                    SysLog.LogWarning("Message [{0}] has [MsgFlag.SecurityToken] cleared but has a [SecurityToken].  [MsgFlag.SecurityToken] will be set.");
                    flags |= MsgFlag.SecurityToken;
                }
            }
#endif

            es.WriteByte((byte)version);
            es.WriteByte((byte)ttl);
            es.WriteInt32((int)flags);

            es.WriteString16(toEP != null ? toEP.ToString() : null);
            es.WriteString16(fromEP != null ? fromEP.ToString() : null);
            es.WriteString16(receiptEP != null ? receiptEP.ToString() : null);

            if ((flags & MsgFlag.MsgID) != 0)
                es.WriteBytesNoLen(msgID.ToByteArray());

            if ((flags & MsgFlag.SessionID) != 0)
                es.WriteBytesNoLen(sessionID.ToByteArray());

            if ((flags & MsgFlag.SecurityToken) != 0)
                es.WriteBytes16(token);

            // Write the extended headers

            if ((flags & MsgFlag.ExtensionHeaders) != 0)
            {
                es.WriteByte((byte)extHeaders.Count);
                for (int i = 0; i < extHeaders.Count; i++)
                {
                    var header = extHeaders[i];

                    es.WriteByte((byte)header.HeaderID);
                    es.WriteBytes16(header.Contents);
                }
            }
        }

        /// <summary>
        /// Loads the message payload of the base classes from the memory
        /// stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream holding the payload data.</param>
        /// <remarks>
        /// Classes that are designed to be derived from should implement
        /// this method to serialize their content.  Note that the base.ReadFrom()
        /// method should be called before doing this to ensure that any 
        /// ancestor classes will be unserialized properly.
        /// </remarks>
        protected virtual void ReadFrom(EnhancedStream es)
        {
#if DEBUG
            this.readBase  = true;
#endif
            this.version   = es.ReadByte();
            this.ttl       = es.ReadByte();
            this.flags     = (MsgFlag)es.ReadInt32();
            this.toEP      = MsgEP.Parse(es.ReadString16());
            this.fromEP    = MsgEP.Parse(es.ReadString16());
            this.receiptEP = MsgEP.Parse(es.ReadString16());

            if ((flags & MsgFlag.MsgID) != 0)
                msgID = new Guid(es.ReadBytes(16));
            else
                msgID = Guid.Empty;

            if ((flags & MsgFlag.SessionID) != 0)
                sessionID = new Guid(es.ReadBytes(16));
            else
                sessionID = Guid.Empty;

            if ((flags & MsgFlag.SecurityToken) != 0)
                token = es.ReadBytes16();
            else
                token = null;

            // Read the extended headers

            if ((flags & MsgFlag.ExtensionHeaders) != 0)
            {
                int             cHeaders;
                MsgHeaderID     headerID;
                byte[]          contents;

                cHeaders = es.ReadByte();
                if (cHeaders > 0)
                {
                    extHeaders = new MsgHeaderCollection(cHeaders);
                    for (int i = 0; i < cHeaders; i++)
                    {
                        headerID = (MsgHeaderID)es.ReadByte();
                        contents = es.ReadBytes16();

                        extHeaders.Set(new MsgHeader(headerID, contents));
                    }
                }
            }
        }

        /// <summary>
        /// Serializes the payload of the message into the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        protected virtual void WritePayload(EnhancedStream es)
        {
        }

        /// <summary>
        /// Loads the message payload from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <param name="cbPayload">Number of bytes of payload data.</param>
        protected virtual void ReadPayload(EnhancedStream es, int cbPayload)
        {
        }
    }
}
