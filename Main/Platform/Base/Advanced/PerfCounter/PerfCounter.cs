//-----------------------------------------------------------------------------
// FILE:        PerfCounter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A higher-level implementation of Windows performance counters.

using System;
using System.Diagnostics;
using System.Threading;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// A higher-level implementation of the Windows performance counters that 
    /// simplifies usage of Windows performance counters within applications and 
    /// also provides and alternate implementation for situations where the current
    /// process doesn't have sufficient rights to access system performance counters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class can be used to implement a performance counter that is connected to
    /// a standard Windows performance counter that can be viewed by other processes
    /// via the Windows API or WMI.  This class also provides an alternate implementation
    /// that provides the same capabilities but only within the context of local
    /// process.  The latter is useful, because installing Windows performance counters
    /// requires elevated security rights.
    /// </para>
    /// <note>
    /// The only difference between a counter that maps to a real Windows performance counter
    /// and a process local counter is that Windows counters will have the <see cref="Counter"/>
    /// property set to a <see cref="PerformanceCounter" /> instance and this will be <c>null</c>
    /// for local counters.
    /// </note>
    /// <para>
    /// The <see cref="PerfCounterSet" /> class is typically used to install and manage
    /// performance counters.  This class attempts to create standard Windows performance
    /// counters and if that fails, revert to creating local counters.
    /// </para>
    /// <note>
    /// At this time, the only counter types supported for process local counters are:
    /// <see cref="PerformanceCounterType.NumberOfItems32" />, <see cref="PerformanceCounterType.NumberOfItems64" />,
    /// <see cref="PerformanceCounterType.RateOfCountsPerSecond32" />, and <see cref="PerformanceCounterType.RateOfCountsPerSecond32" />.
    /// Other types may be supported in the future.  A silent warning will be logged when attempting
    /// to create performance counter with an unsupported type.
    /// </note>
    /// <note>
    /// The static <see cref="ProcessLocalOnly"/> property can be used to force the 
    /// local simulation.  The can be useful for testing and also for situations where
    /// the application knows its not going to get access to Windows performance counters.
    /// </note>
    /// <note>
    /// <para><b>Thread Safety:</b></para>
    /// <para>
    ///  The <see cref="Increment" />, <see cref="IncrementBy" />, 
    /// <see cref="Decrement" />, and <see cref="Close"/> methods are thread-safe.  
    /// The <see cref="NextValue"/> method should only be called on a single thread.
    /// The <see cref="RawValue "/> property can be accessed safely from multiple threads
    /// and may also be set with the understanding that when incrementing the property, 
    /// some updates may be lost
    /// </para>
    /// <para>
    /// <see cref="Value"/> may also be accessed from multiple threads.  This returns
    /// the counter value calculated by the last call to <see cref="NextValue"/>.
    /// The essential pattern for using counters is to have a single thread or timer
    /// calling <see cref="NextValue"/> to perform the counter calculations and then
    /// use <see cref="Value"/> from anywhere to access the calculated value.
    /// </para>
    /// </note>
    /// </remarks>
    public sealed class PerfCounter
    {
        //---------------------------------------------------------------------
        // Static members

        private static bool processLocalOnly = false;

        /// <summary>
        /// This stub performance counter doesn't actually connect to a real Windows
        /// performance counter so it can be used in situations when performance
        /// counting is not enabled.
        /// </summary>
        public static readonly PerfCounter Stub = new PerfCounter("(null)", string.Empty, string.Empty, PerformanceCounterType.NumberOfItems32);

        /// <summary>
        /// Controls whether the process local performance counter implementation will
        /// be used rather than trying to support Windows performance counters.
        /// </summary>
        public static bool ProcessLocalOnly
        {
            get { return processLocalOnly; }
            set { processLocalOnly = value; }
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                  syncLock = new object();
        private string                  name;
        private string                  category;
        private string                  help;
        private string                  instance;
        private PerformanceCounterType  type;
        private PerformanceCounter      counter;
        private PerfCounter[]           related;
        private string[]                relatedNames;
        private long                    localRawValue;
        private DateTime                localStartTimeSys;
        private float                   lastValue;

        /// <summary>
        /// Use this to initialize a stub performance counter.
        /// </summary>
        public PerfCounter()
        {
            this.name              = null;
            this.category          = null;
            this.help              = null;
            this.instance          = null;
            this.counter           = null;
            this.related           = null;
            this.relatedNames      = null;
            this.localRawValue     = 0;
            this.localStartTimeSys = DateTime.MinValue;
            this.lastValue         = 0.0F;
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="name">The logical counter name.</param>
        /// <param name="help">Counter description.</param>
        /// <param name="type">The performance counter type.</param>
        public PerfCounter(string name, string help, PerformanceCounterType type)
            : this()
        {
            this.name     = name;
            this.category = string.Empty;
            this.help     = help;
            this.type     = type;

            switch (type)
            {
                case PerformanceCounterType.NumberOfItems32:
                case PerformanceCounterType.NumberOfItems64:
                case PerformanceCounterType.RateOfCountsPerSecond32:
                case PerformanceCounterType.RateOfCountsPerSecond64:

                    break;

                default:

                    SysLog.LogWarning("Performance counter type [{0}] for counter [{1}] is not supported for process local counters.  [NextValue()] will always return zero.", type, name);
                    break;
            }
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="name">The logical counter name.</param>
        /// <param name="category">The logical counter category.</param>
        /// <param name="help">Counter description.</param>
        /// <param name="type">The performance counter type.</param>
        public PerfCounter(string name, string category, string help, PerformanceCounterType type)
            : this(name, help, type)
        {
            this.category = category;
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="name">The logical counter name.</param>
        /// <param name="category">The logical counter category.</param>
        /// <param name="help">Counter description.</param>
        /// <param name="instance">The counter instance name.</param>
        /// <param name="type">The performance counter type.</param>
        public PerfCounter(string name, string category, string help, string instance, PerformanceCounterType type)
            : this()
        {
            this.name     = name;
            this.category = category;
            this.help     = help;
            this.instance = instance;
            this.type     = type;
        }

        /// <summary>
        /// Initializes a LillTek performance counter from a .NET counter.
        /// </summary>
        /// <param name="counter">The .NET counter.</param>
        public PerfCounter(PerformanceCounter counter)
            : this()
        {
            this.counter  = counter;
            this.name     = counter.CounterName;
            this.category = counter.CategoryName;
            this.help     = counter.CounterHelp;
            this.instance = counter.InstanceName;
            this.type     = counter.CounterType;
        }

        /// <summary>
        /// Releases unmanaged resources.
        /// </summary>
        ~PerfCounter()
        {
            this.Close();
        }

        /// <summary>
        /// Closes the underlying performance counter if present.
        /// </summary>
        public void Close()
        {
            var ctr = counter;

            if (ctr != null)
            {
                if (!ctr.ReadOnly)
                    ctr.RawValue = 0;

                ctr.Close();
                counter = null;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the counter name.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Returns the counter category.
        /// </summary>
        public string Category
        {
            get { return category; }
        }

        /// <summary>
        /// Returns the counter description.
        /// </summary>
        public string Help
        {
            get { return help; }
        }

        /// <summary>
        /// Returns the instance name or <c>null</c> if this is a single instance counter.
        /// </summary>
        public string Instance
        {
            get { return instance; }
        }

        /// <summary>
        /// Returns the performance counter type.
        /// </summary>
        public PerformanceCounterType Type
        {
            get { return type; }
        }

        /// <summary>
        /// Returns the names of the counters related to this counter (or <c>null</c>).  
        /// Related counters will be incremented or decremented when this counter 
        /// is modified.  This makes it easy to implement rollup counters.
        /// </summary>
        public string[] RelatedCounters
        {
            get { return relatedNames; }
            set { relatedNames = value; }
        }

        /// <summary>
        /// The set of related performance counters (or <c>null</c>).  This is used
        /// by PerfCounterSet to initialize the related underlying counters
        /// based on their names.
        /// </summary>
        internal PerfCounter[] Related
        {
            get { return related; }
            set { related = value; }
        }

        /// <summary>
        /// The underlying .NET framework performance counter or <c>null</c> if the counter
        /// is running locally within the process.
        /// </summary>
        public PerformanceCounter Counter
        {
            get { return counter; }
            set { counter = value; }
        }

        /// <summary>
        /// Returns a nicely formatted and scaled string representation of
        /// the value passed.
        /// </summary>
        /// <param name="v">The value.</param>
        public static string ToString(double v)
        {
            bool    neg;
            string  s;
            string  suffix = " ";
            string  format;
            int     p;

            if (v == 0.0)
                return "0" + suffix;

            if (v < 0.0)
            {
                neg = true;
                v   = -v;
            }
            else
                neg = false;

            if (v >= 1000000.0)
            {
                v /= 1000000.0;
                if (v > 100)
                    format = "F0";
                else if (v > 10)
                    format = "F1";
                else
                    format = "F2";

                s = v.ToString(format);
                suffix = "M";
            }
            else if (v >= 10000.0)
            {
                v /= 1000.0;
                if (v > 100)
                    format = "F0";
                else if (v > 10)
                    format = "F1";
                else
                    format = "F2";

                s = v.ToString(format);
                suffix = "K";
            }
            else
            {
                if (v > 100)
                    format = "F0";
                else if (v > 10)
                    format = "F1";
                else
                    format = "F2";

                s = v.ToString(format);
            }

            if (format == "F2")
            {
                p = s.IndexOf(".00");
                if (p != -1)
                    s = s.Substring(0, p);
            }
            else if (format == "F1")
            {
                p = s.IndexOf(".0");
                if (p != -1)
                    s = s.Substring(0, p);
            }

            if (neg)
                return "-" + s + suffix;
            else
                return s + suffix;
        }

        /// <summary>
        /// Returns the current value of the counter in human-readable form,
        /// including scaling large numbers down to reasonable values.
        /// </summary>
        public override string ToString()
        {
            return ToString(this.Value);
        }

        /// <summary>
        /// Gets/sets the raw, uncalculated value of the counter.  Note
        /// that modifying this value will NOT modify related counters.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method is not completely thread-safe.  This may be used to
        /// change the counter value but under under load an a multi-threaded
        /// application, it may be possible to lose counter updates, but setting
        /// this directly is about 5 times faster than using the <see cref="Increment"/>,
        /// <see cref="IncrementBy" />, and <see cref="Decrement" /> methods.
        /// So it might be worth setting this directly in high performance situations
        /// where totally accurate counting isn't as important.
        /// </note>
        /// </remarks>
        public long RawValue
        {
            get
            {
                var ctr = counter;

                if (ctr == null || processLocalOnly)
                    return localRawValue;
                else
                    return ctr.RawValue;
            }

            set
            {
                var ctr = counter;

                if (ctr == null || processLocalOnly)
                    localRawValue = value;
                else
                    ctr.RawValue = value;
            }
        }

        /// <summary>
        /// Returns the next calculated value for the performance counter.  Note
        /// that for counters that require two samples, the first call to this
        /// method will return 0.0.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Your application must ensure that only a single thread will call 
        /// <see cref="NextValue"/> at any given instant to ensure that counter
        /// values are computed accurately.  Applications may access <see cref="Value" />
        /// from multiple threads to obtain the value calculated the last call
        /// to this method.
        /// </para>
        /// </remarks>
        public float NextValue()
        {
            var ctr = counter;

            if (ctr != null && !processLocalOnly)
                return lastValue = ctr.NextValue();

            // $todo: Support additional counter types.

            switch (type)
            {
                case PerformanceCounterType.NumberOfItems32:
                case PerformanceCounterType.NumberOfItems64:

                    lock (syncLock)
                    {
                        return lastValue = (float)localRawValue;
                    }

                case PerformanceCounterType.RateOfCountsPerSecond32:
                case PerformanceCounterType.RateOfCountsPerSecond64:

                    var startSys = localStartTimeSys;
                    var nowSys   = SysTime.Now;
                    var count    = localRawValue;

                    localStartTimeSys = nowSys;
                    localRawValue     = 0;

                    if (localStartTimeSys == DateTime.MinValue)
                        return 0.0F;

                    var calculatedValue = (float)(count / (nowSys - startSys).TotalSeconds);

                    lock (syncLock)
                    {
                        return lastValue = calculatedValue;
                    }

                default:

                    return 0.0F;
            }
        }

        /// <summary>
        /// Returns the counter value returned by the last call to <see cref="NextValue" />.
        /// This property may be accessed simultaneously by multiple threads.
        /// </summary>
        public float Value
        {
            get
            {
                lock (syncLock)
                {
                    return lastValue;
                }
            }
        }

        /// <summary>
        /// Increments the counter by one.
        /// </summary>
        /// <returns>The new value.</returns>
        /// <remarks>
        /// <note>
        /// This does not throw an exception if the underlying
        /// .NET performance counter instance has not been created.
        /// </note>
        /// </remarks>
        public long Increment()
        {
            // Increment any related counters

            if (related != null)
            {
                for (int i = 0; i < related.Length; i++)
                    related[i].Increment();
            }

            // Increment the counter

            var ctr = counter;

            if (ctr != null && !processLocalOnly)
                return ctr.Increment();
            else
                return Interlocked.Increment(ref localRawValue);
        }

        /// <summary>
        /// Increments the counter by the value passed.
        /// </summary>
        /// <param name="value">The amount to increment by.</param>
        /// <returns>The new value.</returns>
        /// <remarks>
        /// <note>
        /// This does not throw an exception if the underlying
        /// .NET performance counter instance has not been created.
        /// </note>
        /// </remarks>
        public long IncrementBy(long value)
        {
            // Increment any related counters

            if (related != null)
            {
                for (int i = 0; i < related.Length; i++)
                    related[i].IncrementBy(value);
            }

            // Increment the counter

            var ctr = counter;

            if (ctr != null && !processLocalOnly)
                return ctr.IncrementBy(value);
            else
                return Interlocked.Add(ref localRawValue, value);
        }

        /// <summary>
        /// Decrements the counter by one.
        /// </summary>
        /// <returns>The new value.</returns>
        /// <remarks>
        /// <note>
        /// This does not throw an exception if the underlying
        /// .NET performance counter instance has not been created.
        /// </note>
        /// </remarks>
        public long Decrement()
        {
            // Decrement any related counters

            if (related != null)
            {
                for (int i = 0; i < related.Length; i++)
                    related[i].Decrement();
            }

            // Decrement the counter

            var ctr = counter;

            if (ctr != null && !processLocalOnly)
                return ctr.Decrement();
            else
                return Interlocked.Decrement(ref localRawValue);
        }
    }
}
