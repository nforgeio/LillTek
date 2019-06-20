//-----------------------------------------------------------------------------
// FILE:        _Config.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests for the Config class.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _FlightRecorder
    {
        //---------------------------------------------------------------------
        // Persist mode tests

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_Basic()
        {
            // Verify that the recorder can persist a couple of events
            // and then dequeue them.

            MemoryStream stream;
            byte[] serialized;

            stream = new MemoryStream();

            using (var recorder = new FlightRecorder(stream))
            {
                Assert.IsTrue(recorder.IsPersistMode);
                Assert.IsFalse(recorder.IsPassThruMode);

                recorder.Log("event #1");
                recorder.Log("event #2");

                serialized = stream.ToArray();
            }

            stream = new MemoryStream(serialized);

            using (var recorder = new FlightRecorder(stream))
            {
                Assert.AreEqual(2, recorder.Count);
                Assert.AreEqual("event #1", recorder.Dequeue().Operation);
                Assert.AreEqual(1, recorder.Count);
                Assert.AreEqual("event #2", recorder.Dequeue().Operation);
                Assert.AreEqual(0, recorder.Count);

                Assert.IsNull(recorder.Dequeue());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_Multiple()
        {
            // Verify that a second flight recorder trying to use the same
            // backing file will fail-over to using a memory stream.

            string path = Path.GetTempFileName();

            try
            {
                using (var recorder1 = new FlightRecorder(path))
                {
                    Assert.IsTrue(recorder1.IsPersistMode);
                    Assert.IsFalse(recorder1.IsPassThruMode);

                    using (var recorder2 = new FlightRecorder(path))
                    {
                        // Perform some operations to the second recorder.  These
                        // operations will be NOPs since the recorder is blocked,
                        // but should not fail or impact the first recorder's
                        // backing file.

                        recorder2.Log("Test");
                        recorder2.Dequeue();
                        recorder2.Dequeue(2);
                        recorder2.GetEvents();

                        // Record some events to the first recorder.

                        recorder1.Log("event #1");
                        recorder1.Log("event #2");
                    }
                }

                // Verify that the first instance did record properly.

                using (var recorder = new FlightRecorder(path))
                {
                    Assert.AreEqual(2, recorder.Count);
                    Assert.AreEqual("event #1", recorder.Dequeue().Operation);
                    Assert.AreEqual(1, recorder.Count);
                    Assert.AreEqual("event #2", recorder.Dequeue().Operation);
                    Assert.AreEqual(0, recorder.Count);

                    Assert.IsNull(recorder.Dequeue());
                }
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_EventFields()
        {
            // Verify that all event fields are persisted properly.

            MemoryStream stream;
            byte[] serialized;
            string organizationID = Guid.NewGuid().ToString();
            string userID = Guid.NewGuid().ToString();
            string sessionID = Guid.NewGuid().ToString();
            FlightEvent flightEvent;

            stream = new MemoryStream();

            using (var recorder = new FlightRecorder(stream))
            {
                recorder.OrganizationID = null;
                recorder.UserID = null;
                recorder.SessionID = null;
                recorder.Source = null;
                recorder.SourceVersion = null;

                recorder.Log("event #1");

                recorder.OrganizationID = organizationID;
                recorder.UserID = userID;
                recorder.SessionID = sessionID;
                recorder.Source = "test.app";
                recorder.SourceVersion = new Version("1.2.3.4");

                recorder.Log("event #2", "details", true);

                serialized = stream.ToArray();
            }

            stream = new MemoryStream(serialized);

            using (var recorder = new FlightRecorder(stream))
            {
                flightEvent = recorder.Dequeue();
                Assert.IsNull(flightEvent.OrganizationID);
                Assert.IsNull(flightEvent.UserID);
                Assert.IsNull(flightEvent.SessionID);
                Assert.IsNull(flightEvent.Source);
                Assert.IsNull(flightEvent.SourceVersion);
                Assert.AreEqual("event #1", flightEvent.Operation);
                Assert.IsNull(flightEvent.Details);
                Assert.IsFalse(flightEvent.IsError);

                flightEvent = recorder.Dequeue();
                Assert.AreEqual(organizationID, flightEvent.OrganizationID);
                Assert.AreEqual(userID, flightEvent.UserID);
                Assert.AreEqual(sessionID, flightEvent.SessionID);
                Assert.AreEqual("test.app", flightEvent.Source);
                Assert.AreEqual(new Version("1.2.3.4"), flightEvent.SourceVersion);
                Assert.AreEqual("event #2", flightEvent.Operation);
                Assert.AreEqual("details", flightEvent.Details);
                Assert.IsTrue(flightEvent.IsError);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_MaxEvents()
        {
            // Verify that the record honors the MaxEvents property.

            MemoryStream stream = new MemoryStream();
            FlightEvent flightEvent;

            using (var recorder = new FlightRecorder(stream))
            {
                recorder.MaxEvents = 10;

                for (int i = 0; i < 20; i++)
                    recorder.Log(i.ToString());

                Assert.AreEqual(10, recorder.Count);

                flightEvent = recorder.Dequeue();
                Assert.AreEqual("10", flightEvent.Operation);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_Clear()
        {
            // Verify that Clear() works.

            MemoryStream stream = new MemoryStream();

            using (var recorder = new FlightRecorder(stream))
            {
                for (int i = 0; i < 20; i++)
                    recorder.Log(i.ToString());

                Assert.AreEqual(20, recorder.Count);
                recorder.Clear();
                Assert.AreEqual(0, recorder.Count);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_GetEvents()
        {
            // Verify that GetEvents() works

            MemoryStream stream = new MemoryStream();
            List<FlightEvent> events;

            using (var recorder = new FlightRecorder(stream))
            {
                for (int i = 0; i < 20; i++)
                    recorder.Log(i.ToString());

                events = new List<FlightEvent>();
                recorder.GetEvents().CopyTo(events);
            }

            for (int i = 0; i < 20; i++)
                Assert.AreEqual(i.ToString(), events[i].Operation);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_SysLogProvider()
        {
            // Verify that the ISysLogProvider implementation works.

            ISysLogProvider orgProvider = SysLog.LogProvider;

            try
            {
                MemoryStream stream = new MemoryStream();
                FlightEvent flightEvent;

                using (var recorder = new FlightRecorder(stream))
                {
                    SysLog.LogProvider = recorder;

                    SysLog.LogError("Test Error");
                    SysLog.LogWarning("Test Warning");
                    SysLog.LogInformation("Test Information");
                    SysLog.Flush();

                    Assert.AreEqual(3, recorder.Count);

                    flightEvent = recorder.Dequeue();
                    Assert.AreEqual("SysLog:Error", flightEvent.Operation);
                    Assert.IsTrue(flightEvent.Details.Contains("Test Error"));
                    Assert.IsTrue(flightEvent.IsError);

                    flightEvent = recorder.Dequeue();
                    Assert.AreEqual("SysLog:Warning", flightEvent.Operation);
                    Assert.IsTrue(flightEvent.Details.Contains("Test Warning"));
                    Assert.IsFalse(flightEvent.IsError);

                    flightEvent = recorder.Dequeue();
                    Assert.AreEqual("SysLog:Information", flightEvent.Operation);
                    Assert.IsTrue(flightEvent.Details.Contains("Test Information"));
                    Assert.IsFalse(flightEvent.IsError);

                    // Verify that system events actually serialize exception
                    // and stack trace related information.

                    try
                    {
                        throw new AssertException();
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                        SysLog.Flush();

                        flightEvent = recorder.Dequeue();
                        Assert.AreEqual("SysLog:Exception", flightEvent.Operation);
                        Assert.IsTrue(flightEvent.Details.Contains("AssertException"));
                    }
                }
            }
            finally
            {
                SysLog.LogProvider = orgProvider;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_Persist_Disabled()
        {
            // Verify that events are not logged when the recorder is disabled.

            MemoryStream stream;
            byte[] serialized;

            stream = new MemoryStream();

            using (var recorder = new FlightRecorder(stream))
            {
                Assert.IsTrue(recorder.IsPersistMode);
                Assert.IsFalse(recorder.IsPassThruMode);

                recorder.IsEnabled = false;
                recorder.Log("event XX");
                recorder.IsEnabled = true;

                recorder.Log("event #1");
                recorder.Log("event #2");

                serialized = stream.ToArray();
            }

            stream = new MemoryStream(serialized);

            using (var recorder = new FlightRecorder(stream))
            {
                Assert.AreEqual(2, recorder.Count);
                Assert.AreEqual("event #1", recorder.Dequeue().Operation);
                Assert.AreEqual(1, recorder.Count);
                Assert.AreEqual("event #2", recorder.Dequeue().Operation);
                Assert.AreEqual(0, recorder.Count);

                Assert.IsNull(recorder.Dequeue());
            }

            // Now verify that setting IsEnabled=false clears the recorder.

            stream = new MemoryStream();

            using (var recorder = new FlightRecorder(stream))
            {
                Assert.IsTrue(recorder.IsPersistMode);
                Assert.IsFalse(recorder.IsPassThruMode);

                recorder.Log("event #1");

                recorder.IsEnabled = false;
                recorder.Log("event XX");
                recorder.IsEnabled = true;

                recorder.Log("event #2");

                Assert.AreEqual(1, recorder.Count);
                Assert.AreEqual("event #2", recorder.Dequeue().Operation);
            }
        }

        //---------------------------------------------------------------------
        // Pass-thru tests

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_Basic()
        {
            // Verify that the recorder can persist a couple of events
            // and then dequeue them.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                Assert.IsFalse(recorder.IsPersistMode);
                Assert.IsTrue(recorder.IsPassThruMode);

                recorder.Log("event #1");
                recorder.Log("event #2");
            }

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual("event #1", queue.Dequeue().Operation);
            Assert.AreEqual("event #2", queue.Dequeue().Operation);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_EventFields()
        {
            // Verify that all event fields are persisted properly.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();
            string organizationID = Guid.NewGuid().ToString();
            string userID = Guid.NewGuid().ToString();
            string sessionID = Guid.NewGuid().ToString();
            FlightEvent flightEvent;

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                Assert.IsFalse(recorder.IsPersistMode);
                Assert.IsTrue(recorder.IsPassThruMode);

                recorder.OrganizationID = null;
                recorder.UserID = null;
                recorder.SessionID = null;
                recorder.Source = null;
                recorder.SourceVersion = null;

                recorder.Log("event #1");

                recorder.OrganizationID = organizationID;
                recorder.UserID = userID;
                recorder.SessionID = sessionID;
                recorder.Source = "test.app";
                recorder.SourceVersion = new Version("1.2.3.4");

                recorder.Log("event #2", "details", true);
            }

            flightEvent = queue.Dequeue();
            Assert.IsNull(flightEvent.OrganizationID);
            Assert.IsNull(flightEvent.UserID);
            Assert.IsNull(flightEvent.SessionID);
            Assert.IsNull(flightEvent.Source);
            Assert.IsNull(flightEvent.SourceVersion);
            Assert.AreEqual("event #1", flightEvent.Operation);
            Assert.IsNull(flightEvent.Details);
            Assert.IsFalse(flightEvent.IsError);

            flightEvent = queue.Dequeue();
            Assert.AreEqual(organizationID, flightEvent.OrganizationID);
            Assert.AreEqual(userID, flightEvent.UserID);
            Assert.AreEqual(sessionID, flightEvent.SessionID);
            Assert.AreEqual("test.app", flightEvent.Source);
            Assert.AreEqual(new Version("1.2.3.4"), flightEvent.SourceVersion);
            Assert.AreEqual("event #2", flightEvent.Operation);
            Assert.AreEqual("details", flightEvent.Details);
            Assert.IsTrue(flightEvent.IsError);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_MaxEvents()
        {
            // Verify that the class ignores and doesn't crap out when 
            // setting the MaxEvents property.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                recorder.MaxEvents = 10;

                for (int i = 0; i < 20; i++)
                    recorder.Log(i.ToString());

                Assert.AreEqual(20, queue.Count);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_Clear()
        {
            // Verify that Clear() doesn't crap out.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                for (int i = 0; i < 20; i++)
                    recorder.Log(i.ToString());

                Assert.AreEqual(0, recorder.Count);
                recorder.Clear();
                Assert.AreEqual(0, recorder.Count);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_GetEvents()
        {
            // Verify that GetEvents() always returns an empty list.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                for (int i = 0; i < 20; i++)
                    recorder.Log(i.ToString());

                Assert.AreEqual(0, recorder.GetEvents().Count);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_SysLogProvider()
        {
            // Verify that the ISysLogProvider implementation works.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();
            ISysLogProvider orgProvider = SysLog.LogProvider;

            try
            {
                FlightEvent flightEvent;

                using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
                {
                    SysLog.LogProvider = recorder;

                    SysLog.LogError("Test Error");
                    SysLog.LogWarning("Test Warning");
                    SysLog.LogInformation("Test Information");
                    SysLog.Flush();

                    Assert.AreEqual(3, queue.Count);

                    flightEvent = queue.Dequeue();
                    Assert.AreEqual("SysLog:Error", flightEvent.Operation);
                    Assert.IsTrue(flightEvent.Details.Contains("Test Error"));
                    Assert.IsTrue(flightEvent.IsError);

                    flightEvent = queue.Dequeue();
                    Assert.AreEqual("SysLog:Warning", flightEvent.Operation);
                    Assert.IsTrue(flightEvent.Details.Contains("Test Warning"));
                    Assert.IsFalse(flightEvent.IsError);

                    flightEvent = queue.Dequeue();
                    Assert.AreEqual("SysLog:Information", flightEvent.Operation);
                    Assert.IsTrue(flightEvent.Details.Contains("Test Information"));
                    Assert.IsFalse(flightEvent.IsError);

                    // Verify that system events actually serialize exception
                    // and stack trace related information.

                    try
                    {
                        throw new AssertException();
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                        SysLog.Flush();

                        flightEvent = queue.Dequeue();
                        Assert.AreEqual("SysLog:Exception", flightEvent.Operation);
                        Assert.IsTrue(flightEvent.Details.Contains("AssertException"));
                    }
                }
            }
            finally
            {
                SysLog.LogProvider = orgProvider;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FlightRecorder_PassThru_Disabled()
        {
            // Verify that events are not logged when the recorder is disabled.

            Queue<FlightEvent> queue = new Queue<FlightEvent>();

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                Assert.IsFalse(recorder.IsPersistMode);
                Assert.IsTrue(recorder.IsPassThruMode);

                recorder.IsEnabled = false;
                recorder.Log("event XX");
                recorder.IsEnabled = true;

                recorder.Log("event #1");
                recorder.Log("event #2");
            }

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual("event #1", queue.Dequeue().Operation);
            Assert.AreEqual("event #2", queue.Dequeue().Operation);

            // Now verify that setting IsEnabled=false does not impact
            // any events logged before.

            queue.Clear();

            using (var recorder = new FlightRecorder(evt => queue.Enqueue(evt)))
            {
                Assert.IsFalse(recorder.IsPersistMode);
                Assert.IsTrue(recorder.IsPassThruMode);

                recorder.Log("event #1");

                recorder.IsEnabled = false;
                recorder.Log("event XX");
                recorder.IsEnabled = true;

                recorder.Log("event #2");

                Assert.AreEqual(2, queue.Count);
                Assert.AreEqual("event #1", queue.Dequeue().Operation);
                Assert.AreEqual("event #2", queue.Dequeue().Operation);
            }
        }
    }
}

