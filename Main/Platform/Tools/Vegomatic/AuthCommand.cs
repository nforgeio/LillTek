//-----------------------------------------------------------------------------
// FILE:        AuthCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements LillTek Authentication service related commands

using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Net.Radius;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements LillTek Authentication service related commands.
    /// </summary>
    public static class AuthCommand
    {
        const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic auth keyupdate

Broadcasts a message to all authenticator instances indicating that
a new RSA public key has been assigned to the authentication cluster.

-------------------------------------------------------------------------------
vegomatic auth loadrealmmap

Broadcasts a message to all authentication services commanding to
schedule an immediate reloading of the realm map.

-------------------------------------------------------------------------------
vegomatic auth lock <realm> <account> <lock time>

Broadcasts a message to all authentication service and authenticator
client instances commanding them to lock the account specified by
<realm> and <account> for the duration specified by <lock time>.  This
time can be expressed as the number of seconds or a floating point
value with a units suffix (one of D=days, H=hours, M=minutes,
S=seconds, or MS=milliseconds).

-------------------------------------------------------------------------------
vegomatic auth cacheremove [-realm:<realm>] [-account:<account>] [-all]

Broadcasts a message all authentication service and authenticator
client instances commanding them to remove one or more cached
authentications from their local caches.

If -all is present then all cached credentials for all realms
are removed.

If only -realm is present then all cached credentials for the realm
are removed.

If -realm and -account is present then all cached credentials for
the specific account are removed.

-------------------------------------------------------------------------------
vegomatic auth radius <server>:<port> <secret> <realm>/<account> <password>

Performs a RADUIS authentication using the specified arguments.

";
        /// <summary>
        /// Executes the specified AUTH command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "keyupdate":

                    return KeyUpdate();

                case "loadrealmmap":

                    return LoadRealmMap();

                case "lock":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Lock(args);

                case "cacheremove":

                    if (args.Length < 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return CacheRemove(args);

                case "radius":

                    if (args.Length != 5)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return AuthRadius(args[1], args[2], args[3], args[4]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int KeyUpdate()
        {
            var authenticator = new Authenticator();

            try
            {
                authenticator.Open(Program.Router, new AuthenticatorSettings());
                authenticator.BroadcastKeyUpdate();
            }
            finally
            {
                authenticator.Close();
            }

            return 0;
        }

        private static int LoadRealmMap()
        {
            var authenticator = new Authenticator();

            try
            {
                authenticator.Open(Program.Router, new AuthenticatorSettings());
                authenticator.BroadcastLoadRealmMap();
            }
            finally
            {
                authenticator.Close();
            }

            return 0;
        }

        private static int Lock(string[] args)
        {
            Authenticator   authenticator = new Authenticator();
            string          realm         = args[1];
            string          account       = args[2];
            TimeSpan        lockTime      = Serialize.Parse(args[3], TimeSpan.FromMinutes(5));

            try
            {
                authenticator.Open(Program.Router, new AuthenticatorSettings());
                authenticator.BroadcastAccountLock(realm, account, lockTime);
            }
            finally
            {
                authenticator.Close();
            }

            return 0;
        }

        private static int CacheRemove(string[] args)
        {
            Authenticator   authenticator = new Authenticator();
            string          realm         = null;
            string          account      = null;
            bool            all           = false;

            foreach (string arg in args)
            {
                if (arg == "-all")
                    all = true;
                else if (arg.StartsWith("-realm:"))
                    realm = arg.Substring(7);
                else if (arg.StartsWith("-account:"))
                    account = arg.Substring(9);
            }

            if (realm == null && account == null && !all)
            {
                Program.Error("One of -realm, -account, or -all must be specified.");
                return 1;
            }

            try
            {
                authenticator.Open(Program.Router, new AuthenticatorSettings());

                if (all)
                    authenticator.BroadcastCacheClear();
                else if (account == null)
                    authenticator.BroadcastCacheRemove(realm);
                else
                    authenticator.BroadcastCacheRemove(realm, account);
            }
            finally
            {
                authenticator.Close();
            }

            return 0;
        }

        private static int AuthRadius(string server, string secret, string userid, string password)
        {
            RadiusClient    client = new RadiusClient();
            string          realm;
            string          account;
            int             pos;

            pos = userid.IndexOfAny(new char[] { '/', '\\' });
            if (pos == -1)
            {
                realm   = string.Empty;
                account = userid;
            }
            else
            {
                realm   = userid.Substring(0, pos);
                account = userid.Substring(pos + 1);
            }

            client.Open(new RadiusClientSettings(new NetworkBinding(server), secret));
            try
            {
                Program.Output("Authenticating...");
                if (client.Authenticate(realm, account, password))
                {
                    Program.Output("Success");
                    return 0;
                }
                else
                {
                    Program.Output("Failure");
                    return 1;
                }
            }
            catch (Exception e)
            {
                Program.Error("Error[{0}]: {1}", e.GetType().Name, e.Message);
                return 1;
            }
            finally
            {
                client.Close();
            }
        }
    }
}
