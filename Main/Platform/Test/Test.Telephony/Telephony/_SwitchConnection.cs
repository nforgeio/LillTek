//-----------------------------------------------------------------------------
// FILE:        _SwitchConnection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Common.NUnit
{
    [TestClass]
    public class _SwitchConnection
    {
        // The basic approach to these tests is to simulate a NeonSwitch node
        // and then establish connections to the node and run the SwitchConnection
        // class through its paces.

        private NetworkBinding binding = new NetworkBinding(IPAddress.Loopback, 34112);

        /// <summary>
        /// Simulates the server side of the authentication handshake.
        /// </summary>
        /// <param name="serverConnection">The server side connection.</param>
        /// <param name="failAuth">Pass <c>true</c> to simulate an authentication failure.</param>
        private void AuthHandshake(SwitchConnection serverConnection, bool failAuth)
        {
            ArgCollection properties = new ArgCollection(ArgCollectionType.Unconstrained);
            SwitchPacket packet;

            // Receive and respond to the [auth] command.

            packet = serverConnection.ReceivePacket();
            if (packet.CommandText != string.Format("auth {0}", SwitchConnection.DefaultPassword))
            {
                serverConnection.Close();
                return;
            }

            properties["Content-Type"] = "auth/request";
            serverConnection.SendPacket(new SwitchPacket(properties, null));

            // Receive and respond to the [api status] command.

            packet = serverConnection.ReceivePacket();
            if (packet.CommandText != string.Format("api status", SwitchConnection.DefaultPassword))
            {
                serverConnection.Close();
                return;
            }

            serverConnection.SendReply(failAuth ? "-ERR access denied" : "+OK success");

            properties["Content-Type"] = "api/response";
            serverConnection.SendPacket(new SwitchPacket(properties, Helper.ASCIIEncoding.GetBytes("Hello World!")));

            if (failAuth)
                Thread.Sleep(1000);     // Get the client a chance to see the reply.
        }

        /// <summary>
        /// Sends a simulated event.
        /// </summary>
        /// <param name="serverConnection">The switch connection.</param>
        /// <param name="eventCode">The event code.</param>
        /// <param name="text">The content text.</param>
        /// <param name="args">Additional name/values to be added to the event.</param>
        private void SendEvent(SwitchConnection serverConnection, SwitchEventCode eventCode, string text, params NameValue[] args)
        {
            var properties = new ArgCollection(ArgCollectionType.Unconstrained);
            var sb = new StringBuilder();
            byte[] data = Helper.ASCIIEncoding.GetBytes(text);
            byte[] content;

            properties["Event-Name"] = SwitchHelper.GetEventCodeString(eventCode);
            properties["Content-Type"] = "text";
            properties["Content-Length"] = data.Length.ToString();

            foreach (var pair in args)
                properties[pair.Name] = pair.Value;

            foreach (var key in properties)
                sb.AppendFormat("{0}: {1}\n", key, Helper.UrlEncode(properties[key]));

            sb.Append('\n');

            content = Helper.Concat(Helper.ASCIIEncoding.GetBytes(sb.ToString()), data);

            properties = new ArgCollection(ArgCollectionType.Unconstrained);
            properties["Content-Type"] = "text/event-plain";
            properties["Content-Length"] = content.Length.ToString();

            sb.Clear();

            foreach (var key in properties)
                sb.AppendFormat("{0}: {1}\n", key, Helper.UrlEncode(properties[key]));

            sb.Append('\n');

            serverConnection.Send(Helper.Concat(Helper.ASCIIEncoding.GetBytes(sb.ToString()), content));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_BasicConnect()
        {
            // Simulate the very basic connection sequence:
            //
            //      Connect
            //      Auth command
            //      Server close

            var handlerDone = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;

                        AuthHandshake(serverConnection, false);
                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var disconnect = false;
                var closeError = false;

                connection.Disconnected +=
                    (s, a) =>
                    {
                        disconnect = true;
                        closeError = a.Error != null;
                    };

                connection.Connect();

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));
                Thread.Sleep(1000);         // Give the client connection a sec to process the close

                Assert.IsTrue(disconnect);  // Verify that the connection was closed
                Assert.IsTrue(closeError);  // by the server.
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_FailAuth()
        {
            // Simulate a client side authentication failure.

            var handlerDone = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;

                        AuthHandshake(serverConnection, true);
                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var disconnect = false;
                var closeError = false;
                var securityError = false;

                connection.Disconnected +=
                    (s, a) =>
                    {
                        disconnect = true;
                        closeError = a.Error != null;
                    };

                try
                {
                    connection.Connect();
                }
                catch (SecurityException)
                {
                    securityError = true;
                }

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));
                Helper.WaitFor(() => securityError, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(securityError);
                Assert.IsFalse(disconnect);
                Assert.IsFalse(closeError);

            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_SetLogLevel()
        {
            // Verify the SetLogLevel command.

            var handlerDone = false;
            var gotCommand = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;
                        SwitchPacket packet;

                        AuthHandshake(serverConnection, false);

                        // First command should be: log 5 (notify)

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "log 5" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);
                        serverConnection.SendResponse("log level set");

                        // Second command should be: log 2 (critical)

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "log 2" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);
                        serverConnection.SendResponse("log level set");

                        // Third command should be: nolog

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "nolog" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);
                        serverConnection.SendResponse("logging disabled");

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                SwitchConnection connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                CommandDisposition disposition;

                connection.Connect();
                Assert.AreEqual(SwitchLogLevel.None, connection.LogLevel);

                disposition = connection.SetLogLevel(SwitchLogLevel.Notice);
                Assert.IsTrue(disposition.Success);
                Assert.AreEqual("log level set", disposition.ResponseText);
                Assert.AreEqual(SwitchLogLevel.Notice, connection.LogLevel);

                disposition = connection.SetLogLevel(SwitchLogLevel.Critical);
                Assert.IsTrue(disposition.Success);
                Assert.AreEqual("log level set", disposition.ResponseText);
                Assert.AreEqual(SwitchLogLevel.Critical, connection.LogLevel);

                disposition = connection.SetLogLevel(SwitchLogLevel.None);
                Assert.IsTrue(disposition.Success);
                Assert.AreEqual("logging disabled", disposition.ResponseText);
                Assert.AreEqual(SwitchLogLevel.None, connection.LogLevel);

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(gotCommand);
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_Execute()
        {
            // Verify the Execute command.

            var handlerDone = false;
            var gotCommand = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;
                        SwitchPacket packet;

                        AuthHandshake(serverConnection, false);

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "api status" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);
                        serverConnection.SendResponse("foobar");

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                SwitchConnection connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                CommandDisposition disposition;

                connection.Connect();
                disposition = connection.Execute("status");

                Assert.IsTrue(disposition.Success);
                Assert.AreEqual("foobar", disposition.ResponseText);

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(gotCommand);
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_ExecuteBackground()
        {
            // Verify the ExecuteBackground command.

            var handlerDone = false;
            var gotCommand = false;
            var jobID = Guid.NewGuid();

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;
                        SwitchPacket packet;

                        AuthHandshake(serverConnection, false);

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "bgapi status" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null, new NameValue("Job-UUID", jobID.ToString("D")));
                        SendEvent(serverConnection, SwitchEventCode.BackgroundJob, string.Empty, new NameValue("Job-UUID", jobID.ToString("D")));

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                SwitchConnection connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                bool gotJobCompleted = false;
                CommandDisposition disposition;

                connection.JobCompleted +=
                    (s, a) =>
                    {
                        gotJobCompleted = a.JobID == jobID;
                    };

                connection.Connect();
                disposition = connection.ExecuteBackground("status");

                Assert.IsTrue(disposition.Success);
                Assert.AreEqual(jobID, disposition.JobID);

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));
                Helper.WaitFor(() => gotJobCompleted, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(gotCommand);
                Assert.IsTrue(gotJobCompleted);
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_Subscribe()
        {
            // Verify the Subscribe/Unsubscribe commands.

            var handlerDone = false;
            var gotCommand = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;
                        SwitchPacket packet;

                        AuthHandshake(serverConnection, false);

                        // First command: subscribe heartbeat

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "event plain SWITCH_EVENT_HEARTBEAT" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);

                        // Second command: subscribe dtmf

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "event plain SWITCH_EVENT_DTMF" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);

                        // Third command: unsubscribe dtmf

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "nixevent SWITCH_EVENT_DTMF" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);

                        // Fourth command: unsubscribe null

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "noevents" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);

                        // Fifth command: unsubscribe <empty set>

                        packet = serverConnection.ReceivePacket();
                        gotCommand = packet.PacketType == SwitchPacketType.Command &&
                                     packet.CommandText == "noevents" &&
                                     packet.Headers.Count == 0;

                        serverConnection.SendReply(null);

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                SwitchConnection connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                CommandDisposition disposition;

                connection.Connect();
                Assert.IsTrue(connection.EventSubscriptions.IsEmpty);

                disposition = connection.Subscribe(new SwitchEventCodeSet(SwitchEventCode.Heartbeat));
                Assert.IsTrue(disposition.Success);
                Assert.IsTrue(new SwitchEventCodeSet(SwitchEventCode.Heartbeat) == connection.EventSubscriptions);

                disposition = connection.Subscribe(new SwitchEventCodeSet(SwitchEventCode.Dtmf));
                Assert.IsTrue(disposition.Success);
                Assert.IsTrue(new SwitchEventCodeSet(SwitchEventCode.Heartbeat, SwitchEventCode.Dtmf) == connection.EventSubscriptions);

                disposition = connection.Unsubscribe(new SwitchEventCodeSet(SwitchEventCode.Dtmf));
                Assert.IsTrue(disposition.Success);
                Assert.IsTrue(new SwitchEventCodeSet(SwitchEventCode.Heartbeat) == connection.EventSubscriptions);

                disposition = connection.Unsubscribe(null);
                Assert.IsTrue(disposition.Success);
                Assert.IsTrue(connection.EventSubscriptions.IsEmpty);

                disposition = connection.Unsubscribe(SwitchEventCodeSet.Empty);
                Assert.IsTrue(disposition.Success);
                Assert.IsTrue(connection.EventSubscriptions.IsEmpty);

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(gotCommand);
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_EventReceive()
        {
            // Verify that an event can be received.

            var handlerDone = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;

                        AuthHandshake(serverConnection, false);

                        // Send a fake heartbeat event.

                        SendEvent(serverConnection, SwitchEventCode.Heartbeat, "Hello World!", new NameValue("Foo", "Bar"));

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var disconnect = false;
                var closeError = false;
                var gotEvent = false;

                connection.Disconnected +=
                    (s, a) =>
                    {
                        disconnect = true;
                        closeError = a.Error != null;
                    };

                connection.EventReceived +=
                    (s, a) =>
                    {
                        gotEvent = a.EventCode == SwitchEventCode.Heartbeat &&
                                   a.ContentType == "text" &&
                                   a.ContentText == "Hello World!" &&
                                   a.Properties["Foo"] == "Bar";
                    };

                connection.Connect();

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));
                Helper.WaitFor(() => gotEvent, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(gotEvent);
                Assert.IsTrue(disconnect);  // Verify that the connection was closed
                Assert.IsTrue(closeError);  // by the server.
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_EventSend()
        {
            // Verify that an event can be sent to the switch.

            var handlerDone = false;
            var gotEvent = false;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;

                        serverConnection.CommandReceived +=
                            (s1, a1) =>
                            {
                                gotEvent = a1.CommandText == "sendevent HEARTBEAT" &&
                                           Helper.ASCIIEncoding.GetString(a1.Content) == "Hello World!" &&
                                           a1.Properties["Foo"] == "Bar";
                            };

                        AuthHandshake(serverConnection, false);

                        serverConnection.StartThread();

                        Helper.WaitFor(() => gotEvent, TimeSpan.FromMilliseconds(5000));

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var disconnect = false;
                var closeError = false;

                connection.Disconnected +=
                    (s, a) =>
                    {
                        disconnect = true;
                        closeError = a.Error != null;
                    };

                connection.Connect();

                var properties = new ArgCollection(ArgCollectionType.Unconstrained);

                properties["Foo"] = "Bar";
                connection.SendEvent(SwitchEventCode.Heartbeat, properties, "Hello World!");

                Helper.WaitFor(() => handlerDone, TimeSpan.FromMilliseconds(5000));
                Helper.WaitFor(() => gotEvent, TimeSpan.FromMilliseconds(5000));

                Assert.IsTrue(gotEvent);
                Assert.IsTrue(disconnect);  // Verify that the connection was closed
                Assert.IsTrue(closeError);  // by the server.
            }
            finally
            {
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        //-----------------------------------------------------------------------------------------
        // Load testing

        private const int TransactionCount = 10000;
        private const int SocketBufferSize = 32 * 1024;
        private TimeSpan Timeout           = TimeSpan.FromMilliseconds(120000);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_BlastCommands()
        {
            // Simulate the blasting of commands from the remote machine to the switch.

            var handlerDone = false;
            var cCommandsReceived = 0;
            var cCommandsExecuted = 0;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {
                        SwitchConnection serverConnection = a.Connection;

                        serverConnection.CommandReceived +=
                            (s1, a1) =>
                            {
                                if (a1.CommandText == "sendevent HEARTBEAT" &&
                                    Helper.ASCIIEncoding.GetString(a1.Content) == "Hello World!" &&
                                    a1.Properties["Foo"] == "Bar")
                                {
                                    Interlocked.Increment(ref cCommandsReceived);
                                }
                            };

                        AuthHandshake(serverConnection, false);

                        serverConnection.StartThread();

                        Helper.EnqueueAction(
                            () =>
                            {
                                for (int i = 0; i < TransactionCount; i++)
                                    SendEvent(serverConnection, SwitchEventCode.Heartbeat, "Hello World!", new NameValue("Foo", "Bar"));
                            });

                        Helper.WaitFor(() => cCommandsReceived == TransactionCount && cCommandsExecuted == TransactionCount, Timeout);

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.SendBufferSize =
                SwitchConnection.ReceiveBufferSize = SocketBufferSize;

                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var elapsedTimer = new ElapsedTimer();

                connection.Connect();

                elapsedTimer.Start();

                for (int i = 0; i < TransactionCount; i++)
                {
                    var properties = new ArgCollection(ArgCollectionType.Unconstrained);

                    properties["Foo"] = "Bar";
                    connection.SendEvent(SwitchEventCode.Heartbeat, properties, "Hello World!");
                    Interlocked.Increment(ref cCommandsExecuted);
                }

                Helper.WaitFor(() => handlerDone, Timeout);

                elapsedTimer.Stop();

                var rate = cCommandsReceived / elapsedTimer.ElapsedTime.TotalSeconds;

                Debug.WriteLine(string.Format("Transaction Rate: {0}/sec", rate));
            }
            finally
            {
                SwitchConnection.ResetGlobals();
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_BlastEvents()
        {
            // Simulate the blasting of events from the switch to the remote connection.

            var handlerDone = false;
            var cEventsReceived = 0;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {

                        SwitchConnection serverConnection = a.Connection;

                        AuthHandshake(serverConnection, false);

                        serverConnection.StartThread();

                        Helper.EnqueueAction(
                            () =>
                            {
                                for (int i = 0; i < TransactionCount; i++)
                                    SendEvent(serverConnection, SwitchEventCode.Heartbeat, "Hello World!", new NameValue("Foo", "Bar"));
                            });

                        Helper.WaitFor(() => cEventsReceived == TransactionCount, Timeout);

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.SendBufferSize =
                SwitchConnection.ReceiveBufferSize = SocketBufferSize;

                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var elapsedTimer = new ElapsedTimer();

                connection.EventReceived +=
                    (s, a) =>
                    {
                        if (a.EventCode == SwitchEventCode.Heartbeat &&
                            a.ContentType == "text" &&
                            a.ContentText == "Hello World!")
                        {
                            Interlocked.Increment(ref cEventsReceived);
                        }
                    };

                connection.Connect();

                elapsedTimer.Start();
                Helper.WaitFor(() => handlerDone, Timeout);
                elapsedTimer.Stop();

                var rate = cEventsReceived / elapsedTimer.ElapsedTime.TotalSeconds;

                Debug.WriteLine(string.Format("Transaction Rate: {0}/sec", rate));
            }
            finally
            {
                SwitchConnection.ResetGlobals();
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchConnection_BlastBoth()
        {
            // Simulate the blasting of events from the switch to the remote connection
            // and commands from the remote machine to the switch.

            var handlerDone = false;
            var cCommandsReceived = 0;
            var cCommandsExecuted = 0;
            var cEventsReceived = 0;

            var connectHandler = new EventHandler<SwitchInboundConnectionArgs>(
                (s, a) =>
                {
                    a.StartConnectionThread = false;

                    Helper.EnqueueAction(() =>
                    {

                        SwitchConnection serverConnection = a.Connection;

                        serverConnection.CommandReceived +=
                            (s1, a1) =>
                            {
                                if (a1.CommandText == "sendevent HEARTBEAT" &&
                                    Helper.ASCIIEncoding.GetString(a1.Content) == "Hello World!" &&
                                    a1.Properties["Foo"] == "Bar")
                                {
                                    Interlocked.Increment(ref cCommandsReceived);
                                }
                            };

                        AuthHandshake(serverConnection, false);

                        serverConnection.StartThread();

                        Helper.EnqueueAction(
                            () =>
                            {
                                for (int i = 0; i < TransactionCount; i++)
                                    SendEvent(serverConnection, SwitchEventCode.Heartbeat, "Hello World!", new NameValue("Foo", "Bar"));
                            });

                        Helper.WaitFor(() => cCommandsReceived == TransactionCount && cCommandsExecuted == TransactionCount && cEventsReceived == TransactionCount, Timeout);

                        serverConnection.Close();
                        handlerDone = true;
                    });
                });

            SwitchConnection.InboundConnection += connectHandler;

            try
            {
                SwitchConnection.SendBufferSize =
                SwitchConnection.ReceiveBufferSize = SocketBufferSize;

                SwitchConnection.StartListener(binding, 10);

                var connection = new SwitchConnection(binding, SwitchConnection.DefaultPassword);
                var elapsedTimer = new ElapsedTimer();

                connection.EventReceived +=
                    (s, a) =>
                    {
                        if (a.EventCode == SwitchEventCode.Heartbeat &&
                            a.ContentType == "text" &&
                            a.ContentText == "Hello World!")
                        {
                            Interlocked.Increment(ref cEventsReceived);
                        }
                    };

                connection.Connect();

                elapsedTimer.Start();

                for (int i = 0; i < TransactionCount; i++)
                {
                    var properties = new ArgCollection(ArgCollectionType.Unconstrained);

                    properties["Foo"] = "Bar";
                    connection.SendEvent(SwitchEventCode.Heartbeat, properties, "Hello World!");
                    Interlocked.Increment(ref cCommandsExecuted);
                }

                Helper.WaitFor(() => handlerDone, Timeout);

                elapsedTimer.Stop();

                var rate = (cCommandsReceived + cEventsReceived) / elapsedTimer.ElapsedTime.TotalSeconds;

                Debug.WriteLine(string.Format("Transaction Rate: {0}/sec", rate));
            }
            finally
            {
                SwitchConnection.ResetGlobals();
                SwitchConnection.InboundConnection -= connectHandler;
                SwitchConnection.StopListener();
            }
        }
    }
}

