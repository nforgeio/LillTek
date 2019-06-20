//-----------------------------------------------------------------------------
// FILE:        TextProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes common properties for drawing text.

using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Text;

namespace LillTek.Common
{
#if WINFULL

    /// <summary>
    /// Describes common properties for drawing text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is typically used for serializing common settings such as font, size,
    /// style, color used when drawing text.
    /// </para>
    /// <para>
    /// Seralized text settings have the form:
    /// </para>
    /// <example>
    /// <![CDATA[ 
    /// <font name>,<size>,<unit>,<color>,<style>
    /// ]]>
    /// </example>
    /// <para>
    /// where <b>font name</b> identifies the font, <b>size</b> is the integer font size,
    /// <b>unit</b> describes the measurement unit as either <b>Pixel</b> or <b>Point</b>,
    /// <b>color</b> is a known color name or HEX RGB/RGBA color code, and style is
    /// <b>Bold</b>, <b>Italic</b>, <b>Regular</b>, <b>Strikeout</b>, or <b>Underline</b>
    /// where multiple styles can be specified by separating them with the <b>"+"</b>
    /// character.
    /// </para>
    /// <para>
    /// Here are some examples:
    /// </para>
    /// <example>
    /// Tahoma,12,Pixel,Black,Regular
    /// Tahoma,12,Pixel,#000000,Bold
    /// Tahoma,12,Pixel,#FF000000,Bold+Italic
    /// </example>
    /// </remarks>
    public class TextProperties
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Attempts to parse a text settings string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="settings">Returns as the text settings.</param>
        /// <returns><c>true</c> if the settings were parsed successfully.</returns>
        /// <remarks>
        /// <para>
        /// Seralized text settings have the form:
        /// </para>
        /// <example>
        /// <![CDATA[ 
        /// <font name>,<size>,<unit>,<color>,<style>
        /// ]]>
        /// </example>
        /// <para>
        /// where <b>font name</b> identifies the font, <b>size</b> is the integer font size,
        /// <b>unit</b> describes the measurement unit as either <b>Pixel</b> or <b>Point</b>,
        /// <b>color</b> is a known color name or HEX RGB/RGBA color code, and style is
        /// <b>Bold</b>, <b>Italic</b>, <b>Regular</b>, <b>Strikeout</b>, or <b>Underline</b>
        /// where multiple styles can be specified by separating them with the <b>"+"</b>
        /// character.
        /// </para>
        /// <para>
        /// Here are some examples:
        /// </para>
        /// <example>
        /// Tahoma,12,Pixel,Black,Regular
        /// Tahoma,12,Pixel,#000000,Bold
        /// Tahoma,12,Pixel,#FF000000,Bold+Italic
        /// </example>
        /// </remarks>
        public static bool TryParse(string input, out TextProperties settings)
        {
            settings = new TextProperties();

            if (settings.Parse(input) != null)
            {

                settings = null;
                return false;
            }

            return true;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns the name of the font.
        /// </summary>
        public string FontName { get; private set; }

        /// <summary>
        /// Returns the font size in pixels.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Returns the measurement unit for the font size.
        /// </summary>
        public GraphicsUnit Unit { get; private set; }

        /// <summary>
        /// Returns the foreground color.
        /// </summary>
        public Color Color { get; private set; }

        /// <summary>
        /// Returns the font style.
        /// </summary>
        public FontStyle Style { get; private set; }

        /// <summary>
        /// Private constructor.
        /// </summary>
        private TextProperties()
        {
        }

        /// <summary>
        /// Constructs an instance by parsing the input string.
        /// </summary>
        /// <param name="input">The serialized text settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="input" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the settings string passed is not valid.</exception>
        /// <remarks>
        /// <para>
        /// Seralized text settings have the form:
        /// </para>
        /// <example>
        /// <![CDATA[ 
        /// <font name>,<size>,<unit>,<color>,<style>
        /// ]]>
        /// </example>
        /// <para>
        /// where <b>font name</b> identifies the font, <b>size</b> is the integer font size,
        /// <b>unit</b> describes the measurement unit as either <b>Pixel</b> or <b>Point</b>,
        /// <b>color</b> is a known color name or HEX RGB/RGBA color code, and style is
        /// <b>Bold</b>, <b>Italic</b>, <b>Regular</b>, <b>Strikeout</b>, or <b>Underline</b>
        /// where multiple styles can be specified by separating them with the <b>"+"</b>
        /// character.
        /// </para>
        /// <para>
        /// Here are some examples:
        /// </para>
        /// <example>
        /// Tahoma,12,Pixel,Black,Regular
        /// Tahoma,12,Pixel,#000000,Bold
        /// Tahoma,12,Pixel,#FF000000,Bold+Italic
        /// </example>
        /// </remarks>
        public TextProperties(string input)
        {
            if (input == null)
                throw new ArgumentException("input");

            var error = Parse(input);

            if (error != null)
                throw new ArgumentException("input", error);
        }

        /// <summary>
        /// Constructs an instance from parameters.
        /// </summary>
        /// <param name="fontName">The font name.</param>
        /// <param name="size">The font size.</param>
        /// <param name="unit">The measurement unit for the font size.</param>
        /// <param name="color">The forground color</param>
        /// <param name="style">The font style.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fontName" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="size" /> is zero or negative.</exception>
        public TextProperties(string fontName, int size, GraphicsUnit unit, Color color, FontStyle style)
        {
            if (string.IsNullOrWhiteSpace(fontName))
                throw new ArgumentNullException("fontName", "[fontName] cannot be NULL or empty.");

            if (size <= 0)
                throw new ArgumentException("size", "[size] cannot be zero or negative.");

            this.FontName = fontName;
            this.Unit     = unit;
            this.Size     = size;
            this.Color    = color;
            this.Style    = style;
        }

        /// <summary>
        /// Attempts to parse the settings.
        /// </summary>
        /// <param name="input">The serialized text settings.</param>
        /// <returns><c>null</c> if the settings were parsed successfully, an error message otherwise.</returns>
        private string Parse(string input)
        {
            GraphicsUnit    unit;
            Color           color;

            if (input == null)
                return "[TextProperties] input string is NULL.";

            var fields = input.Split(',');

            if (fields.Length != 5)
                return "[TextProperties] input string requires five comma separated values.";

            this.FontName = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(this.FontName))
                return "[TextProperties] has empty font name.";

            int size;

            if (!int.TryParse(fields[1].Trim(), out size) || size <= 0)
                return "[TextProperties] has invalid font size.";

            this.Size = size;

            if (!Enum.TryParse<GraphicsUnit>(fields[2].Trim(), out unit))
                return "[TextProperties] has an invalid font size unit.";

            this.Unit = unit;

            if (!Helper.TryParseColor(fields[3].Trim(), out color))
                return "[TextProperties] has an invalid color.";

            this.Color = color;

            foreach (var styleString in fields[4].Split('+'))
            {
                FontStyle s;

                if (!Enum.TryParse<FontStyle>(styleString.Trim(), out s))
                    return "[TextProperties] has an invalid font style.";

                this.Style |= s;
            }

            return null;
        }

        /// <summary>
        /// Serializes the settings to a string.
        /// </summary>
        /// <returns>The settings string.</returns>
        public override string ToString()
        {
            string styleString;

            switch (Style)
            {
                case FontStyle.Bold:

                    styleString = "Bold";
                    break;

                case FontStyle.Italic:

                    styleString = "Italic";
                    break;

                case FontStyle.Regular:

                    styleString = "Regular";
                    break;

                case FontStyle.Strikeout:

                    styleString = "Strikeout";
                    break;

                case FontStyle.Underline:

                    styleString = "Underline";
                    break;

                default:

                    // Must be a bitwise OR of multiple styles.

                    var sb = new StringBuilder(32);

                    if ((Style & FontStyle.Bold) != 0)
                        sb.Append("Bold");

                    if ((Style & FontStyle.Italic) != 0)
                    {

                        if (sb.Length > 0)
                            sb.Append("+Italic");
                        else
                            sb.Append("Italic");
                    }

                    if ((Style & FontStyle.Strikeout) != 0)
                    {

                        if (sb.Length > 0)
                            sb.Append("+Strikeout");
                        else
                            sb.Append("Strikeout");
                    }

                    if ((Style & FontStyle.Underline) != 0)
                    {

                        if (sb.Length > 0)
                            sb.Append("+Underline");
                        else
                            sb.Append("Underline");
                    }

                    styleString = sb.ToString();
                    break;
            }

            return string.Format("{0},{1},{2},#{3:x8},{4}", FontName, Size, Unit, Color.ToArgb(), styleString);
        }

        /// <summary>
        /// Creates a <see cref="Font" /> instance based on the settings.
        /// </summary>
        /// <returns>The font.</returns>
        public Font CreateFont()
        {
            return new Font(this.FontName, this.Size, this.Style);
        }
    }

#endif // WINFULL
}
