//-----------------------------------------------------------------------------
// FILE:        Serialize.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.

using System;
using System.IO;

using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;

#if !SILVERLIGHT
using System.IO.Compression;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
#endif

namespace LillTek.Common
{
#if MOBILE_DEVICE
    /// <summary>
    /// Implements extended serialization for some common types as well as
    /// serialization of complex objects and object graphs.
    /// </summary>
#else
    /// <summary>
    /// Implements extended serialization for some common types as well as
    /// serialization of complex objects and object graphs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In addition to handling string based serialization of simple types,
    /// this class also provides the <see cref="ToBinary(object,Compress)" />,
    /// <see cref="ToBinary(Stream,object,Compress)" />, <see cref="FromBinary(byte[])" />
    /// and <see cref="FromBinary(Stream)" /> methods that handle serialization 
    /// of complex objects and object graphs.
    /// </para>
    /// <para>
    /// These methods use a combination of <see cref="BinaryFormatter" />
    /// and <see cref="DeflateStream" /> to perform the actual serialization.
    /// This means that classes will need to be tagged with the <c>[ISerializable]</c>
    /// attribute to be serialized.
    /// </para>
    /// <note>
    /// Even though this class uses <see cref="BinaryFormatter" />, the serialized
    /// output generated is not directly compatible with this class.  The reason for
    /// this is the addition of a few bytes of header information and the optional
    /// compression performed by this class.
    /// </note>
    /// <para>
    /// The <see cref="ToXml(object)" />, <see cref="ToXml(TextWriter,object)" />, 
    /// <see cref="FromXml(string,System.Type)" />, and <see cref="FromXml(TextReader,System.Type)" /> 
    /// methods can be used to serialize object graphs to and from XML.
    /// </para>
    /// </remarks>
#endif
    public static class Serialize
    {
        private static XmlWriterSettings xmlSettings;    // Invariant XML formatting settings
#if !SILVERLIGHT
        private static XmlSerializerNamespaces xmlNamespaces;  // An empty namespaces collection
#endif

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Serialize()
        {
            xmlSettings                    = new XmlWriterSettings();
            xmlSettings.Encoding           = Encoding.UTF8;
            xmlSettings.Indent             = false;
            xmlSettings.NewLineHandling    = NewLineHandling.None;
            xmlSettings.OmitXmlDeclaration = true;
            xmlSettings.ConformanceLevel   = ConformanceLevel.Document;

#if !SILVERLIGHT
            xmlNamespaces = new XmlSerializerNamespaces();
            xmlNamespaces.Add("", "");
#endif
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(string v)
        {
            return v.Trim();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static string Parse(string value, string def)
        {
            if (value == null)
                return def;

            return value.Trim();
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(int v)
        {
            return v.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", and "G" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, and "G" multiples the value
        /// parsed by 1073741824.  The "T" suffix is not supported
        /// by this method because it exceeds the capacity of a
        /// 32-bit integer.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public static int Parse(string value, int def)
        {
            int     result;
            int     multiplier;
            char    last;

            if (value == null || value.Length == 0)
                return def;

            value = value.Trim();
            switch (value)
            {
                case "short.min":   return short.MinValue;
                case "short.max":   return short.MaxValue;
                case "ushort.max":  return ushort.MaxValue;
                case "int.min":     return int.MinValue;
                case "int.max":     return int.MaxValue;
            }

            last = value[value.Length - 1];
            if (Char.IsDigit(last))
                multiplier = 1;
            else
            {
                switch (last)
                {

                    case 'k':
                    case 'K':

                        multiplier = 1024;
                        break;

                    case 'm':
                    case 'M':

                        multiplier = 1024 * 1024;
                        break;

                    case 'g':
                    case 'G':

                        multiplier = 1024 * 1024 * 1024;
                        break;

                    default:

                        return def;
                }

                value = value.Substring(0, value.Length - 1);
            }

            if (int.TryParse(value, out result))
                return result * multiplier;
            else
                return def;
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(long v)
        {
            return v.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", "G", and "T" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, "G" multiples the value
        /// parsed by 1073741824, and "T" multiplies the value by 
        /// 1099511627776.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public static long Parse(string value, long def)
        {
            long    result;
            long    multiplier;
            char    last;

            if (value == null || value.Length == 0)
                return def;

            value = value.Trim();
            switch (value)
            {
                case "short.min":   return short.MinValue;
                case "short.max":   return short.MaxValue;
                case "ushort.max":  return ushort.MaxValue;
                case "int.min":     return int.MinValue;
                case "int.max":     return int.MaxValue;
                case "uint.max":    return uint.MaxValue;
                case "long.min":    return long.MinValue;
                case "long.max":    return long.MaxValue;
            }

            last = value[value.Length - 1];
            if (Char.IsDigit(last))
                multiplier = 1;
            else
            {
                switch (last)
                {
                    case 'k':
                    case 'K':

                        multiplier = 1024L;
                        break;

                    case 'm':
                    case 'M':

                        multiplier = 1024L * 1024L;
                        break;

                    case 'g':
                    case 'G':

                        multiplier = 1024L * 1024L * 1024L;
                        break;

                    case 't':
                    case 'T':

                        multiplier = 1024L * 1024L * 1024L * 1024L;
                        break;

                    default:

                        return def;
                }

                value = value.Substring(0, value.Length - 1);
            }

            if (long.TryParse(value, out result))
                return result * multiplier;
            else
                return def;
        }

        /// <summary>
        /// Parses an enumeration value where the value is case insenstive.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static object Parse(string value, System.Type enumType, object def)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException("[type] must be an enumeration.", "type");

            if (value == null)
                return def;

            try
            {
                return Enum.Parse(enumType, value, true);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses an enumeration value where the value is case insenstive.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type being parsed.</typeparam>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static TEnum Parse<TEnum>(string value, object def)
        {
            if (!typeof(TEnum).IsEnum)
                throw new ArgumentException("[TEnum] must be an enumeration.", "type");

            if (def == null || !typeof(TEnum).IsInstanceOfType(def))
                throw new ArgumentException("The default parameter type does not match the enumeration type.", "def");

            if (value == null)
                return (TEnum)def;

            try
            {
                return (TEnum)Enum.Parse(typeof(TEnum), value, true);
            }
            catch
            {
                return (TEnum)def;
            }
        }

        /// <summary>
        /// Parses an arbitrary structured type that implements <see cref="IParseable" />.
        /// </summary>
        /// <typeparam name="TValue">The resulting type.</typeparam>
        /// <param name="value">The string form of the type.</param>
        /// <param name="def">The default value to be returned if the configuration setting is <c>null</c> or invalid.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        public static TValue ParseCustom<TValue>(string value, TValue def)
            where TValue : IParseable, new()
        {
            try
            {
                var v = new TValue();

                if (v.TryParse(value))
                    return v;
                else
                    return def;
            }
            catch (Exception e)
            {
                // Handle this just to be safe.

                SysLog.LogException(e);
                return def;
            }
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static Uri Parse(string value, Uri def)
        {
            Uri uri;

            if (value == null || string.IsNullOrWhiteSpace(value))
                return def;

            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                return uri;
            else
                return def;
        }

        /// <summary>
        /// Renders the value passed as a string.
        /// </summary>
        /// <param name="uri">The <see cref="Uri" /> value.</param>
        /// <returns>The rendered value.</returns>
        public static string ToString(Uri uri)
        {
            return uri != null ? uri.ToString() : string.Empty;
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(bool v)
        {
            return v ? "1" : "0";
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <para>
        /// This method recognises the following boolean values:
        /// </para>
        /// <code language="none">
        /// False Values        True Values
        /// ------------        -----------
        ///     0                   1
        ///     no                  yes
        ///     off                 on
        ///     low                 high
        ///     false               true
        ///     disable             enable
        /// </code>
        /// </remarks>
        public static bool Parse(string value, bool def)
        {
            if (value == null)
                return def;

            switch (value.Trim().ToLowerInvariant())
            {

                case "0":
                case "no":
                case "off":
                case "low":
                case "false":
                case "disable":

                    return false;

                case "1":
                case "yes":
                case "on":
                case "true":
                case "high":
                case "enable":

                    return true;

                default:

                    return def;
            }
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(double v)
        {
            return v.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", "G", and "T" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, "G" multiples the value
        /// parsed by 1073741824, and "T" multiplies the value by 
        /// 1099511627776.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public static double Parse(string value, double def)
        {
            double  result;
            long    multiplier;
            char    last;

            if (value == null || value.Length == 0)
                return def;

            value = value.Trim();
            switch (value)
            {
                case "short.min":   return short.MinValue;
                case "short.max":   return short.MaxValue;
                case "ushort.max":  return ushort.MaxValue;
                case "int.min":     return int.MinValue;
                case "int.max":     return int.MaxValue;
                case "uint.max":    return uint.MaxValue;
                case "long.min":    return long.MinValue;
                case "long.max":    return long.MaxValue;
            }

            last = value[value.Length - 1];
            if (Char.IsDigit(last))
                multiplier = 1;
            else
            {
                switch (last)
                {
                    case 'k':
                    case 'K':

                        multiplier = 1024L;
                        break;

                    case 'm':
                    case 'M':

                        multiplier = 1024L * 1024L;
                        break;

                    case 'g':
                    case 'G':

                        multiplier = 1024L * 1024L * 1024L;
                        break;

                    case 't':
                    case 'T':

                        multiplier = 1024L * 1024L * 1024L * 1024L;
                        break;

                    default:

                        return def;
                }

                value = value.Substring(0, value.Length - 1);
            }

            if (double.TryParse(value, out result))
                return result * multiplier;
            else
                return def;
        }

        private enum Units
        {
            Days,
            Hours,
            Minutes,
            Seconds,
            Milliseconds
        }

        private static TimeSpan oneSec = TimeSpan.FromSeconds(1.0);
        private static TimeSpan oneMin = TimeSpan.FromMinutes(1.0);
        private static TimeSpan oneHour = TimeSpan.FromHours(1.0);
        private static TimeSpan oneDay = TimeSpan.FromDays(1.0);

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        /// <remarks>
        /// </remarks>
        /// <remarks>
        /// Timespan values are encoded as floating point values terminated with
        /// one of the unit codes: "d" (days), "h" (hours), "m" (minutes), "s"
        /// (seconds), or "ms" (milliseconds).  If the unit code is missing then 
        /// seconds will be assumed.  An infinite timespan is encoded using the 
        /// literal "infinite".
        /// </remarks>
        public static string ToString(TimeSpan v)
        {
            if (v == TimeSpan.Zero)
                return "0";
            else if (v < oneSec)
                return v.TotalMilliseconds.ToString() + "ms";
            else if (v < oneMin)
                return v.TotalSeconds.ToString() + "s";
            else if (v < oneHour)
                return v.TotalMinutes.ToString() + "m";
            else if (v < oneDay)
                return v.TotalHours.ToString() + "h";
            else if (v == TimeSpan.MaxValue)
                return "infinite";
            else
                return v.TotalDays.ToString() + "d";
        }

        /// <summary>
        /// Serializes the <see cref="TimeSpan" /> value passed as
        /// a time offset with days, hours, minutes, seconds, etc.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToTimeString(TimeSpan v)
        {
            string sign     = string.Empty;
            string days     = string.Empty;
            string hours    = string.Empty;
            string minutes  = string.Empty;
            string seconds  = string.Empty;
            string fraction = string.Empty;

            if (v < TimeSpan.Zero)
            {
                sign = "-";
                v = -v;
            }

            if (v.TotalDays >= 1)
                days = v.Days.ToString() + ".";

            hours = v.Hours.ToString("0#");
            minutes = string.Format(":{0:0#}", v.Minutes);

            if (v.Seconds > 0 || v.Milliseconds > 0)
            {
                seconds = string.Format(":{0:0#}", v.Seconds);

                if (v.Milliseconds > 0)
                    fraction = string.Format(".{0:0##}", v.Milliseconds);
            }

            return string.Format("{0}{1}{2}{3}{4}{5}", sign, days, hours, minutes, seconds, fraction);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <para>
        /// Timespan values are encoded as floating point values terminated with
        /// one of the unit codes: "d" (days), "h" (hours), "m" (minutes), "s"
        /// (seconds), or "ms" (milliseconds).  If the unit code is missing then 
        /// seconds will be assumed.  An infinite timespan is encoded using the 
        /// literal "infinite".
        /// </para>
        /// <para>
        /// Timespan values can also be specified as:
        /// </para>
        /// <para>
        /// <c>[ws][-]{ d | d.hh:mm[:ss[.ff]] | hh:mm[:ss[.ff]] }[ws]</c>
        /// </para>
        /// <para>where:</para>
        /// <list type="table">
        ///     <item>
        ///         <term>ws</term>
        ///         <definition>is whitespace</definition>
        ///     </item>
        ///     <item>
        ///         <term>d</term>
        ///         <definition>specifies days.</definition>
        ///     </item>
        ///     <item>
        ///         <term>hh</term>
        ///         <definition>specifies hours</definition>
        ///     </item>
        ///     <item>
        ///         <term>mm</term>
        ///         <definition>specifies minutes</definition>
        ///     </item>
        ///     <item>
        ///         <term>ss</term>
        ///         <definition>specifies seconds</definition>
        ///     </item>
        ///     <item>
        ///         <term>ff</term>
        ///         <definition>specifies fractional seconds</definition>
        ///     </item>
        /// </list>
        /// </remarks>
        public static TimeSpan Parse(string value, TimeSpan def)
        {

            Units   unit;
            int     len;
            int     trim;
            double  v;

            if (value == null)
                return def;

            value = value.Trim().ToLowerInvariant();
            if (value == "infinite")
                return TimeSpan.MaxValue;

            // Parse the d.hh:mm:ss form if a colon is present.

            if (value.IndexOf(':') != -1)
            {
                TimeSpan ts;

                if (!TimeSpan.TryParse(value, out ts))
                    return def;

                return ts;
            }

            // Extract the units

            len  = value.Length;
            trim = 1;

            if (value.Length <= 1)
            {
                unit = Units.Seconds;
                trim = 0;
            }
            else if (value.LastIndexOf("ms") == len - 2)
            {
                unit = Units.Milliseconds;
                trim = 2;
            }
            else if (value.LastIndexOf("s") == len - 1)
                unit = Units.Seconds;
            else if (value.LastIndexOf("m") == len - 1)
                unit = Units.Minutes;
            else if (value.LastIndexOf("h") == len - 1)
                unit = Units.Hours;
            else if (value.LastIndexOf("d") == len - 1)
                unit = Units.Days;
            else
            {
                unit = Units.Seconds;
                trim = 0;
            }

            if (trim > 0)
                value = value.Substring(0, len - trim);

            if (!double.TryParse(value, out v))
                return def;

            switch (unit)
            {
                case Units.Days:

                    return TimeSpan.FromDays(v);

                case Units.Hours:

                    return TimeSpan.FromHours(v);

                case Units.Minutes:

                    return TimeSpan.FromMinutes(v);

                case Units.Seconds:

                    return TimeSpan.FromSeconds(v);

                case Units.Milliseconds:

                    return TimeSpan.FromMilliseconds(v);

                default:

                    return def;
            }
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        /// <remarks>
        /// </remarks>
        /// <remarks>
        /// Dates are encoded as a .NET tick count.
        /// </remarks>
        public static string ToString(DateTime v)
        {
            return v.Ticks.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// This method first attempts to parse a date encoded as a .NET tick count,
        /// then as a date as described in RFC 1123, and if this fails, uses standard
        /// .NET date parser.
        /// </remarks>
        public static DateTime Parse(string value, DateTime def)
        {
            long ticks;

            if (long.TryParse(value, out ticks))
                return new DateTime(ticks);

            try
            {
                return Helper.ParseInternetDate(value);
            }
            catch
            {
                DateTime date;

                if (DateTime.TryParse(value, out date))
                    return date;
                else
                    return def;
            }
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        /// <remarks>
        /// IP addresses are formatted as &lt;dotted-quad&gt;
        /// </remarks>
        public static string ToString(IPAddress v)
        {
            return v.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// IP addresses are formatted as &lt;dotted-quad&gt;
        /// </remarks>
        public static IPAddress Parse(string value, IPAddress def)
        {
            IPAddress result;

            if (value == null)
                return def;

            if (Helper.TryParseIPAddress(value, out result))
                return result;
            else
                return def;
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        /// <remarks>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </remarks>
        public static string ToString(NetworkBinding v)
        {
            return v.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </remarks>
        public static NetworkBinding Parse(string value, NetworkBinding def)
        {
            if (value == null)
                return def;

            try
            {
                return NetworkBinding.Parse(value);
            }
            catch
            {

                return def;
            }
        }

        /// <summary>
        /// Serializes the value passed into a string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(Guid v)
        {
            return v.ToString();
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static Guid Parse(string value, Guid def)
        {
            if (value == null)
                return def;

            try
            {
                return new Guid(value);
            }
            catch
            {

                return def;
            }
        }

        /// <summary>
        /// Serializes the value passed into a HEX string.
        /// </summary>
        /// <param name="v">The value</param>
        /// <returns>The output string.</returns>
        public static string ToString(byte[] v)
        {
            return Helper.ToHex(v);
        }

        /// <summary>
        /// Parses the hex encoded string passed unless the string is <c>null</c>
        /// or the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static byte[] Parse(string value, byte[] def)
        {
            if (value == null)
                return def;

            try
            {
                return Helper.FromHex(value);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the base-64 encoded string passed unless the string is 
        /// null or the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static byte[] ParseBase64(string value, byte[] def)
        {

            if (value == null)
                return def;

            try
            {
                return Convert.FromBase64String(value);
            }
            catch
            {
                return def;
            }
        }

#if !MOBILE_DEVICE

        //---------------------------------------------------------------------
        // Serialization implementation note:
        //
        // I'm going to prefix the serialized blob with 32-bits of header
        // information.  The first 16-bits will be the magic number 0xAA51
        // and the second 16-bits will be used for flag/format bits.  One
        // bit is currently used to indicate whether the data is compressed,
        // one to indicate that a null graph, and the rest are reserved for 
        // future use and are set to 0.

        private const int Magic = 0xAA51;
        private const int Flag_Compress = 0x0001;
        private const int Flag_IsNull = 0x0002;

        /// <summary>
        /// Serializes the object graph to a byte array, optionally compressing
        /// the output.
        /// </summary>
        /// <param name="graph">The object graph.</param>
        /// <param name="compress">A <see cref="Compress" /> value indicating whether compression is to be performed.</param>
        /// <returns>The serialized data.</returns>
        public static byte[] ToBinary(object graph, Compress compress)
        {
            var ms = new MemoryStream(512);

            try
            {
                ToBinary(ms, graph, compress);
                return ms.ToArray();
            }
            finally
            {
                ms.Close();
            }
        }

        /// <summary>
        /// Serializes the object graph to a stream, optionally compressing
        /// the output.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="graph">The object graph.</param>
        /// <param name="compress">A <see cref="Compress" /> value indicating whether compression is to be performed.</param>
        /// <remarks>
        /// <note>It is possible to serialize <c>null</c> graphs using this method.</note>
        /// </remarks>
        public static void ToBinary(Stream output, object graph, Compress compress)
        {
            // Handle null graphs

            if (graph == null)
            {
                int flags = Flag_IsNull;

                output.WriteByte((byte)(Magic >> 8));
                output.WriteByte(unchecked((byte)Magic));
                output.WriteByte((byte)(flags >> 8));
                output.WriteByte((byte)flags);

                return;
            }

            // Handle serialization of real graphs

            var formatter = new BinaryFormatter();

            // Initalize the formatter

            formatter.AssemblyFormat = FormatterAssemblyStyle.Full;
            formatter.TypeFormat = FormatterTypeStyle.TypesAlways;

            if (compress == Compress.None || compress == Compress.Always)
            {
                int flags = compress == Compress.Always ? Flag_Compress : 0;

                // Write the header

                output.WriteByte((byte)(Magic >> 8));
                output.WriteByte(unchecked((byte)Magic));
                output.WriteByte((byte)(flags >> 8));
                output.WriteByte((byte)flags);

                // Serialize

                if (compress == Compress.Always)
                {
                    using (DeflateStream ds = new DeflateStream(output, CompressionMode.Compress, true))
                        formatter.Serialize(ds, graph);
                }
                else
                    formatter.Serialize(output, graph);
            }
            else
            {
                // We need to serialize the graph to a memory stream and then 
                // decide whether to use the compressed or uncompressed form
                // based on which is smaller.

                using (MemoryStream msUncompressed = new MemoryStream(1024))
                {
                    Stream  serialized;
                    int     flags = 0;

                    formatter.Serialize(msUncompressed, graph);
                    msUncompressed.Position = 0;

                    using (MemoryStream msCompressed = new MemoryStream((int)msUncompressed.Length / 2))
                    {
                        using (DeflateStream ds = new DeflateStream(msCompressed, CompressionMode.Compress, true))
                        {
                            EnhancedStream.Copy(msUncompressed, ds, int.MaxValue);
                            ds.Flush();
                        }

                        if (msCompressed.Length < msUncompressed.Length)
                        {
                            flags |= Flag_Compress;
                            serialized = msCompressed;
                        }
                        else
                        {

                            serialized = msUncompressed;
                        }

                        // Write the header

                        output.WriteByte((byte)(Magic >> 8));
                        output.WriteByte(unchecked((byte)Magic));
                        output.WriteByte((byte)(flags >> 8));
                        output.WriteByte((byte)flags);

                        // Write the serialized data

                        serialized.Position = 0;
                        EnhancedStream.Copy(serialized, output, int.MaxValue);
                    }
                }
            }
        }

        /// <summary>
        /// Deserializes an object graph from a byte array.
        /// </summary>
        /// <param name="input">The input data.</param>
        /// <returns>The deserialized graph.</returns>
        public static object FromBinary(byte[] input)
        {

            var ms = new MemoryStream(input);

            try
            {
                return FromBinary(ms);
            }
            finally
            {
                ms.Close();
            }
        }

        /// <summary>
        /// Deserializes an object graph from a stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The deserialized graph.</returns>
        public static object FromBinary(Stream input)
        {
            var formatter = new BinaryFormatter();
            int magic;
            int flags;

            // Initalize the formatter

            formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;
            formatter.TypeFormat = FormatterTypeStyle.TypesAlways;

            // Read the header

            magic = input.ReadByte() << 8;
            magic |= input.ReadByte();
            if (magic != Magic)
                throw new FormatException("Invalid serialized object header.");

            flags = input.ReadByte() << 8;
            flags |= input.ReadByte();

            if ((flags & Flag_IsNull) != 0)
                return null;

            if ((flags & Flag_Compress) != 0)
            {
                using (DeflateStream ds = new DeflateStream(input, CompressionMode.Decompress, true))
                {
                    return formatter.Deserialize(ds);
                }
            }
            else
                return formatter.Deserialize(input);
        }

        /// <summary>
        /// Serializes an object graph to XML. 
        /// </summary>
        /// <param name="graph">The object graph.</param>
        /// <returns>The serialized XML.</returns>
        /// <remarks>
        /// <note>
        /// This method uses the <see cref="XmlSerializer" /> class to serialize the graph.
        /// </note>
        /// </remarks>
        public static string ToXml(object graph)
        {
            var serializer = new XmlSerializer(graph.GetType(), "");
            var sb         = new StringBuilder(128);
            var writer     = XmlTextWriter.Create(sb, xmlSettings);

            serializer.Serialize(writer, graph, xmlNamespaces);
            writer.Close();
            return sb.ToString();
        }

        /// <summary>
        /// Serializes an object graph to XML. 
        /// </summary>
        /// <param name="writer">The XML output will be written here.</param>
        /// <param name="graph">The object graph.</param>
        /// <remarks>
        /// <note>
        /// This method uses the <see cref="XmlSerializer" /> class to serialize the graph.
        /// </note>
        /// </remarks>
        public static void ToXml(TextWriter writer, object graph)
        {
            new XmlSerializer(graph.GetType(), "").Serialize(XmlTextWriter.Create(writer, xmlSettings), graph, xmlNamespaces);
        }

        /// <summary>
        /// Deserializes an object graph from XML in a string.
        /// </summary>
        /// <param name="xmlText">The XML text.</param>
        /// <param name="type">The type of the root object in the graph.</param>
        /// <returns>The deserialized object.</returns>
        /// <remarks>
        /// <note>
        /// This method uses the <see cref="XmlSerializer" /> class to deserialize the graph.
        /// </note>
        /// </remarks>
        public static object FromXml(string xmlText, System.Type type)
        {
            return new XmlSerializer(type, "").Deserialize(new StringReader(xmlText));
        }

        /// <summary>
        /// Deserializes an object graph from XML read by a <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader" /> with the XML.</param>
        /// <param name="type">The type of the root object in the graph.</param>
        /// <returns>The deserialized object.</returns>
        /// <remarks>
        /// <note>
        /// This method uses the <see cref="XmlSerializer" /> class to deserialize the graph.
        /// </note>
        /// </remarks>
        public static object FromXml(TextReader reader, System.Type type)
        {
            return new XmlSerializer(type, "").Deserialize(reader);
        }

#endif // !MOBILE_DEVICE

#if !XAMARIN

        /// <summary>
        /// Uses a <see cref="DataContractSerializer" /> to serialize an instance to XML.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="value">The object instance.</param>
        /// <returns>The XML text.</returns>
        /// <remarks>
        /// <note>
        /// This method uses the <see cref="DataContractSerializer" /> class to serialize the graph.
        /// </note>
        /// </remarks>
        public static string ToXml<T>(T value)
        {
            var serializer = new DataContractSerializer(typeof(T));
            var sb        = new StringBuilder(2048);

            using (var writer = XmlWriter.Create(new StringWriter(sb), xmlSettings))
                serializer.WriteObject(writer, value);

            return sb.ToString();
        }

        /// <summary>
        /// Parses an object instance from XML using a <see cref="DataContractSerializer" />.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="xmlText">The XML text.</param>
        /// <returns>The parsed instance.</returns>
        /// <remarks>
        /// <note>
        /// This method uses the <see cref="DataContractSerializer" /> class to deserialize the graph.
        /// </note>
        /// </remarks>
        public static T FromXml<T>(string xmlText)
        {
            var serializer = new DataContractSerializer(typeof(T));

            using (var reader = XmlReader.Create(new StringReader(xmlText)))
                return (T)serializer.ReadObject(reader);
        }

#endif // !XAMARIN
    }
}
