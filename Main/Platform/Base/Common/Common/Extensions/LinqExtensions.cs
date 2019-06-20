//-----------------------------------------------------------------------------
// FILE:        LinqExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: LINQ extension methods.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace LillTek.Common
{
    /// <summary>
    /// LINQ extension methods.
    /// </summary>
    public static class LinqExtensions
    {
        /// <summary>
        /// Returns the first <see cref="XElement" /> found at the specified path
        /// rooted at the current XML element.
        /// </summary>
        /// <param name="container">
        /// The <see cref="XContainer" /> where the search is to start.
        /// </param>
        /// <param name="path">
        /// The path of element names separated by forward slashes (/).
        /// </param>
        /// <returns>The <see cref="XElement" /> found or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// This method uses an empty XML namespace.
        /// </note>
        /// </remarks>
        public static XElement ElementPath(this XContainer container, string path)
        {
            return ElementPath(container, XNamespace.None, path);
        }

        /// <summary>
        /// Returns the first <see cref="XElement" /> found at the specified path
        /// rooted at the current XML element.
        /// </summary>
        /// <param name="container">
        /// The <see cref="XContainer" /> where the search is to start.
        /// </param>
        /// <param name="ns">The <see cref="XNamespace" /> to be used when resolving names.</param>
        /// <param name="path">
        /// The path of element names separated by forward slashes (/).
        /// </param>
        /// <returns>The <see cref="XElement" /> found or <c>null</c>.</returns>
        public static XElement ElementPath(this XContainer container, XNamespace ns, string path)
        {
            var names = path.Split('/');

            for (int i = 0; i < names.Length; i++)
            {
                string name;

                name = names[i].Trim();
                if (name == string.Empty)
                    continue;

                container = (XElement)container.Element(ns + name);
                if (container == null)
                    return null;
            }

            return (XElement)container;
        }

        /// <summary>
        /// Returns the child <see cref="XElement" /> of the <paramref name="container" /> with the 
        /// <paramref name="name" /> specified.
        /// </summary>
        /// <param name="container">The parent <see cref="XContainer" />.</param>
        /// <param name="name">The desired <see cref="XElement" /> name.</param>
        /// <returns>The matching child element.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the container does not have exactly one child element with this name.</exception>
        public static XElement SingleElement(this XContainer container, XName name)
        {
            return container.Elements(name).Single();
        }

        /// <summary>
        /// Returns the child <see cref="XAttribute" /> of the <paramref name="element" /> with the 
        /// <paramref name="name" /> specified.
        /// </summary>
        /// <param name="element">The parent <see cref="XElement" />.</param>
        /// <param name="name">The desired <see cref="XElement" /> name.</param>
        /// <returns>The matching child attribute.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the container does not have exactly one child attribute with this name.</exception>
        public static XAttribute SingleAttribute(this XElement element, XName name)
        {
            return element.Attributes(name).Single();
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the specified
        /// <see cref="XName" /> and then parses and returns its value as a string.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static string ParseAttribute(this XElement element, XName name, string def)
        {
            var attribute = element.Attribute(name);

            if (attribute == null)
                return def;

            return attribute.Value.Trim();
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// <see cref="int" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static int ParseAttribute(this XElement element, XName name, int def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// <see cref="long" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static long ParseAttribute(this XElement element, XName name, long def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a case insensitive enumeration of type <typeparamref name="TEnum" />.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type being parsed.</typeparam>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static TEnum ParseAttribute<TEnum>(this XElement element, XName name, object def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse<TEnum>(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as 
        /// a value of type <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type being parsed.</typeparam>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static TValue ParseCustomAttribute<TValue>(this XElement element, XName name, TValue def)
            where TValue : IParseable, new()
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.ParseCustom<TValue>(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="Uri" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static Uri ParseAttribute(this XElement element, XName name, Uri def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            if (value == null)
                return def;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="bool" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static bool ParseAttribute(this XElement element, XName name, bool def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="bool" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static double ParseAttribute(this XElement element, XName name, double def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static TimeSpan ParseAttribute(this XElement element, XName name, TimeSpan def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="DateTime" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Dates are encoded into strings as described in RFC 1123.
        /// </remarks>
        public static DateTime ParseAttribute(this XElement element, XName name, DateTime def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// an <see cref="IPAddress" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// IP addresses are formatted as &lt;dotted-quad&gt;
        /// </remarks>
        public static IPAddress ParseAttribute(this XElement element, XName name, IPAddress def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// an <see cref="NetworkBinding" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </remarks>
        public static NetworkBinding ParseAttribute(this XElement element, XName name, NetworkBinding def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// <see cref="Guid" />.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static Guid ParseAttribute(this XElement element, XName name, Guid def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// hex encoded byte array.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static byte[] ParseAttribute(this XElement element, XName name, byte[] def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XElement" /> for the first attribute instance with the
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// base-64 encoded byte array.
        /// </summary>
        /// <param name="element">The <see cref="XElement" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static byte[] ParseAttributeBase64(this XElement element, XName name, byte[] def)
        {
            var attribute = element.Attribute(name);
            var value     = attribute != null ? attribute.Value : null;

            return Serialize.ParseBase64(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// <see cref="XName" /> and then parses and returns its value as a string.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static string ParseElement(this XContainer container, XName name, string def)
        {
            var element = container.Element(name);

            if (element == null)
                return def;

            return element.Value.Trim();
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// <see cref="int" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static int ParseElement(this XContainer container, XName name, int def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// <see cref="long" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static long ParseElement(this XContainer container, XName name, long def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a case insensitive enumeration of type <typeparamref name="TEnum" />.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type being parsed.</typeparam>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static TEnum ParseElement<TEnum>(this XContainer container, XName name, object def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse<TEnum>(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as 
        /// a value of type <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type being parsed.</typeparam>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static TValue ParseCustomElement<TValue>(this XContainer container, XName name, TValue def)
            where TValue : IParseable, new()
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.ParseCustom<TValue>(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="Uri" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static Uri ParseElement(this XContainer container, XName name, Uri def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            if (value == null)
                return def;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="bool" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static bool ParseElement(this XContainer container, XName name, bool def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="bool" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static double ParseElement(this XContainer container, XName name, double def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
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
        public static TimeSpan ParseElement(this XContainer container, XName name, TimeSpan def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// a <see cref="DateTime" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Dates are encoded into strings as described in RFC 1123.
        /// </remarks>
        public static DateTime ParseElement(this XContainer container, XName name, DateTime def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as
        /// an <see cref="IPAddress" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// IP addresses are formatted as &lt;dotted-quad&gt;
        /// </remarks>
        public static IPAddress ParseElement(this XContainer container, XName name, IPAddress def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// an <see cref="NetworkBinding" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </remarks>
        public static NetworkBinding ParseElement(this XContainer container, XName name, NetworkBinding def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// <see cref="Guid" />.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static Guid ParseElement(this XContainer container, XName name, Guid def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// hex encoded byte array.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static byte[] ParseElement(this XContainer container, XName name, byte[] def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Searches a <see cref="XContainer" /> for the first <see cref="XElement" /> instance with the specified
        /// specified <see cref="XName" /> and then parses and returns its value as a
        /// base-64 encoded byte array.
        /// </summary>
        /// <param name="container">The <see cref="XContainer" /></param>
        /// <param name="name">The attribute <see cref="XName" />.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public static byte[] ParseElementBase64(this XContainer container, XName name, byte[] def)
        {
            var element = container.Element(name);
            var value   = element != null ? element.Value : null;

            return Serialize.ParseBase64(value, def);
        }
    }
}
