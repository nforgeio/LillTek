//-----------------------------------------------------------------------------
// FILE:        SysLogEntry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements some common code used for implementing an ISysLogProvider.

using System;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace LillTek.Common
{
    /// <summary>
    /// Implements some common code used for implementing an <see cref="ISysLogProvider" />.
    /// </summary>
    public sealed class SysLogEntry : IFlightEventInfo
    {
        /// <summary>
        /// The log entry time (UTC).
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Describes the log entry type.
        /// </summary>
        public SysLogEntryType Type;

        /// <summary>
        /// The log message text.
        /// </summary>
        public string Message;

        /// <summary>
        /// The extended event information (or <c>null</c>).
        /// </summary>
        [DataMember(IsRequired = false)]
        public ISysLogEntryExtension Extension = null;

        /// <summary>
        /// The debug log entry category (or <c>null</c>).
        /// </summary>
        [DataMember(IsRequired = false)]
        public string Category = null;

        /// <summary>
        /// The exception instance for logged exceptions (or <c>null</c>).
        /// </summary>
        [DataMember(IsRequired = false)]
        public Exception Exception = null;

        /// <summary>
        /// This can be used by in-memory log providers to chain log
        /// entries together into a list.
        /// </summary>
        [DataMember(IsRequired = false)]
        public SysLogEntry Next = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SysLogEntry()
        {
        }

        /// <summary>
        /// Constructs a log entry.
        /// </summary>
        /// <param name="type">The entry type.</param>
        /// <param name="message">The message.</param>
        /// <param name="extension">The extended log information (or <c>null</c>).</param>
        public SysLogEntry(ISysLogEntryExtension extension, SysLogEntryType type, string message)
        {
            this.Time      = DateTime.UtcNow;
            this.Type      = type;
            this.Message   = message;
            this.Extension = extension;
        }

        /// <summary>
        /// Constucts an exception based log entry.
        /// </summary>
        /// <param name="extension">The extended log information (or <c>null</c>).</param>
        /// <param name="e">The exception.</param>
        /// <param name="message">The message.</param>
        public SysLogEntry(ISysLogEntryExtension extension, Exception e, string message)
        {
            TargetInvocationException eInvoke;

            eInvoke = e as TargetInvocationException;
            if (eInvoke != null)
                e = eInvoke.InnerException;

            this.Time      = DateTime.UtcNow;
            this.Type      = SysLogEntryType.Exception;
            this.Exception = e;
            this.Message   = message;
            this.Extension = extension;
        }

        /// <summary>
        /// Constructs a debug related log entry.
        /// </summary>
        /// <param name="extension">The extended log information (or <c>null</c>).</param>
        /// <param name="type">The entry type.</param>
        /// <param name="category">The debug category.</param>
        /// <param name="message">The message.</param>
        public SysLogEntry(ISysLogEntryExtension extension, SysLogEntryType type, string category, string message)
        {
            this.Time      = DateTime.UtcNow;
            this.Type      = type;
            this.Category  = category;
            this.Message   = message;
            this.Extension = extension;
        }

        /// <summary>
        /// Appends information about an exception to string builder.
        /// </summary>
        /// <param name="sb">The string builder.</param>
        /// <param name="rootException">The root exception.</param>
        private void AppendException(StringBuilder sb, Exception rootException)
        {
            Exception   e       = rootException;
            int         nesting = 0;

            while (e != null)
            {
#if !SILVERLIGHT
                Win32Exception winErr = e as Win32Exception;

                if (nesting > 0)
                    sb.AppendFormat("\r\n------ Nested:{0} ------\r\n", nesting);

                if (winErr == null)
                    sb.AppendFormat((IFormatProvider)null, "{0}: {1} [\"{2}\"]\r\n", Type.ToString(), e.GetType().Name, e.Message.Replace("\r\n", " "));
                else
                    sb.AppendFormat((IFormatProvider)null, "{0}: {1} [\"{2}\"] [WINERR={3}]\r\n", Type.ToString(), e.GetType().Name, e.Message.Replace("\r\n", " "), winErr.NativeErrorCode);
#else
                sb.AppendFormat((IFormatProvider) null,"{0}: {1} [\"{2}\"]\r\n",Type.ToString(),e.GetType().Name,e.Message.Replace("\r\n"," "));
#endif

                ICustomExceptionLogger custom;

                custom = e as ICustomExceptionLogger;
                if (custom != null)
                    custom.Log(sb);

                sb.Append("\r\n");
                sb.Append(e.StackTrace);
                sb.Append("\r\n");

                e = e.InnerException;
                nesting++;
            }

            // Handler serialization of any aggregated exceptions.

            var aggregation = rootException as AggregateException;

            if (aggregation != null)
            {
                sb.Append("\r\n");
                sb.Append(" ============= Aggregated Exceptions =============\r\n");

                foreach (var subException in aggregation.InnerExceptions)
                    AppendException(sb, subException);
            }
        }

        /// <summary>
        /// Renders the log entry into a string.
        /// </summary>
        /// <param name="format">The formatting option flags.</param>
        /// <returns>The formatted string.</returns>
        public string ToString(SysLogEntryFormat format)
        {
            var sb = new StringBuilder();

            if ((format & SysLogEntryFormat.ShowBar) != 0)
                sb.Append("=================================\r\n");

            if ((format & SysLogEntryFormat.ShowTime) != 0)
                sb.AppendFormat((IFormatProvider)null, "Time: {0} UTC\r\n", Time.ToString("MM-dd-yyyy HH:mm:ss.fff"));

            if (this.Message != null)
            {
                sb.Append(Message);
                sb.Append("\r\n");
            }

            if (Extension != null && (format & SysLogEntryFormat.ShowExtended) != 0)
                sb.AppendFormat((IFormatProvider)null, Extension.Format());

            if (this.Exception != null)
                AppendException(sb, this.Exception);
            else if (this.Category != null)
            {
                if ((format & SysLogEntryFormat.ShowType) != 0)
                {
                    sb.AppendFormat((IFormatProvider)null, "{0}: {1}\r\n", Type.ToString(), Category);
                }
                else
                {
                    sb.AppendFormat((IFormatProvider)null, "{0}\r\n", Category);
                }
            }
            else
            {
                if ((format & SysLogEntryFormat.ShowType) != 0)
                {
                    sb.AppendFormat((IFormatProvider)null, "{0}:\r\n", Type.ToString());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Renders the log entry as a string enabling all formatting options.
        /// </summary>
        /// <returns>The formatted string.</returns>
        public override string ToString()
        {
            return ToString(SysLogEntryFormat.ShowAll);
        }

        //---------------------------------------------------------------------
        // IFlightEventInfo implementation

        /// <summary>
        /// Returns the flight event operation.
        /// </summary>
        public string SerializeOperation()
        {
            return string.Format("SysLog:{0}", this.Type);
        }

        /// <summary>
        /// Serializes the instance into a string that can be saved in a
        /// <see cref="FlightEvent" />.
        /// </summary>
        /// <returns>The serialized instance.</returns>
        public string SerializeDetails()
        {
            return this.ToString();
        }

        /// <summary>
        /// Unserializes the operation and details from a <see cref="FlightEvent" />
        /// into the current instance.
        /// </summary>
        /// <param name="flightEvent">The event being deseralized.</param>
        /// <exception cref="NotImplementedException">This class does not implement this method.</exception>
        public void Deserialize(FlightEvent flightEvent)
        {
            throw new NotImplementedException();
        }
    }
}