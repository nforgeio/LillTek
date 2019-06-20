//-----------------------------------------------------------------------------
// FILE:        SqlHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements useful SQL utilitiy methods.

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Diagnostics;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Implements useful SQL utilitiy methods.
    /// </summary>
    public static class SqlHelper
    {
        /// <summary>
        /// Determines whether a database result field is <c>null</c>.
        /// </summary>
        /// <param name="field">The field to be tested.</param>
        /// <returns><c>true</c> if the field is <c>null</c>.</returns>
        public static bool IsNull(object field)
        {
            return field is DBNull;
        }

        /// <summary>
        /// Returns the result set object as a string, handling the
        /// type and possible <see cref="DBNull" /> conversion.
        /// </summary>
        /// <param name="field">The result set or table field.</param>
        /// <returns>The string value or <c>null</c>.</returns>
        public static string AsString(object field)
        {
            if (field is DBNull)
                return null;
            else
                return (string)field;
        }

        /// <summary>
        /// Renders a <see cref="DateTime" /> value into a SQL compatible string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The rendered string.</returns>
        public static string AsString(DateTime value)
        {
            value = SqlHelper.ValidDate(value);

            return string.Format("{0}-{1}-{2} {3}:{4}:{5}.{6:0##}", value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond);
        }

        /// <summary>
        /// Returns the result set object as a byte array, handling the
        /// type and possible <see cref="DBNull" /> conversion.
        /// </summary>
        /// <param name="field">The result set or table field.</param>
        /// <returns>The byte array or <c>null</c>.</returns>
        public static byte[] AsBytes(object field)
        {
            if (field is DBNull)
                return null;
            else
                return (byte[])field;
        }

        /// <summary>
        /// Casts a result set value into the specified enumeration type.
        /// </summary>
        /// <typeparam name="T">The desired enumeration type.</typeparam>
        /// <param name="field">The field value (should be an integer).</param>
        /// <returns>The cast enumeration value.</returns>
        /// <exception cref="ArgumentException">Thrown if the type passed is not an enumeration.</exception>
        /// <exception cref="InvalidCastException">Thrown if the field cannot be cast into the enumeration.</exception>
        public static T AsEnum<T>(object field) where T : struct
        {
            int             v;
            System.Type     type;

            type = typeof(T);
            if (!type.IsEnum)
                throw new ArgumentException(string.Format("Type [{0}] is not an enumeration type.", type.Name));

            if (field == null || field is DBNull)
                throw new InvalidCastException("Field cannot be  null.");

            if (typeof(byte).IsInstanceOfType(field))
                v = (int)(byte)field;
            else if (typeof(int).IsInstanceOfType(field))
                v = (int)field;
            else if (typeof(sbyte).IsInstanceOfType(field))
                v = (int)(sbyte)field;
            else if (typeof(uint).IsInstanceOfType(field))
                v = (int)(uint)field;
            else if (typeof(short).IsInstanceOfType(field))
                v = (int)(short)field;
            else if (typeof(ushort).IsInstanceOfType(field))
                v = (int)(ushort)field;
            else
                throw new InvalidCastException(string.Format("Field type [{0}] cannot be cast into an enumeration.", field.GetType().Name));

            return (T)Enum.ToObject(type, v);
        }

        /// <summary>
        /// Converts an integer value into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal.</returns>
        public static string Literal(int value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Converts a long value into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal.</returns>
        public static string Literal(long value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Converts a floating point value into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal.</returns>
        public static string Literal(double value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Converts a boolean value into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal.</returns>
        public static string Literal(bool value)
        {
            return value ? "1" : "0";
        }

        /// <summary>
        /// Converts a <see cref="Guid" /> value into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal.</returns>
        public static string Literal(Guid value)
        {
            return string.Format("'{0}'", value.ToString("D"));
        }

        /// <summary>
        /// Converts a date/time value into a T-SQL literal string, including the
        /// surrounding quotes.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal string.</returns>
        public static string Literal(DateTime value)
        {
            return string.Format("'{0:0###}-{1:0#}-{2:0#} {3:0#}:{4:0#}:{5:0#}.{6:0##}'",
                                 value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond);
        }

        /// <summary>
        /// Converts a date/time offset value into a T-SQL literal string, including the
        /// surrounding quotes.
        /// </summary>
        /// <param name="value">The value to be converted into a literal.</param>
        /// <returns>The T-SQL literal string.</returns>
        public static string Literal(DateTimeOffset value)
        {
            var offset = value.Offset;

            if (offset == TimeSpan.Zero)
                return Literal(value.DateTime);
            else
                return string.Format("'{0:0###}-{1:0#}-{2:0#} {3:0#}:{4:0#}:{5:0#}.{6:0##} {7}{8:0#}:{9:0#}'",
                                     value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond,
                                     offset < TimeSpan.Zero ? '-' : '+', Math.Abs(offset.Hours), Math.Abs(offset.Minutes));
        }

        /// <summary>
        /// Converts a string value into a T-SQL literal, adding
        /// the surrounding quotes as well as any necessary escape
        /// sequences.
        /// </summary>
        /// <param name="value">The value to be converted into a literal string.</param>
        /// <returns>The T-SQL literal string.</returns>
        public static string Literal(string value)
        {
            var sb = new StringBuilder(value.Length + 10);

            if (value.Length == 0)
                return "' '";

            sb.Append('\'');

            for (int i = 0; i < value.Length; i++)
            {
                var ch = value[i];

                switch (ch)
                {
                    case '\'':

                        sb.Append("''");
                        break;

                    case '"':

                        sb.Append("\"");
                        break;

                    case '\a':

                        sb.Append("\\a");
                        break;

                    case '\b':

                        sb.Append("\\b");
                        break;

                    case '\f':

                        sb.Append("\\f");
                        break;

                    case '\t':

                        sb.Append("\\t");
                        break;

                    case '\n':

                        sb.Append("\\n");
                        break;

                    case '\r':

                        sb.Append("\\r");
                        break;

                    case '\v':

                        sb.Append("\\v");
                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            sb.Append('\'');

            return sb.ToString();
        }

        /// <summary>
        /// Converts the byte array passed into a T-SQL literal.
        /// </summary>
        /// <param name="data">The data to be converted.</param>
        /// <returns>A string of the form: 0x&lt;hex string&gt;.</returns>
        public static string Literal(byte[] data)
        {
            return "0x" + Helper.ToHex(data);
        }

        /// <summary>
        /// Converts a string containing <b>?</b> and/or <b>*</b> wildcard
        /// characters into the equivalent SQL/Server <b>LIKE</b> expression,
        /// where <b>?</b> matches any single character and <b>*</b> matches
        /// zero or more characters.
        /// </summary>
        /// <param name="pattern">The wildcarded pattern to be converted.</param>
        /// <returns>The equivalent <b>LIKE</b> expression.</returns>
        public static string LikeWildcard(string pattern)
        {
            // Escape characters as necessary in the pattern.

            pattern = pattern.Replace("%", @"\%");
            pattern = pattern.Replace("_", @"\_");
            pattern = pattern.Replace("[", @"\[");
            pattern = pattern.Replace("]", @"\]");
            pattern = pattern.Replace("^", @"\^");

            // Convert the wildcards

            pattern = pattern.Replace('*', '%');
            pattern = pattern.Replace('?', '_');

            return pattern;
        }

        /// <summary>
        /// The minimum valid SQL Server date.
        /// </summary>
        public static readonly DateTime MinDate = Helper.SqlMinDate;

        /// <summary>
        /// The maximum possible SQL Server date.
        /// </summary>
        public static readonly DateTime MaxDate = Helper.SqlMaxDate;

        /// <summary>
        /// Ensures that the <see cref="DateTime" /> value passed is within the limits
        /// required by Microsoft SQL/Server.
        /// </summary>
        /// <param name="date">The date to be validated.</param>
        /// <returns>The validated and potentially adjusted <see cref="DateTime" /> value.</returns>
        /// <remarks>
        /// SQL/Server accepts a more limited date range than is supported by the .NET
        /// Framework where dates must be in the range of 1/1/1735 to 12/31/9999.  This
        /// method adjusts the date passed by ensuring that it doesn't exceed these limits.
        /// </remarks>
        public static DateTime ValidDate(DateTime date)
        {
            if (date < MinDate)
                return MinDate;
            else if (date > MaxDate)
                return MaxDate;
            else
                return date;
        }
    }
}
