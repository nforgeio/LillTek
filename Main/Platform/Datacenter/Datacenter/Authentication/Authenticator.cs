//-----------------------------------------------------------------------------
// FILE:        Authenticator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side interface to an AuthServiceHandler.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter.Msgs.AuthService;
using LillTek.Messaging;

// $todo(jeff.lill):
//
// The protocol currently implemented is not immune from man-in-the-middle
// attacks.  This needs to be revisited at some point.

// $todo(jeff.lill): This class should probably be extended to expose some performance counters.

namespace LillTek.Datacenter
{
    /// <summary>
    /// Implements the client side interface to an AuthServiceHandler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Start off by constructing an instance, and then calling <see cref="Open(MsgRouter,string)" />
    /// to intialize the instance and then <see cref="Authenticate" /> or <see cref="BeginAuthenticate" />
    /// and <see cref="EndAuthenticate" /> to validate account credentials.  The class is capable 
    /// of caching results for performance.  The <see cref="ClearCache" /> method flushes
    /// the cache.  <see cref="Close" /> should be called when the authenticator is no longer
    /// needed to free system resources.
    /// </para>
    /// <para>
    /// The <see cref="BroadcastCacheClear()" />, <see cref="BroadcastCacheRemove(string)" />, and
    /// <see cref="BroadcastCacheRemove(string,string)" /> methods will broadcast messages to
    /// all authenticator instances on the network, commanding them to remove one or more
    /// items from their caches.  <see cref="BroadcastKeyUpdate" /> broadcast a message to
    /// all authenticator instances indicating that a new global authentication public key
    /// is available and that each instance should retrieve the new key before performing
    /// the next authentication.  The <b>broadcast</b> methods are designed to be used by
    /// administrator tools and possibly by authentication servers to help ensure that 
    /// the distributed authentication caches are coherent if problems crop up.
    /// </para>
    /// <para>
    /// By default, the authentication servers will be addressed via the <b>abstract://LillTek/DataCenter/Auth/Server</b>
    /// endpoint and authentication broadcasts will be directed to <b>abstract://LillTek/DataCenter/Auth/Client</b>.
    /// These endpoints can be reconfigured by editing the application router's <b>AbstractMap</b>
    /// configuration setting.
    /// </para>
    /// <para>
    /// The class is thread-safe and is designed to be used globally by an application.
    /// Performance will be very poor if instances are created for each authentication
    /// since there will be no caching benefit and there will also be the overhead
    /// of obtaining the authentication service's public key for each authentication.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// The <see cref="Open(MsgRouter,string)" /> method is passed an application configuration key
    /// prefix for the class' configuration settings.  See <see cref="AuthenticatorSettings.LoadConfig" />
    /// for a description of the configuration settings loaded.
    /// </para>
    /// <para>
    /// Alternatively, the <see cref="Open(MsgRouter,AuthenticatorSettings)" /> method may be
    /// used to open and authenticator.
    /// </para>
    /// <para><b><u>Authentication Protocol</u></b></para>
    /// <para>
    /// The protocol currently implemented is moderately secure in that it encrypts
    /// transmitted credentials as well as digitally signs responses.  The protocol
    /// is subject to man-in-the-middle attacks though and probably should not be
    /// used for authenticating over an open channel over a public network.
    /// </para>
    /// <para>
    /// The protocol works as follows.
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     The client sends a <see cref="GetPublicKeyMsg" /> message to 
    ///     one of the authentication service instances listrening on <see cref="AbstractAuthServerEP" /> 
    ///     when the client is first opened.
    ///     </item>
    ///     <item>
    ///     The receiving Authentication service instances respond by sending
    ///     a <see cref="GetPublicKeyAck" /> message back to the client.  The
    ///     response holds the RSA public key used by all Authentication service
    ///     instances as well as the IP address of the service instance.
    ///     </item>
    ///     <item>
    ///     The client broadcasts an <see cref="AuthServerIDMsg" /> to <see cref="AbstractAuthServerEP" />.
    ///     This message includes the public key received as well as the IP address.
    ///     Receiving authentication service instances will verify that the public
    ///     key is valid and log a potential security breach if the key is not valid.
    ///     </item>
    ///     <item>
    ///     This completes the client initialization.  Note that the protocol does not
    ///     currently provide a mechanism for indicating to the client that legitimate
    ///     authentication service instances have detected a security breach.
    ///     </item>
    ///     <item>
    ///     Credential authentication will start with the client sending an
    ///     <see cref="AuthMsg" /> to <see cref="AbstractAuthServerEP" />.  The message
    ///     payload is the credentials encrypted with a combination of the
    ///     server's public key and a one-time symmetric key (see <see cref="SecureData" />.
    ///     The message also includes the client's public RSA key.
    ///     </item>
    ///     <item>
    ///     The receiving authentication service instance decrypts the credentials,
    ///     performs the authentication, and then responds with a <see cref="AuthAck" />
    ///     holding the result encrypted with the client's public key and a one-time
    ///     symmetric key.
    ///     </item>
    ///     <item>
    ///     The client receives and decodes the <see cref="AuthAck" /> message and
    ///     returns the result to the application.
    ///     </item>
    ///     <item>
    ///     A <see cref="AuthControlMsg" /> may be broadcasted to authentication
    ///     clients and servers via the <see cref="AbstractAuthEP" /> endpoint.  This message 
    ///     provides a global mechanism for proactively removing credentials from distributed 
    ///     authentication caches as well as other authentication control functions.  The supported 
    ///     commands are <b>cache-clear</b>, <b>cache-remove-account</b>, and <b>cache-remove-realm</b>
    ///     using the <see cref="BroadcastCacheClear()" />, <see cref="BroadcastCacheRemove(string)" />,
    ///     <see cref="BroadcastCacheRemove(string,string)" />, and <see cref="BroadcastAccountLock" />
    ///     methods.
    ///     </item>
    ///     <item>
    ///     When clients fail an authentication against cached credentials then the 
    ///     client will broadcast an <see cref="AuthControlMsg" /><b>(command=auth-failed)</b>
    ///     message to the authentication services at the <see cref="AbstractAuthServerEP" /> endpoint,
    ///     passing the realm and account as well as the cached authentication status.  The
    ///     authentication service instances will use this to update their account lockout
    ///     counts. 
    ///     </item>
    ///     <item>
    ///     Authentication service instances also broadcast <see cref="AuthControlMsg" /><b>(command=auth-failed)</b>
    ///     messages amongst themselves to whenever they fail an authentication for any reason except
    ///     for a locked account so that the other servers can increment their lock counts.
    ///     Service instances also broadcast an <see cref="AuthControlMsg" /><b>(command=lock-account)</b>
    ///     message to the other instances whenever an account is locked so the other instances
    ///     can mark it as locked as well.
    ///     </item>
    ///     <item>
    ///     When an account is finally unlocked by an authentication service instance, an
    ///     <see cref="AuthControlMsg" /><b>(command=cache-remove-account)</b> message will
    ///     be broadcast to all authentication service and client endpoints ensuring the
    ///     the account will be unlocked globally.
    ///     </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class Authenticator : ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// The possible authentication operation states.
        /// </summary>
        private enum AuthOpState
        {
            Unknown,
            GetPublicKey,
            AuthPending
        }

        /// <summary>
        /// Used for tracking the state of an authentication operation.
        /// </summary>
        private sealed class AuthAsyncResult : AsyncResult
        {
            /// <summary>
            /// The operation state.
            /// </summary>
            public AuthOpState OpState = AuthOpState.Unknown;

            /// <summary>
            /// The symmetric crypto algorithm arguments used to communicate
            /// with the authentication service (used for encrypting the
            /// credentials and decrypting the result).
            /// </summary>
            public SymmetricKey SymmetricKey = null;

            /// <summary>
            /// The authentication result.
            /// </summary>
            public new AuthenticationResult Result = null;

            /// <summary>
            /// The authentication realm.
            /// </summary>
            public string Realm;

            /// <summary>
            /// The account.
            /// </summary>
            public string Account;

            /// <summary>
            /// The password.
            /// </summary>
            public string Password;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="owner">The object that owns the operation (or <c>null</c>).</param>
            /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
            /// <param name="state">Application defined state (or <c>null</c>).</param>
            public AuthAsyncResult(object owner, AsyncCallback callback, object state)
                : base(owner, callback, state)
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation 

        /// <summary>
        /// The default abstract endpoint of the LillTek.Datacenter.Server.AuthServiceHandler class.
        /// </summary>
        public const string AbstractAuthServerEP = "abstract://LillTek/DataCenter/Auth/Server";

        /// <summary>
        /// The default abstract endpoint for authentication client instances to be used for
        /// broadcasting messages (such as for cache management) to all clients.
        /// </summary>
        public const string AbstractAuthClientEP = "abstract://LillTek/DataCenter/Auth/Client";

        /// <summary>
        /// The default abstract endpoint to be used for broadcasting to both authentication
        /// clients and servers.
        /// </summary>
        public const string AbstractAuthEP = "abstract://LillTek/DataCenter/Auth/*";

        private const string NotOpenMsg = "Authenticator is not open.";

        private bool                                        isOpen;             // True if the instance is open
        private GatedTimer                                  bkTimer;            // Background task timer
        private MsgRouter                                   router;             // Associated message router
        private TimeSpan                                    successTTL;         // Time to cache successful authentications
        private TimeSpan                                    failTTL;            // Time to cache failed authentications
        private TimeSpan                                    cacheFlushInterval; // Time between cache flushes
        private DateTime                                    cacheFlushTime;     // Scheduled time of the next cache flush (SYS)
        private string                                      publicKey;          // Authentication service public RSA key (or null)
        private TimedLRUCache<string, AuthenticationResult> cache;              // Cached authentications
        private AsyncCallback                               onGetKey;           // Delegate called for received public keys
        private AsyncCallback                               onAuth;             // Delegate called for received auth results

        // Implementation Note
        // -------------------
        // The cache above is keyed by the credentials formatted as: 
        //
        //          <realm> TAB <account> TAB <password>
        //
        // where the realm and account are converted to uppercase.
        //
        // Note that publicKey will be initialized to null.  The first authentication
        // attempt will send a GetPublicKeyMsg to the authentication service
        // endpoint, requesting the service's public key.  The key returned will
        // be stored and the current and subsequent authentication attempts will
        // use the key to encrypt credentials sent to the authentication service.

        /// <summary>
        /// Constructor.
        /// </summary>
        public Authenticator()
        {
            Global.RegisterMsgTypes();

            this.isOpen    = false;
            this.bkTimer   = null;
            this.router    = null;
            this.publicKey = null;
            this.onGetKey  = new AsyncCallback(OnGetKey);
            this.onAuth    = new AsyncCallback(OnAuth);
        }

        /// <summary>
        /// Opens the authenticator using the settings passed.
        /// </summary>
        /// <param name="router">The router to be used for messaging.</param>
        /// <param name="settings">The <see cref="AuthenticatorSettings" /> instance to use.</param>
        public void Open(MsgRouter router, AuthenticatorSettings settings)
        {
            using (TimedLock.Lock(this))
            {
                if (isOpen)
                    throw new AuthenticationException("Authenticator is already open.");

                // Load the configuration settings

                cacheFlushInterval = settings.CacheFlushInterval;
                successTTL         = settings.SuccessTTL;
                failTTL            = settings.FailTTL;

                // Crank this sucker up.

                if (settings.MaxCacheSize > 0)
                {
                    cache          = new TimedLRUCache<string, AuthenticationResult>();
                    cache.MaxItems = settings.MaxCacheSize;
                }
                else
                    cache = null;

                this.router         = router;
                this.bkTimer        = new GatedTimer(new TimerCallback(OnBkTimer), null, settings.BkTaskInterval);
                this.isOpen         = true;
                this.cacheFlushTime = SysTime.Now + cacheFlushInterval;

                router.Dispatcher.AddTarget(this);
            }
        }

        /// <summary>
        /// Opens the authenticator instance using settings loaded from the 
        /// application configuration.
        /// </summary>
        /// <param name="router">The router to be used for messaging.</param>
        /// <param name="keyPrefix">Application configuration key prefix for the instance settings.</param>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is already open.</exception>
        /// <remarks>
        /// See <see cref="AuthenticatorSettings" /> for a description of the configuration
        /// settings loaded.
        /// </remarks>
        public void Open(MsgRouter router, string keyPrefix)
        {
            Open(router, AuthenticatorSettings.LoadConfig(keyPrefix));
        }

        /// <summary>
        /// Closes the authenticator instance if it's open.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (router != null)
                {
                    router.Dispatcher.RemoveTarget(this);
                    router = null;
                }

                cache     = null;
                publicKey = null;
                isOpen    = false;
            }
        }

        /// <summary>
        /// Generates a cache key from a set of credentials.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>The key.</returns>
        private static string GetCacheKey(string realm, string account, string password)
        {
            return string.Format("{0}\t{1}\t{2}", realm.ToUpper(), account.ToUpper(), password);
        }

        /// <summary>
        /// Returns the cached <see cref="AuthenticationResult" /> for a set of credentials.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>The cached authentication result or <c>null</c>.</returns>
        public AuthenticationResult GetCachedResult(string realm, string account, string password)
        {
            using (TimedLock.Lock(this))
            {
                AuthenticationResult result;

                if (cache != null && cache.TryGetValue(GetCacheKey(realm, account, password), out result))
                    return result;
                else
                    return null;
            }
        }

        /// <summary>
        /// Initiates a synchronous account credential authentication.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>An <see cref="AuthenticationResult" /> instance detailing the result of the operation.</returns>
        /// <exception cref="System.TimeoutException">Thrown if the operation timed out.</exception>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public AuthenticationResult Authenticate(string realm, string account, string password)
        {
            var ar = BeginAuthenticate(realm, account, password, null, null);

            return EndAuthenticate(ar);
        }

        /// <summary>
        /// Initiates an asynchronous account credential authentication.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>An <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// Successful calls to this method must eventually be followed by a call
        /// to <see cref="EndAuthenticate" />.
        /// </note>
        /// </remarks>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public IAsyncResult BeginAuthenticate(string realm, string account, string password, AsyncCallback callback, object state)
        {
            string                  cacheKey = GetCacheKey(realm, account, password);
            AuthAsyncResult         arAuth;
            AuthenticationResult    result;

            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                arAuth          = new AuthAsyncResult(this, callback, state);
                arAuth.Realm    = realm;
                arAuth.Account  = account;
                arAuth.Password = password;

                arAuth.Started();

                // First check to see if the answer is already cached

                if (cache != null && cache.TryGetValue(cacheKey, out result))
                {
                    arAuth.Result = result;
                    arAuth.Notify();

                    if (result.Status != AuthenticationStatus.Authenticated &&
                        result.Status != AuthenticationStatus.AccountLocked)
                    {
                        // If the authentication failed and the account is not locked
                        // then broadcast message to the authentication services so that
                        // they can increment their fail counts.

                        router.BroadcastTo(AbstractAuthServerEP, new AuthControlMsg("auth-failed", string.Format("realm={0};account={1};status={2}", realm, account, result.Status)));
                    }

                    return arAuth;
                }

                // If we don't have the authentication service's public key yet then
                // initiate an async query to get it.

                if (publicKey == null)
                {
                    arAuth.OpState = AuthOpState.GetPublicKey;
                    router.BeginQuery(AbstractAuthServerEP, new GetPublicKeyMsg(), onGetKey, arAuth);
                    return arAuth;
                }

                // Initiate an async authentication query, encrypting the credentials
                // using the authentication service's public key.

                AuthMsg authMsg;

                arAuth.OpState = AuthOpState.AuthPending;
                authMsg        = new AuthMsg(AuthMsg.EncryptCredentials(publicKey, realm, account, password, out arAuth.SymmetricKey));

                router.BeginQuery(AbstractAuthServerEP, authMsg, onAuth, arAuth);

                return arAuth;
            }
        }

        /// <summary>
        /// Handles the async reception of the public key from the authentication service.
        /// </summary>
        /// <param name="ar">The async result instance.</param>
        private void OnGetKey(IAsyncResult ar)
        {
            AuthAsyncResult     arAuth = (AuthAsyncResult)ar.AsyncState;
            GetPublicKeyAck     ack;
            AuthMsg             authMsg;

            using (TimedLock.Lock(this))
            {
                try
                {
                    if (!isOpen)
                        throw new AuthenticationException(NotOpenMsg);

                    ack       = (GetPublicKeyAck)router.EndQuery(ar);
                    publicKey = ack.PublicKey;

                    // Now that we have the public key, we're going to broadcast
                    // it to the authentication service instances so that they can
                    // perform a man-in-the-middle security check.

                    router.BroadcastTo(AbstractAuthServerEP, new AuthServerIDMsg(ack.PublicKey, ack.MachineName, ack.Address));

                    // Now initiate the authentication query.

                    arAuth.OpState = AuthOpState.AuthPending;
                    authMsg        = new AuthMsg(AuthMsg.EncryptCredentials(publicKey, arAuth.Realm, arAuth.Account, arAuth.Password, out arAuth.SymmetricKey));

                    router.BeginQuery(AbstractAuthServerEP, authMsg, onAuth, arAuth);
                }
                catch (System.TimeoutException)
                {
                    arAuth.Notify(new AuthenticationException("No authentication service instances responded."));
                }
                catch (Exception e)
                {
                    arAuth.Notify(e);
                }
            }
        }

        /// <summary>
        /// Handles the async reception of authentication responses.
        /// </summary>
        /// <param name="ar">The async result instance.</param>
        private void OnAuth(IAsyncResult ar)
        {
            var         arAuth = (AuthAsyncResult)ar.AsyncState;
            AuthAck     ack;

            Assertion.Test(arAuth.OpState == AuthOpState.AuthPending);

            using (TimedLock.Lock(this))
            {
                try
                {
                    if (!isOpen)
                        throw new AuthenticationException(NotOpenMsg);

                    ack           = (AuthAck)router.EndQuery(ar);
                    arAuth.Result = AuthAck.DecryptResult(arAuth.SymmetricKey, ack.EncryptedResult);

                    arAuth.Notify();

                    // Cache the result

                    if (cache != null)
                    {
                        if (arAuth.Result.Status == AuthenticationStatus.Authenticated)
                        {
                            if (successTTL > TimeSpan.Zero)
                                cache.Add(GetCacheKey(arAuth.Realm, arAuth.Account, arAuth.Password), arAuth.Result, successTTL);
                        }
                        else
                        {
                            if (failTTL > TimeSpan.Zero)
                                cache.Add(GetCacheKey(arAuth.Realm, arAuth.Account, arAuth.Password), arAuth.Result, failTTL);
                        }
                    }
                }
                catch (Exception e)
                {
                    arAuth.Notify(e);
                }
            }
        }

        /// <summary>
        /// Completes an asynchronous authentication initiated by a call to
        /// <see cref="BeginAuthenticate" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginAuthenticate" />.</param>
        /// <returns>An <see cref="AuthenticationResult" /> instance detailing the result of the operation.</returns>
        /// <exception cref="System.TimeoutException">Thrown if the operation timed out.</exception>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public AuthenticationResult EndAuthenticate(IAsyncResult ar)
        {
            var arAuth = (AuthAsyncResult)ar;

            arAuth.Wait();
            try
            {
                if (arAuth.Exception != null)
                    throw arAuth.Exception;

                return arAuth.Result;
            }
            finally
            {

                arAuth.Dispose();
            }
        }

        /// <summary>
        /// Clears all cached authentication information.
        /// </summary>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public void ClearCache()
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                if (cache != null)
                    cache.Clear();
            }
        }

        /// <summary>
        /// Broadcasts a message that commands all authentication client and service instances on the network
        /// to clear their caches.
        /// </summary>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public void BroadcastCacheClear()
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                router.BroadcastTo(AbstractAuthEP, new AuthControlMsg("cache-clear", null));
            }
        }

        /// <summary>
        /// Broadcasts a message that commands all authentication client and service instances on the network
        /// to remove all cached entries for a specified authentication realm.
        /// </summary>
        /// <param name="realm">The realm to be cleared.</param>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public void BroadcastCacheRemove(string realm)
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                router.BroadcastTo(AbstractAuthEP, new AuthControlMsg("cache-remove-realm", string.Format("realm={0}", realm)));
            }
        }

        /// <summary>
        /// Broadcasts a message that commands all authentication client and service instances on the network
        /// to remove all cached entries for a specific realm and account.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account.</param>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public void BroadcastCacheRemove(string realm, string account)
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                router.BroadcastTo(AbstractAuthEP, new AuthControlMsg("cache-remove-account", string.Format("realm={0};account={1}", realm, account)));
            }
        }

        /// <summary>
        /// Broadcasts a message that commands all authentication client and service instances on the network
        /// to request a new authentication service public key before the next authentication
        /// attempt.
        /// </summary>
        /// <exception cref="AuthenticationException">Thrown if the authenticator is not open.</exception>
        public void BroadcastKeyUpdate()
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                router.BroadcastTo(AbstractAuthEP, new AuthControlMsg("auth-key-update", null));
            }
        }

        /// <summary>
        /// Broadcasts a message that commands all authentication client and service instances on the network
        /// to lock an account.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="lockTTL">The lock duration.</param>
        public void BroadcastAccountLock(string realm, string account, TimeSpan lockTTL)
        {
            AuthControlMsg msg;

            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                msg = new AuthControlMsg("lock-account", string.Format("realm={0};account={1};source-id={2};lock-ttl={3}",
                                                                      realm, account, Helper.NewGuid(), Serialize.ToString(lockTTL)));
                router.BroadcastTo(AbstractAuthEP, msg);
            }
        }

        /// <summary>
        /// Broadcasts a message that commands all authentication client and service instances on the network
        /// to reload their realm maps.
        /// </summary>
        public void BroadcastLoadRealmMap()
        {
            AuthControlMsg msg;

            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    throw new AuthenticationException(NotOpenMsg);

                msg = new AuthControlMsg("load-realm-map", null);
                router.BroadcastTo(AbstractAuthEP, msg);
            }
        }

        /// <summary>
        /// Called periodically on a background thread to handle background
        /// tasks such as flushing the cache.
        /// </summary>
        /// <param name="o">Not used.</param>
        private void OnBkTimer(object o)
        {
            using (TimedLock.Lock(this))
            {

                if (!isOpen)
                    return;

                if (cache != null && cacheFlushTime <= SysTime.Now)
                {
                    cache.Flush();
                    cacheFlushTime = SysTime.Now + cacheFlushInterval;
                }
            }
        }

        //---------------------------------------------------------------------
        // Message handlers

        /// <summary>
        /// Removes entries from the cache whose key begin with the pattern
        /// passed.  This is highly dependent on the fact that cache keys
        /// are formatted as REALM\tACCOUNT\tPASSWORD.
        /// </summary>
        /// <param name="pattern">The key pattern.</param>
        private void CacheRemove(string pattern)
        {
            var delKeys = new List<string>();

            pattern = pattern.ToUpper();

            foreach (string key in cache.Keys)
                if (key.StartsWith(pattern))
                    delKeys.Add(key);

            for (int i = 0; i < delKeys.Count; i++)
                cache.Remove(delKeys[i]);
        }

        /// <summary>
        /// Handles cache control messages.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler(LogicalEP = AbstractAuthClientEP)]
        public void OnMsg(AuthControlMsg msg)
        {
            try
            {
                using (TimedLock.Lock(this))
                {
                    if (!isOpen)
                        return;

                    switch (msg.Command)
                    {
                        case "auth-key-update":

                            publicKey = null;       // This will force the retrieval of a new key
                            break;                  // on the next auth request

                        case "cache-clear":

                            if (cache != null)
                                cache.Clear();

                            break;

                        case "cache-remove-realm":

                            if (cache != null)
                                CacheRemove(msg.Get("realm", string.Empty) + "\t");

                            break;

                        case "lock-account":
                        case "cache-remove-account":

                            if (cache != null)
                                CacheRemove(msg.Get("realm", string.Empty) + "\t" + msg.Get("account", string.Empty) + "\t");

                            break;

                        default:

                            SysLog.LogWarning("Unexpected authentication control command [{0}].", msg.Command);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
