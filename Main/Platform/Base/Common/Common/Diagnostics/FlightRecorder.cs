//-----------------------------------------------------------------------------
// FILE:        FlightRecorder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to capture audit and log related events by client application
//              pending transmission to a permanent repository.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

#if SILVERLIGHT
using System.IO.IsolatedStorage;
#endif

namespace LillTek.Common
{
    /// <summary>
    /// Used to capture audit and log related events by client and server applications
    /// pending transmission to a permanent repository.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class operates in one of two modes: <b>persist</b> or <b>pass-thru</b>
    /// mode.  For <b>persist-mode</b>, you'll pass a <b>Stream</b> or file system path to
    /// the constructor and the class will temporarily persist logged <see cref="FlightEvent" />s
    /// to the associated stream until the events are dequeued and processed.
    /// </para>
    /// <para>
    /// For <b>pass-thru</b> mode an <see cref="Action{FlightEvent}" /> delegate is
    /// passed to the constructor and any logged events are immediately passed in
    /// a call to the delegate as the event is logged.  Events are not persisted
    /// in pass-thru mode.
    /// </para>
    /// <para>
    /// Persist mode is typically used for client applications that may not always
    /// be connected to the service where the constant uploading or processing of
    /// events is not desirable.  Pass-thru mode is designed for situations where
    /// the application can process events immediately or the performance cost of
    /// persisting events is too high.
    /// </para>
    /// <para>
    /// Use the <see cref="IsPersistMode" /> and/or <see cref="IsPassThruMode" /> properties
    /// to determine whether the flight recorder was started in persist or pass-thru mode.
    /// </para>
    /// <b><u>Persist Mode</u></b>
    /// <para>
    /// The flight recorder can be used by applications to gather and persist important
    /// events such as <see cref="SysLog" /> errors and warnings as well as application
    /// specific usage and audit information.  Information is logged as an ordered
    /// series of <see cref="FlightEvent" /> instances.  This class manages the
    /// persistance of these events to a stream which can be maintained in memory
    /// (as a <see cref="MemoryStream" /> or be persisted to the file system
    /// (including the Silverlight protected file system).
    /// </para>
    /// <para>
    /// The basic idea is that the application will use the <see cref="FlightRecorder" />
    /// to record events until there's a convienent time to upload the events to 
    /// a global service where they will be permanently persisted and analyzed.
    /// </para>
    /// <para>
    /// This class implements <see cref="ISysLogProvider" /> and can be configured
    /// to record all system log entries automatically.  Applications can also use
    /// the various <b>Log()</b> methods to log application specific events directly.
    /// </para>
    /// <para>
    /// The class is pretty easy to use.  Simply construct an instance, passing a
    /// file system path or an arbitrary <see cref="Stream" />.  Then initialize the
    /// <see cref="SessionID" />, <see cref="OrganizationID" />,
    /// <see cref="Source" />, and <see cref="SourceVersion" /> default
    /// event properties as desired and you're ready to begin recording events.
    /// Call <see cref="Close" /> or <see cref="Dispose" /> when you're finished
    /// with the recorder.
    /// </para>
    /// <para>
    /// The <see cref="GetEvents()" /> method returns a read-only collection of events
    /// in the order they were recorded, <see cref="Count" /> returns the number of
    /// recorded events.
    /// </para>
    /// <para>
    /// <see cref="MaxEvents" /> controls how many events can persisted to the 
    /// recorder at any time.  This defaults to <b>1000</b>.  If the recorder already
    /// holds the maximum number of entries when a new entry is logged, then the
    /// oldest entry will be removed before adding the new one.
    /// </para>
    /// <para>
    /// Controls whether or not the flight recorder automatically flushes changes
    /// to the backing stream.  This defaults to <c>true</c> and may have a
    /// performance impact for applications that do a lot of logging.  In these
    /// cases, the application can set <see cref="AutoFlush" /> to <c>false</c>
    /// and then call <see cref="Flush" /> explicitly.
    /// </para>
    /// <para>
    /// Applications can use one of the <b>Dequeue()</b> methods to remove events from the
    /// recorder for uploading to the service.
    /// </para>
    /// <para>
    /// Applications should take care to call <see cref="Close" /> when shutting down to
    /// ensure that the log is perisisted.
    /// </para>
    /// <b><u>Other Information</u></b>
    /// <para>
    /// The static <see cref="Global" /> property can be used to hold an application-wide
    /// flight recorder instance and the <see cref="IsEnabled" /> property can be used
    /// to disable all processing of logged events.
    /// </para>
    /// <para>
    /// The flight recorder can also be configured to submit any logged events to 
    /// an alternate <see cref="ISysLogProvider" /> by assigning the provider to
    /// the <see cref="AltLogProvider" /> property.  This is often used by servers
    /// that wish to record events to the local Windows event log as well as 
    /// have them handled by the flight recorder.
    /// </para>
    /// <note>
    /// This class uses the <see cref="SysTime.ExternalNow" /> property when recording
    /// events so that the event time can be synchronized with an external source.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class FlightRecorder : SysLogProvider, IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// File format magic number.
        /// 
        /// </summary>
        private int Magic = 0x9AAA;

        /// <summary>
        /// File format version.
        /// </summary>
        private int FormatVersion = 0;

        /// <summary>
        /// Used to hold an application-wide flight recorder instance.
        /// </summary>
        public static FlightRecorder Global { get; set; }

        //---------------------------------------------------------------------
        // Instance members

        private object                  syncLock         = new object();
        private EnhancedStream          stream           = null;
        private Queue<FlightEvent>      events           = new Queue<FlightEvent>();
        private Action<FlightEvent>     passThruCallback = null;
        private bool                    isRunning        = true;
        private bool                    isEnabled        = true;
        private bool                    isDirty          = false;
        private bool                    autoFlush        = true;
        private int                     maxEvents        = 1000;
        private ISysLogProvider         altLogProvider   = null;

        /// <summary>
        /// Constructs a flight recorder that runs in <b>persist</b> mode,
        /// saving events to the file system until the can be dequeued and
        /// processed by the application.
        /// </summary>
        /// <param name="path">Path to the log file.</param>
        /// <remarks>
        /// <para>
        /// For Silverlight builds, the <paramref name="path" /> parameter specifies
        /// a file within the application's isolated storage.  For non-Silverlight
        /// builds, the parameter specifies a normal file system path and the 
        /// constructor will attempt to create the directory tree as necessary.
        /// </para>
        /// <note>
        /// If the file cannot be opened for any reason (typically because another 
        /// instance of the application has already opened the file) then a memory
        /// stream will be created and used instead.
        /// </note>
        /// </remarks>
        public FlightRecorder(string path)
        {
            try
            {
#if SILVERLIGHT
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    this.stream = new EnhancedStream(store.OpenFile(path,FileMode.OpenOrCreate,FileAccess.ReadWrite));
                }
#else
                Helper.CreateFileTree(path);
                this.stream = new EnhancedFileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
#endif
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                this.stream = new EnhancedMemoryStream();
            }

            Load();
        }

        /// <summary>
        /// Constructs a flight recorder that runs in <b>persist</b> mode,
        /// saving events to a <see cref="Stream" /> until the can be dequeued and
        /// processed by the application.
        /// </summary>
        /// <param name="stream">The log stream.</param>
        /// <remarks>
        /// <note>
        /// The stream must implement the following members: <see cref="Stream.Position" /> and <see cref="Stream.SetLength" />
        /// such the the stream position and stream length can be changed.
        /// </note>
        /// </remarks>
        public FlightRecorder(Stream stream)
        {
            this.stream = new EnhancedStream(stream);

            Load();
        }

        /// <summary>
        /// Constructs a flight recorder that runs in <b>pass-thru</b> mode and
        /// immediately passed logged events to the callback passed.
        /// </summary>
        /// <param name="passThruCallback">The event processing callback.</param>
        public FlightRecorder(Action<FlightEvent> passThruCallback)
        {
            if (passThruCallback == null)
                throw new ArgumentNullException("passThruCallback");

            this.passThruCallback = passThruCallback;
        }

        /// <summary>
        /// Destructor that makes sure the log is persisted.
        /// </summary>
        ~FlightRecorder()
        {
            Close();
        }

        /// <summary>
        /// The optional alternate log provider.  If set, the flight recorder will log all
        /// events to this provider as well as recording them.
        /// </summary>
        /// <remarks>
        /// This is useful for servers that wish to log events locally to the Windows
        /// event log as well as submit them to the flight recorder.
        /// </remarks>
        public ISysLogProvider AltLogProvider
        {
            get { return altLogProvider; }
            set { altLogProvider = value; }
        }

        /// <summary>
        /// Controls whether or not logged events are actually processed by the
        /// recorder.  Defaults to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property defaults to <c>true</c> after a flight recorder is instantiated.
        /// Setting this to <c>false</c> will cause all persisted events to be cleared
        /// and will prevent any processing of logged events until the property is
        /// set back to <c>true</c>.
        /// </para>
        /// </remarks>
        public bool IsEnabled
        {
            get { return isEnabled; }

            set
            {
                lock (syncLock)
                {
                    if (!value)
                        Clear();

                    isEnabled = value;
                }
            }
        }

        /// <summary>
        /// Controls whether or not the flight recorder automatically flushes changes
        /// to the backing stream.  Defaults to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, flight recorders start out with <see cref="AutoFlush" /> set
        /// to <c>true</c>.  This means that the flight recorder will flush the
        /// entire contents of the log to the backing stream (if the flight recorder
        /// is not in pass-thru mode).  This setting may result in performance issues
        /// in cases when a lot of events are logged.
        /// </para>
        /// <para>
        /// You may set <see cref="AutoFlush" /> to <c>false</c> and then call 
        /// <see cref="Flush" /> yourself periodically persist the log.
        /// </para>
        /// <note>
        /// The flight recorder will still persist the contents of the log to the 
        /// backing stream when the recorder is stopped regardless of the setting
        /// of this property.
        /// </note>
        /// </remarks>
        public bool AutoFlush
        {
            get { return this.autoFlush; }

            set
            {
                this.autoFlush = value;

                if (this.autoFlush && this.isRunning && this.isDirty)
                    Save();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the flight recorder was started in <b>persist</b> mode,
        /// <c>false</c> for <b>pass-thru</b> mode.
        /// </summary>
        public bool IsPersistMode
        {
            get { return passThruCallback == null; }
        }

        /// <summary>
        /// Returns <c>true</c> if the flight recorder was started in <b>pass-thru</b> mode,
        /// <c>false</c> for <b>persist</b> mode.
        /// </summary>
        public bool IsPassThruMode
        {
            get { return passThruCallback != null; }
        }

        /// <summary>
        /// The default session identifier included in log entries (or <c>null</c>).
        /// </summary>
        public string SessionID { get; set; }

        /// <summary>
        /// The default organization identifier included in log entries (or <c>null</c>).
        /// </summary>
        public string OrganizationID { get; set; }

        /// <summary>
        /// The default user identifier included in log entries (or <c>null</c>).
        /// </summary>
        public string UserID { get; set; }

        /// <summary>
        /// Identifies the event source (typically the name of the client application).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Identifies the event source version (typically the version of the client application).
        /// </summary>
        public Version SourceVersion { get; set; }

        /// <summary>
        /// The maximum number of entries to maintained by the log (defaults to <b>1000</b>).
        /// </summary>
        public int MaxEvents
        {
            get { return maxEvents; }
            set { maxEvents = value; }
        }

        /// <summary>
        /// Returns a read-only collection of the stored events in the order they were recorded.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method returns an empty list for <b>pass-thru</b> mode of if the recorder
        /// is stopped.
        /// </note>
        /// </remarks>
        public ICollection<FlightEvent> GetEvents()
        {
            if (IsPassThruMode)
                return new ReadOnlyCollection<FlightEvent>(new List<FlightEvent>(0));

            lock (syncLock)
            {
                if (!isRunning)
                    return new ReadOnlyCollection<FlightEvent>(new List<FlightEvent>(0));

                return new ReadOnlyCollection<FlightEvent>(this.events.ToList());
            }
        }

        /// <summary>
        /// Returns the number of recorded events.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property always returns zero for <b>pass-thru</b> mode
        /// or if the recorder is stopped.
        /// </note>
        /// </remarks>
        public int Count
        {
            get
            {
                if (IsPassThruMode)
                    return 0;

                lock (syncLock)
                {

                    return isRunning ? events.Count : 0;
                }
            }
        }

        /// <summary>
        /// Closes the underlying stream and stops the flight recorder.
        /// </summary>
        /// <remarks>
        /// <note>
        /// All subsequent attempts to record events will be ignored.
        /// </note>
        /// </remarks>
        public void Close()
        {
            lock (syncLock)
            {
                if (!isRunning)
                    return;

                if (IsPassThruMode)
                {
                    isRunning = false;
                    return;
                }

                Save();

                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }

                isRunning = false;

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Closes the underlying stream and stops the flight recorder.
        /// </summary>
        /// <remarks>
        /// <note>
        /// All subsequent attempts to record events will be ignored.
        /// </note>
        /// </remarks>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Removes all recorded events from the flight recorder.
        /// </summary>
        public void Clear()
        {
            lock (syncLock)
            {
                if (IsPassThruMode || !isRunning)
                    return;

                this.isDirty = true;
                this.events.Clear();

                if (this.autoFlush)
                    Save();
            }
        }

        /// <summary>
        /// Records a fully specified <see cref="FlightEvent" />.
        /// </summary>
        /// <param name="flightEvent"></param>
        public void Log(FlightEvent flightEvent)
        {
            try
            {
                if (flightEvent.Operation == null)
                    flightEvent.Operation = "[not set]";

                lock (syncLock)
                {
                    if (!isRunning || !isEnabled)
                        return;

                    if (IsPassThruMode)
                    {
                        passThruCallback(flightEvent);
                        return;
                    }

                    if (this.maxEvents <= 0)
                        return;

                    while (this.events.Count >= this.maxEvents)
                        this.events.Dequeue();

                    this.events.Enqueue(flightEvent);
                    this.isDirty = true;

                    if (this.autoFlush)
                        Save();
                }
            }
            catch
            {

                // Ignore errors
            }
        }

        /// <summary>
        /// Logs an object implementing <see cref="IFlightEventInfo" />.
        /// </summary>
        /// <param name="eventInfo">The event information.</param>
        public void Log(IFlightEventInfo eventInfo)
        {
            Log(eventInfo, false);
        }

        /// <summary>
        /// Logs an object implementing <see cref="IFlightEventInfo" />
        /// with a failure indication.
        /// </summary>
        /// <param name="eventInfo">The event information.</param>
        /// <param name="isError">Indicates whether the operation succeeded or failed.</param>
        public void Log(IFlightEventInfo eventInfo, bool isError)
        {
            FlightEvent flightEvent;

            flightEvent                = new FlightEvent();
            flightEvent.TimeUtc        = SysTime.ExternalNow;
            flightEvent.OrganizationID = this.OrganizationID;
            flightEvent.UserID         = this.UserID;
            flightEvent.SessionID      = this.SessionID;
            flightEvent.Source         = this.Source;
            flightEvent.SourceVersion  = this.SourceVersion;
            flightEvent.Operation      = eventInfo.SerializeOperation();
            flightEvent.IsError        = isError;
            flightEvent.Details        = eventInfo.SerializeDetails();

            Log(flightEvent);
        }

        /// <summary>
        /// Records an event using default properties as required.
        /// </summary>
        /// <param name="operation">Identifies the operation performed.</param>
        /// <remarks>
        /// This method includes the values the default properties:
        /// <see cref="SessionID" />, <see cref="OrganizationID" />,
        /// <see cref="Source" />, and <see cref="SourceVersion" /> 
        /// in the recorded entry and also sets <see cref="FlightEvent.Details" />=<c>null</c>
        /// and <see cref="FlightEvent.IsError" />=<c>false</c>.
        /// </remarks>
        public void Log(string operation)
        {
            Log(new FlightEvent()
            {
                TimeUtc        = SysTime.ExternalNow,
                OrganizationID = this.OrganizationID,
                UserID         = this.UserID,
                SessionID      = this.SessionID,
                Source         = this.Source,
                SourceVersion  = this.SourceVersion,
                Operation      = operation,
                IsError        = false,
                Details        = null
            });
        }

        /// <summary>
        /// Records an event using default properties as required.
        /// </summary>
        /// <param name="operation">Identifies the operation performed.</param>
        /// <param name="details">The operation details.</param>
        /// <summary>
        /// Records an event using default properties as required.
        /// </summary>
        /// <remarks>
        /// This method includes the values the default properties:
        /// <see cref="SessionID" />, <see cref="OrganizationID" />,
        /// <see cref="Source" />, and <see cref="SourceVersion" /> 
        /// in the recorded entry and <see cref="FlightEvent.IsError" />=<c>false</c>.
        /// </remarks>
        public void Log(string operation, string details)
        {
            Log(new FlightEvent()
            {
                TimeUtc        = SysTime.ExternalNow,
                OrganizationID = this.OrganizationID,
                UserID         = this.UserID,
                SessionID      = this.SessionID,
                Source         = this.Source,
                SourceVersion  = this.SourceVersion,
                Operation      = operation,
                IsError        = false,
                Details        = details
            });
        }

        /// <summary>
        /// Records an event using default properties as required.
        /// </summary>
        /// <param name="operation">Identifies the operation performed.</param>
        /// <param name="details">The operation details.</param>
        /// <param name="isError">Indicates whether the operation succeeded or failed.</param>
        /// <remarks>
        /// This method includes the values the default properties:
        /// <see cref="SessionID" />, <see cref="OrganizationID" />,
        /// <see cref="Source" />, and <see cref="SourceVersion" />.
        /// </remarks>
        public void Log(string operation, string details, bool isError)
        {
            Log(new FlightEvent()
            {
                TimeUtc        = SysTime.ExternalNow,
                OrganizationID = this.OrganizationID,
                UserID         = this.UserID,
                SessionID      = this.SessionID,
                Source         = this.Source,
                SourceVersion  = this.SourceVersion,
                Operation      = operation,
                IsError        = isError,
                Details        = details
            });
        }

        /// <summary>
        /// Returns and removes the oldest event from the recorder.
        /// </summary>
        /// <returns>The oldest <see cref="FlightEvent" /> from the queue or <c>null</c> if the recorder is empty.</returns>
        /// <remarks>
        /// <note>
        /// This method always returns <c>null</c> for <b>pass-thru</b> mode.
        /// </note>
        /// </remarks>
        public FlightEvent Dequeue()
        {
            try
            {
                lock (syncLock)
                {
                    if (IsPassThruMode || !isRunning)
                        return null;

                    FlightEvent flightEvent;

                    if (this.events.Count == 0)
                        return null;

                    flightEvent = this.events.Dequeue();
                    this.isDirty = true;

                    if (this.autoFlush)
                        Save();

                    return flightEvent;
                }
            }
            catch
            {
                return null;    // Ignore errors
            }
        }

        /// <summary>
        /// Returns and removes the oldest <paramref Name="Count" /> event from the recorder.
        /// </summary>
        /// <returns>The list of <see cref="FlightEvent" />s from the front of the queue.</returns>
        /// <remarks>
        /// <note>
        /// This method always returns and empty list for <b>pass-thru</b> mode.
        /// </note>
        /// </remarks>
        public List<FlightEvent> Dequeue(int count)
        {
            try
            {
                lock (syncLock)
                {
                    if (IsPassThruMode || !isRunning || this.events.Count == 0)
                        return new List<FlightEvent>(0);

                    int actualCount = Math.Min(count, this.events.Count);
                    List<FlightEvent> flightEvents = new List<FlightEvent>(actualCount);

                    for (int i = 0; i < actualCount; i++)
                        flightEvents.Add(this.events.Dequeue());

                    this.isDirty = true;

                    if (this.autoFlush)
                        Save();

                    return flightEvents;
                }
            }
            catch
            {
                return null;    // Ignore errors
            }
        }

        /// <summary>
        /// Loads the events from the backing stream, ignoring any errors.
        /// </summary>
        private void Load()
        {
            if (IsPassThruMode)
                return;

            lock (syncLock)
            {
                try
                {
                    this.events.Clear();

                    if (stream.ReadInt32() != Magic)
                        return;

                    if (stream.ReadInt32() != FormatVersion)
                        return;

                    int count;

                    count = stream.ReadInt32();
                    for (int i = 0; i < count; i++)
                        this.events.Enqueue(new FlightEvent(stream));

                    stream.Flush();

                    this.isDirty = false;
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        /// <summary>
        /// Rewrites the events to the backing stream, ignoring any errors.
        /// </summary>
        private void Save()
        {
            if (IsPassThruMode)
                return;

            lock (syncLock)
            {
                if (!this.isRunning || !this.isDirty)
                    return;

                try
                {
                    stream.SetLength(0);
                    stream.WriteInt32(Magic);
                    stream.WriteInt32(FormatVersion);
                    stream.WriteInt32(this.events.Count);

                    foreach (var flightEvent in this.events)
                        flightEvent.Write(stream);

                    this.isDirty = false;
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        //---------------------------------------------------------------------
        // SysLogProvider implementation

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public override void Flush()
        {
            if (altLogProvider != null)
                altLogProvider.Flush();

            if (IsPassThruMode)
                return;

            lock (syncLock)
            {
                if (!isRunning)
                    return;

                Save();
            }
        }

        /// <summary>
        /// Appends a <see cref="SysLogEntry" /> to the event log.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected override void Append(SysLogEntry entry)
        {
            if (altLogProvider != null)
                altLogProvider.Log(entry);

            try
            {
                entry.Time = SysTime.ExternalNow;
                Log(entry.SerializeOperation(), entry.SerializeDetails(), entry.Type == SysLogEntryType.Error || entry.Type == SysLogEntryType.Exception || entry.Type == SysLogEntryType.SecurityFailure);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
