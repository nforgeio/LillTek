//-----------------------------------------------------------------------------
// FILE:        JsonSerializer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a simple JSON object serializer.

using System;
using System.IO;
using System.Text;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Json
{
    /// <summary>
    /// Implements a simple JSON object serializer using type reflection.
    /// </summary>
    /// <remarks>
    /// This is a very thin wrapper over the NewtonSoft open source library.
    /// </remarks>
    public static class JsonSerializer
    {
        /// <summary>
        /// Reads an object of the specified type from a <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader" />.</param>
        /// <param name="type">The object type.</param>
        /// <returns>The unserialized object.</returns>
        /// <exception cref="JsonException">Throw on deserialization errors.</exception>
        public static object Read(TextReader reader, System.Type type)
        {
            Internal.JsonSerializer     serializer;
            Internal.JsonReader         jsonReader;

            try
            {
                jsonReader = new Internal.JsonReader(reader);
                serializer = new Internal.JsonSerializer();
                return serializer.Deserialize(jsonReader, type);
            }
            catch (Exception e)
            {
                throw new JsonException(e.Message, e);
            }
        }

        /// <summary>
        /// Reads an object of the specified type from a string.
        /// </summary>
        /// <param name="input">The serialized object.</param>
        /// <param name="type">The object type.</param>
        /// <returns>The unserialized object.</returns>
        /// <exception cref="JsonException">Throw on deserialization errors.</exception>
        public static object Read(string input, System.Type type)
        {
            var reader = new StringReader(input);

            try
            {
                return Read(reader, type);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Serializes an object to a <see cref="TextWriter" />.
        /// </summary>
        /// <param name="writer">The <see cref="TextWriter" />.</param>
        /// <param name="value">The object to be written.</param>
        /// <exception cref="JsonException">Throw on serialization errors.</exception>
        public static void Write(TextWriter writer, object value)
        {
            Internal.JsonSerializer serializer;

            try
            {
                serializer = new Internal.JsonSerializer();
                serializer.Serialize(writer, value);
            }
            catch (Exception e)
            {
                throw new JsonException(e.Message, e);
            }
        }

        /// <summary>
        /// Serializes an object to a string.
        /// </summary>
        /// <param name="value">The object to be written.</param>
        /// <returns>The serializes object.</returns>
        /// <exception cref="JsonException">Throw on serialization errors.</exception>
        public static string ToString(object value)
        {
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            try
            {
                Write(writer, value);
            }
            finally
            {
                writer.Close();
            }

            return sb.ToString();
        }
    }
}
