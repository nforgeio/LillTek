//-----------------------------------------------------------------------------
// FILE:        Config.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements access to application specific configuration information.

using System;
using System.IO;

#if SILVERLIGHT
using System.IO.IsolatedStorage;
#endif

using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

// $todo(jeff.lill): Implement support for encrypted configuration settings

namespace LillTek.Common
{
#if MOBILE_DEVICE
    /// <summary>
    /// Abstracts access to the application configuration settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class accesses settings from a standard XML based configuration
    /// file for WINFULL .NET Framework applications as well as from a
    /// name/value text file for both WINFULL and WINCE implementations.
    /// </para>
    /// <para>
    /// The compact framework does not implement the ConfigurationSettings
    /// class (probably to avoid having to load the XML DOM classes).  This
    /// class implementation will load the UTF8 encoded configuration file named
    /// with the same as the file name of the currently executing process
    /// with the extension replaced by ".ini" for normal processes or the
    /// <b>Web.ini</b> file in the root folder of websites.  Due to limitations
    /// in the  Compact Framework, the WINCE constructors must be passed a reference 
    /// to the assembly for the main application entrypoint.
    /// </para>
    /// <para>
    /// The LillTek configuration system has evolved over the past several years
    /// from a simple name/value text file to deal with the lack of configuration
    /// support on WINCE into an advanced format that supports macros, conditions,
    /// sections, environment variables, and pluggable configuration providers. 
    /// Most LillTek applications now use this class is their primary means of 
    /// managing application specific configuration settings.
    /// </para>
    /// <para>
    /// The format of the config file is simply a series of configuration 
    /// name/value pairs, one to a line, formatted as:
    /// </para>
    /// <para>
    /// <code language="none">
    /// name=value
    /// </code>
    /// </para>
    /// <para>
    /// Extra whitespace will be trimmed from both ends of these strings.
    /// Note that keys and values are case sensitive.  Multi-line values
    /// can be specified surrounding the value with "{{" and "}}" where the
    /// terminating "}}" must appear on a line by itself:
    /// </para>
    /// <code language="none">
    /// name = {{
    ///     line1
    ///     line2
    ///     line3
    /// }}
    /// </code>
    /// <para>
    /// Note that each line of a multi-line value will be trimmed of whitespace
    /// at the beginning and end of the line.
    /// </para>
    /// <para>
    /// The <b>#section</b> and <b>#endsection</b> commands can be used to 
    /// specify a configuration key prefix:
    /// </para>
    /// <code language="none">
    /// #section MyApplication
    /// 
    /// Name1 = value
    /// Name2 = value
    /// 
    /// #endsection
    /// 
    /// // The definitions above are equivalant to:
    /// 
    /// MyApplication.Name1 = value
    /// MyApplication.Name2 = value
    /// 
    /// // #section commands can also be nested:
    /// 
    /// #section Foo
    /// #section Bar
    /// 
    /// Name1 = Value
    /// 
    /// #endsection
    /// #endsection
    /// 
    /// // Is equivalent to:
    /// 
    /// Foo.Bar.Name = Value
    /// </code>
    /// <para>
    /// Lines prefixed by "&lt;", "--", or "//" will be ignored as comments.
    /// These characters were selected so that XML comment tags can be used
    /// to surround key specifications within WINFULL configuration files.
    /// This will enable the Visual Studio editor to parse and color the file 
    /// correctly and will also avoid any trouble with the .NET ConfigurationSettings
    /// class trying to read invalid XML.
    /// </para>
    /// <para>
    /// Config files implement a simple form of conditionals of the form:
    /// </para>
    /// <code language="none">
    /// #define ident
    ///     
    /// #if ident
    /// #endif
    ///     
    /// #if !ident
    /// #endif
    ///     
    /// #if ident
    /// #else
    /// #endif
    /// </code>
    /// <para>
    /// where <b>ident</b> can be one of the built-in identifiers WINFULL or WINCE specifying
    /// the current environment, an evironment variable, or an identifier defined with #define.  
    /// Note that the reserved words and identifiers are case sensitive.  #if..#endif commands
    /// may be nested.
    /// </para>
    /// <para>
    /// The configuration class hardcodes the two identifiers <c>true</c> and <c>false</c>
    /// with the appropriate values.  These values can be used to comment or
    /// uncomment sections of a configuration file.
    /// </para>
    /// <para>
    /// Configuration files also support the more structured <b>#switch... #endswitch</b>
    /// conditional.  This compares the value (case insensitively) of an identifier 
    /// with one or more <b>#case &lt;value&gt;</b> sections and loads configuration
    /// keys from sections that match.The <b>#default</b> section will be enabled if 
    /// no <b>#case</b> section up to this point has matched.  <b>#default</b> sections
    /// should be the last section in the <b>#switch</b> statement.  <b>#switch</b> 
    /// statements may be nested.
    /// </para>
    /// <para>
    /// In the example below, key will end up with the value of "Foo".  
    /// </para>
    /// <code language="none">
    /// #define MyValue Foo
    /// 
    /// #switch MyValue
    /// 
    ///     #case Foo
    /// 
    ///         key = Foo
    /// 
    ///     #case Bar
    /// 
    ///         key = Bar
    /// 
    ///     #default
    /// 
    ///         key = FooBar
    /// 
    /// #endswitch
    /// </code>
    /// <para>
    /// Macro variables can be added within a configuration using either of
    /// the syntaxs:
    /// </para>
    /// <code language="none">
    /// #define ident value
    /// #set ident value
    /// </code>
    /// <para>
    /// The difference is that macros defined with the #define syntax will be
    /// expanded recursively each time the macro is referenced where as macros
    /// defined via #set will be evaluated only once at the time the macro
    /// is defined.
    /// </para>
    /// <para>
    /// These macros can be used within configuration setting values via the
    /// <b>$(ident)</b> or the archaic <b>%ident%</b> syntax.  The Config class 
    /// will recursively expand macros up to 16 levels deep.  Note that environment 
    /// variables may also appear on the right side the equals sign in a configuration 
    /// setting using the same syntax.  Note that macros defined within the configuration
    /// file take presidence over environment variables.
    /// </para>
    /// <para>
    /// Note that lookups for macros created via #define and #set are case insensitive.
    /// </para>
    /// <para>
    /// Use the #undef statement to remove a macro from the configuration.  It is
    /// not an error to remove an undefined macro.
    /// </para>
    /// <para>
    /// <para><b><u>Configuration Value Parsing</u></b></para>
    /// <para>
    /// The <see cref="Config" /> class implements several methods that are capable
    /// of parsing various types from string values.  The parsable types include
    /// <see cref="int" />, <see cref="bool" />, <see cref="double" />, <see cref="IPAddress" />,
    /// <see cref="NetworkBinding" />, <see cref="Guid" />, <see cref="System.Type" />, 
    /// <see cref="TimeSpan" />, <see cref="Uri" /> as well as enumeration type values.  
    /// The table below describes the parsing bahavior for each type:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Integer</term>
    ///         <description>
    ///         <para>
    ///         Integer configuration values are formatted as you'd expect, a
    ///         series of decimal digits optionally prefixed by a plus or minus
    ///         sign.  A unit suffix of "K", "M", or "G" can be added to the integer.  
    ///         This multiplies the value by K=1024, M=1024*1024 and G=1024*1024*1024.
    ///         </para>
    ///         <para>
    ///         The following constant values are also supported:
    ///         </para>
    ///         <list type="table">
    ///             <item><term><b>short.min</b></term><description>-32768</description></item>
    ///             <item><term><b>short.max</b></term><description>32767</description></item>
    ///             <item><term><b>ushort.max</b></term><description>65533</description></item>
    ///             <item><term><b>int.min</b></term><description>-2147483648</description></item>
    ///             <item><term><b>int.max</b></term><description>2147483647</description></item>
    ///             <item><term><b>uint.max</b></term><description>4294967295</description></item>
    ///         </list>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Long</term>
    ///         <description>
    ///         <para>
    ///         Integer configuration values are formatted as you'd expect, a
    ///         series of decimal digits optionally prefixed by a plus or minus
    ///         sign.  A unit suffix of "K", "M", or "G" can be added to the integer.  
    ///         This multiplies the value by K=1024, M=1024*1024 and G=1024*1024*1024.
    ///         </para>
    ///         <para>
    ///         The following constant values are also supported:
    ///         </para>
    ///         <list type="table">
    ///             <item><term><b>short.min</b></term><description>-32768</description></item>
    ///             <item><term><b>short.max</b></term><description>32767</description></item>
    ///             <item><term><b>ushort.max</b></term><description>65533</description></item>
    ///             <item><term><b>int.min</b></term><description>-2147483648</description></item>
    ///             <item><term><b>int.max</b></term><description>2147483647</description></item>
    ///             <item><term><b>uint.max</b></term><description>4294967295</description></item>
    ///             <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
    ///         </list>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Boolean</term>
    ///         <description>
    ///         Parses a boolean value.  True values can be specified by "true", 
    ///         "yes", "on", or "1".  False values by "false", "no", "off", or "0".
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Double</term>
    ///         <description>
    ///         <para>
    ///         Floating point values are specified using fixed point notation and
    ///         optionally appending a "K", "M", or "G" unit suffix.  Note that
    ///         scientific notation is not supported.
    ///         </para>
    ///         <para>
    ///         The following constant values are also supported:
    ///         </para>
    ///         <list type="table">
    ///             <item><term><b>short.min</b></term><description>-32768</description></item>
    ///             <item><term><b>short.max</b></term><description>32767</description></item>
    ///             <item><term><b>ushort.max</b></term><description>65533</description></item>
    ///             <item><term><b>int.min</b></term><description>-2147483648</description></item>
    ///             <item><term><b>int.max</b></term><description>2147483647</description></item>
    ///             <item><term><b>uint.max</b></term><description>4294967295</description></item>
    ///             <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
    ///         </list>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="IPAddress" /></term>
    ///         <description>
    ///         IP addresses are parsed in dotted quad notation: ###.###.###.###.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="NetworkBinding" /></term>
    ///         <description>
    ///         <para>
    ///         Network bindings are used to specify a remote network endpoint for
    ///         the client side of a connection or the network interface to be
    ///         bound for the server side of a connection.  Network bindings are
    ///         formatted as: 
    ///         </para>
    ///         <para>
    ///         <br />
    ///         <b>&lt;address&gt; ":" &lt;port&gt;</b>
    ///         <br />
    ///         </para>
    ///         <para>
    ///         where <b>address</b> is an IP address or DNS host name and <b>port</b>
    ///         is an integer port number or a well-known port name.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Guid" /></term>
    ///         <description>
    ///         <para>
    ///         Guids are expected to be formatted using the registry format,
    ///         as in:
    ///         </para>
    ///         <blockquote>{D9AB82D3-FC3D-4eca-A19F-A527D16BC4AC}</blockquote>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="TimeSpan" /></term>
    ///         <description>
    ///         <para>
    ///         Time intervals are parsed as:
    ///         </para>
    ///         <para>
    ///         <br />
    ///         <b>&lt;duration&gt;[&lt;units&gt;]</b>
    ///         <br />
    ///         or
    ///         <br />
    ///         <b>&lt;d&gt;.&lt;hh&gt;:&lt;mm&gt;:&lt;ss&gt;</b>
    ///         <br />
    ///         </para>
    ///         <para>
    ///         where <b>duration</b> is the duration value expressed as a fixed
    ///         point number and the optional <b>units</b> is one of <b>d=days</b>, 
    ///         <b>h=hours</b>, <b>m=minutes</b>, <b>s=seconds</b>, or <b>ms=milliseconds</b>.
    ///         Seconds will be assumed if no units are specified.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="System.Type" /></term>
    ///         <description>
    ///         <para>
    ///         Type references are used to dynmically load an assembly and
    ///         return a reference to a particular type located within the
    ///         assembly.  Type references are formatted as:
    ///         </para>
    ///         <para>
    ///         <br />
    ///         <b>&lt;type name&gt;:&lt;assembly path&gt;</b>
    ///         <br />
    ///         </para>
    ///         <para>
    ///         Where <b>type name</b> is the fully qualified name of the type
    ///         to be loaded and <b>assembly path</b> is the fully qualified
    ///         path of the assembly implementing the type.  Here's an
    ///         example for the type <b>MyLibrary.MyType</b> located in the
    ///         <b>MyAssembly.dll</b> assembly:
    ///         </para>
    ///         <para>
    ///         <br />
    ///         MyLibrary.MyType:C:\MyAssembly.dll
    ///         <br />
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Uri" /></term>
    ///         <description>
    ///         <para>
    ///         Parses a standard absolute URI.
    ///         </para>
    ///         </description>
    ///     </item>
    /// </list>
    /// <b><u>Environment Variables</u></b>
    /// </para>
    /// <para>
    /// The configuration class is capable of processing environment variable
    /// references embedded in configuration values.  Environment variables are
    /// quoted via the $(name) pattern as in $(SystemRoot).  By default,
    /// all configuration strings will be processed for environment variables.
    /// This behavior can be disabled by setting the <c>static</c> <see cref="ProcessEnvironmentVars" />
    /// property to false.  See the <see cref="EnvironmentVars" /> class for a description of 
    /// how environment variables are handled on the various platforms.
    /// </para>
    /// <para>
    /// The configuration class will also scan the environment for variables
    /// that match the keyPrefix passed to the constructor and adds those to
    /// the configuration as well.
    /// </para>
    /// <b><u>Key Arrays</u></b>
    /// <para>
    /// The Config class provides a simple implementation of multi-valued keys, referred
    /// to as key arrays.  Each value in a key array is specified using the square
    /// bracket notation:
    /// </para>
    /// <code language="none">
    /// key[index] = value
    /// </code>
    /// <para>
    /// where <b>key</b> is the underlying key name, <b>index</b> specifies the index
    /// of the value, and <b>value</b> is the key value.  <b>index</b> may be either a
    /// zero based integer or a subkey name.  Here are some examples:
    /// </para>
    /// <code language="none">
    /// example[0] = value 1
    /// example[1] = value 2
    /// example[2] = value 3
    /// 
    /// property[name]  = Jeff
    /// property[phone] = 555-1212
    /// property[zip]   = 98037
    /// </code>
    /// <para>
    /// These two examples specify two multi-valued configuration keys: <b>example</b> and
    /// <b>property</b> where <b>example</b> can be indexed via zero based integer indexes and
    /// <b>property</b> by subkey names.
    /// </para>
    /// <para>
    /// It is also possible to specify auto incrementing array indexes using the <b>[-]</b>
    /// syntax to avoid having to manually enter these values.  The <see cref="Config" />
    /// class will assign an index of zero to the first entry like this it sees and
    /// then automatically increments this index for subsequent entries.  The two examples
    /// below are qequivalent:
    /// </para>
    /// <code language="none">
    /// example[-] = value 1
    /// example[-] = value 2
    /// example[-] = value 3
    /// 
    /// // Equivalent to:
    /// 
    /// example[0] = value 1
    /// example[1] = value 2
    /// example[2] = value 3
    /// </code>
    /// <note>
    /// You generally shouldn't include both <b>mysetting[-]</b> and <b>mysetting[#]</b> forms
    /// of key array specifications in the same configuration file, but if you do, the explicit
    /// <b>mysetting[#]</b> setting will replace the <b>mysetting[-]</b> value regardless of
    /// the relative position of the two settings in the configuration file.
    /// </note>
    /// <para>
    /// Use <see cref="Config.GetArray(string)"/> or <see cref="Config.GetArray(string,string[])"/> 
    /// to return the zero based array of strings
    /// for a configuration setting.  Calling GetArray("example") on the configuration
    /// above will return an array of the strings {"value 1","value 2","value 3"}.
    /// </para>
    /// <para>
    /// Use <see cref="Config.GetDictionary"/> to return a string dictionary for containing
    /// all of the subkey/value pairs for a particular key.  Calling <see cref="GetDictionary" />
    /// will return a dictionary of the property values keyed by the subkeys.
    /// </para>
    /// <para><b><u>Configuration References</u></b></para>
    /// <para>
    /// The <see cref="GetConfigRef" /> and <b>ParseValue()</b> methods provide a
    /// standardized way to parse configuration values passed as string constants or
    /// via a configuration lookup.
    /// </para>
    /// <para>
    /// A configuration reference specifies a global configuration key and
    /// a string representing the default value to use if the key is not
    /// present in the configuration or there's a problem parsing its value.
    /// </para>
    /// <para>
    /// This method provides a standard mechanism for being able to specify a configuration
    /// key and a default value in situations like this by formatting the attribute
    /// property value as:
    /// </para>
    /// <code language="none">"[" config:&lt;key name&gt;[,&lt;default value&gt;] "]"</code>
    /// <para>
    /// where &lt;key name&gt; is the configuration key name and &lt;default value&gt;
    /// specifies an optional default value.  Here's an example:
    /// </para>
    /// <code language="none">
    /// [MyAttribute(Timeout="[config:MyApp.Timeout,10s")]
    /// public void Foo() {
    /// 
    /// }
    /// </code>
    /// <para>
    /// This example specifies that the <see cref="Timeout" /> property should be 
    /// set to the configuration value returned by Config.Global("MyApp").Get("Timeout","10s").
    /// </para>
    /// </remarks>
#else
    /// <summary>
    /// Abstracts access to the application configuration settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class accesses settings from a standard XML based configuration
    /// file for WINFULL .NET Framework applications as well as from a
    /// name/value text file for both WINFULL and WINCE implementations.
    /// </para>
    /// <para>
    /// The compact framework does not implement the ConfigurationSettings
    /// class (probably to avoid having to load the XML DOM classes).  This
    /// class implementation will load the UTF8 encoded configuration file named
    /// with the same as the file name of the currently executing process
    /// with the extension replaced by ".ini" for normal processes or the
    /// <b>Web.ini</b> file in the root folder of websites.  Due to limitations
    /// in the  Compact Framework, the WINCE constructors must be passed a reference 
    /// to the assembly for the main application entrypoint.
    /// </para>
    /// <para>
    /// The LillTek configuration system has evolved over the past several years
    /// from a simple name/value text file to deal with the lack of configuration
    /// support on WINCE into an advanced format that supports macros, conditions,
    /// sections, environment variables, and pluggable configuration providers. 
    /// Most LillTek applications now use this class is their primary means of 
    /// managing application specific configuration settings.
    /// </para>
    /// <para>
    /// The format of the config file is simply a series of configuration 
    /// name/value pairs, one to a line, formatted as:
    /// </para>
    /// <para>
    /// <code language="none">
    /// name=value
    /// </code>
    /// </para>
    /// <para>
    /// Extra whitespace will be trimmed from both ends of these strings.
    /// Note that keys and values are case sensitive.  Multi-line values
    /// can be specified surrounding the value with "{{" and "}}" where the
    /// terminating "}}" must appear on a line by itself:
    /// </para>
    /// <code language="none">
    /// name = {{
    ///     line1
    ///     line2
    ///     line3
    /// }}
    /// </code>
    /// <para>
    /// Note that each line of a multi-line value will be trimmed of whitespace
    /// at the beginning and end of the line.
    /// </para>
    /// <para>
    /// The <b>#section</b> and <b>#endsection</b> commands can be used to 
    /// specify a configuration key prefix:
    /// </para>
    /// <code language="none">
    /// #section MyApplication
    /// 
    /// Name1 = value
    /// Name2 = value
    /// 
    /// #endsection
    /// 
    /// // The definitions above are equivalant to:
    /// 
    /// MyApplication.Name1 = value
    /// MyApplication.Name2 = value
    /// 
    /// // #section commands can also be nested:
    /// 
    /// #section Foo
    /// #section Bar
    /// 
    /// Name1 = Value
    /// 
    /// #endsection
    /// #endsection
    /// 
    /// // Is equivalent to:
    /// 
    /// Foo.Bar.Name = Value
    /// </code>
    /// <para>
    /// Lines prefixed by "&lt;", "--", or "//" will be ignored as comments.
    /// These characters were selected so that XML comment tags can be used
    /// to surround key specifications within WINFULL configuration files.
    /// This will enable the Visual Studio editor to parse and color the file 
    /// correctly and will also avoid any trouble with the .NET ConfigurationSettings
    /// class trying to read invalid XML.
    /// </para>
    /// <para>
    /// Config files implement a simple form of conditionals of the form:
    /// </para>
    /// <code language="none">
    /// #define ident
    ///     
    /// #if ident
    /// #endif
    ///     
    /// #if !ident
    /// #endif
    ///     
    /// #if ident
    /// #else
    /// #endif
    /// </code>
    /// <para>
    /// where <b>ident</b> can be one of the built-in identifiers WINFULL or WINCE specifying
    /// the current environment, an evironment variable, or an identifier defined with #define.  
    /// Note that the reserved words and identifiers are case sensitive.  #if..#endif commands
    /// may be nested.
    /// </para>
    /// <para>
    /// The configuration class hardcodes the two identifiers <c>true</c> and <c>false</c>
    /// with the appropriate values.  These values can be used to comment or
    /// uncomment sections of a configuration file.
    /// </para>
    /// <para>
    /// Configuration files also support the more structured <b>#switch... #endswitch</b>
    /// conditional.  This compares the value (case insensitively) of an identifier 
    /// with one or more <b>#case &lt;value&gt;</b> sections and loads configuration
    /// keys from sections that match.The <b>#default</b> section will be enabled if 
    /// no <b>#case</b> section up to this point has matched.  <b>#default</b> sections
    /// should be the last section in the <b>#switch</b> statement.  <b>#switch</b> 
    /// statements may be nested.
    /// </para>
    /// <para>
    /// In the example below, key will end up with the value of "Foo".  
    /// </para>
    /// <code language="none">
    /// #define MyValue Foo
    /// 
    /// #switch MyValue
    /// 
    ///     #case Foo
    /// 
    ///         key = Foo
    /// 
    ///     #case Bar
    /// 
    ///         key = Bar
    /// 
    ///     #default
    /// 
    ///         key = FooBar
    /// 
    /// #endswitch
    /// </code>
    /// <para>
    /// Macro variables can be added within a configuration using either of
    /// the syntaxs:
    /// </para>
    /// <code language="none">
    /// #define ident value
    /// #set ident value
    /// </code>
    /// <para>
    /// The difference is that macros defined with the #define syntax will be
    /// expanded recursively each time the macro is referenced where as macros
    /// defined via #set will be evaluated only once at the time the macro
    /// is defined.
    /// </para>
    /// <para>
    /// These macros can be used within configuration setting values via the
    /// <b>$(ident)</b> or the archaic <b>%ident%</b> syntax.  The Config class 
    /// will recursively expand macros up to 16 levels deep.  Note that environment 
    /// variables may also appear on the right side the equals sign in a configuration 
    /// setting using the same syntax.  Note that macros defined within the configuration
    /// file take presidence over environment variables.
    /// </para>
    /// <para>
    /// Note that lookups for macros created via #define and #set are case insensitive.
    /// </para>
    /// <para>
    /// Use the #undef statement to remove a macro from the configuration.  It is
    /// not an error to remove an undefined macro.
    /// </para>
    /// <para>
    /// Other configuration files may be included into a main file using the <b>#include</b>
    /// expression, as in:
    /// </para>
    /// <code language="none">
    /// #include CommonConfig.ini
    /// </code>
    /// <para>
    /// Note that the file path is relative to the <see cref="Helper.AppFolder"/> property
    /// set in the <see cref="Helper"/> class and that file <b>#include</b>s can be nested
    /// a maximum of 4 levels deep.
    /// </para>
    /// <para><b><u>Configuration Value Parsing</u></b></para>
    /// <para>
    /// The <see cref="Config" /> class implements several methods that are capable
    /// of parsing various types from string values.  The parsable types include
    /// <see cref="int" />, <see cref="bool" />, <see cref="double" />, <see cref="IPAddress" />,
    /// <see cref="NetworkBinding" />, <see cref="Guid" />, <see cref="System.Type" />, 
    /// <see cref="TimeSpan" />, <see cref="Uri" /> as well as enumeration type values.  
    /// The table below describes the parsing bahavior for each type:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Integer</term>
    ///         <description>
    ///         <para>
    ///         Integer configuration values are formatted as you'd expect, a
    ///         series of decimal digits optionally prefixed by a plus or minus
    ///         sign.  A unit suffix of "K", "M", or "G" can be added to the integer.  
    ///         This multiplies the value by K=1024, M=1024*1024 and G=1024*1024*1024.
    ///         </para>
    ///         <para>
    ///         The following constant values are also supported:
    ///         </para>
    ///         <list type="table">
    ///             <item><term><b>short.min</b></term><description>-32768</description></item>
    ///             <item><term><b>short.max</b></term><description>32767</description></item>
    ///             <item><term><b>ushort.max</b></term><description>65533</description></item>
    ///             <item><term><b>int.min</b></term><description>-2147483648</description></item>
    ///             <item><term><b>int.max</b></term><description>2147483647</description></item>
    ///             <item><term><b>uint.max</b></term><description>4294967295</description></item>
    ///         </list>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Long</term>
    ///         <description>
    ///         <para>
    ///         Integer configuration values are formatted as you'd expect, a
    ///         series of decimal digits optionally prefixed by a plus or minus
    ///         sign.  A unit suffix of "K", "M", or "G" can be added to the integer.  
    ///         This multiplies the value by K=1024, M=1024*1024 and G=1024*1024*1024.
    ///         </para>
    ///         <para>
    ///         The following constant values are also supported:
    ///         </para>
    ///         <list type="table">
    ///             <item><term><b>short.min</b></term><description>-32768</description></item>
    ///             <item><term><b>short.max</b></term><description>32767</description></item>
    ///             <item><term><b>ushort.max</b></term><description>65533</description></item>
    ///             <item><term><b>int.min</b></term><description>-2147483648</description></item>
    ///             <item><term><b>int.max</b></term><description>2147483647</description></item>
    ///             <item><term><b>uint.max</b></term><description>4294967295</description></item>
    ///             <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
    ///         </list>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Boolean</term>
    ///         <description>
    ///         Parses a boolean value.  True values can be specified by "true", 
    ///         "yes", "on", or "1".  False values by "false", "no", "off", or "0".
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Double</term>
    ///         <description>
    ///         <para>
    ///         Floating point values are specified using fixed point notation and
    ///         optionally appending a "K", "M", or "G" unit suffix.  Note that
    ///         scientific notation is not supported.
    ///         </para>
    ///         <para>
    ///         The following constant values are also supported:
    ///         </para>
    ///         <list type="table">
    ///             <item><term><b>short.min</b></term><description>-32768</description></item>
    ///             <item><term><b>short.max</b></term><description>32767</description></item>
    ///             <item><term><b>ushort.max</b></term><description>65533</description></item>
    ///             <item><term><b>int.min</b></term><description>-2147483648</description></item>
    ///             <item><term><b>int.max</b></term><description>2147483647</description></item>
    ///             <item><term><b>uint.max</b></term><description>4294967295</description></item>
    ///             <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
    ///         </list>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="IPAddress" /></term>
    ///         <description>
    ///         IP addresses are parsed in dotted quad notation: ###.###.###.###.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="NetworkBinding" /></term>
    ///         <description>
    ///         <para>
    ///         Network bindings are used to specify a remote network endpoint for
    ///         the client side of a connection or the network interface to be
    ///         bound for the server side of a connection.  Network bindings are
    ///         formatted as: 
    ///         </para>
    ///         <para>
    ///         <br />
    ///         <b>&lt;address&gt; ":" &lt;port&gt;</b>
    ///         <br />
    ///         </para>
    ///         <para>
    ///         where <b>address</b> is an IP address or DNS host name and <b>port</b>
    ///         is an integer port number or a well-known port name.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Guid" /></term>
    ///         <description>
    ///         <para>
    ///         Guids are expected to be formatted using the registry format,
    ///         as in:
    ///         </para>
    ///         <blockquote>{D9AB82D3-FC3D-4eca-A19F-A527D16BC4AC}</blockquote>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="TimeSpan" /></term>
    ///         <description>
    ///         <para>
    ///         Time intervals are parsed as:
    ///         </para>
    ///         <para>
    ///         <br />
    ///         <b>&lt;duration&gt;[&lt;units&gt;]</b>
    ///         <br />
    ///         or
    ///         <br />
    ///         <b>&lt;d&gt;.&lt;hh&gt;:&lt;mm&gt;:&lt;ss&gt;</b>
    ///         <br />
    ///         </para>
    ///         <para>
    ///         where <b>duration</b> is the duration value expressed as a fixed
    ///         point number and the optional <b>units</b> is one of <b>d=days</b>, 
    ///         <b>h=hours</b>, <b>m=minutes</b>, <b>s=seconds</b>, or <b>ms=milliseconds</b>.
    ///         Seconds will be assumed if no units are specified.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="System.Type" /></term>
    ///         <description>
    ///         <para>
    ///         Type references are used to dynmically load an assembly and
    ///         return a reference to a particular type located within the
    ///         assembly.  Type references are formatted as:
    ///         </para>
    ///         <para>
    ///         <br />
    ///         <b>&lt;type name&gt;:&lt;assembly path&gt;</b>
    ///         <br />
    ///         </para>
    ///         <para>
    ///         Where <b>type name</b> is the fully qualified name of the type
    ///         to be loaded and <b>assembly path</b> is the fully qualified
    ///         path of the assembly implementing the type.  Here's an
    ///         example for the type <b>MyLibrary.MyType</b> located in the
    ///         <b>MyAssembly.dll</b> assembly:
    ///         </para>
    ///         <para>
    ///         <br />
    ///         MyLibrary.MyType:C:\MyAssembly.dll
    ///         <br />
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Uri" /></term>
    ///         <description>
    ///         <para>
    ///         Parses a standard absolute URI.
    ///         </para>
    ///         </description>
    ///     </item>
    /// </list>
    /// <b><u>Environment Variables</u></b>
    /// <para>
    /// The configuration class is capable of processing environment variable
    /// references embedded in configuration values.  Environment variables are
    /// quoted via the $(name) pattern as in $(SystemRoot).  By default,
    /// all configuration strings will be processed for environment variables.
    /// This behavior can be disabled by setting the <c>static</c> <see cref="ProcessEnvironmentVars" />
    /// property to false.  See the <see cref="EnvironmentVars" /> class for a description of 
    /// how environment variables are handled on the various platforms.
    /// </para>
    /// <note>
    /// Environment variable names are case insenstitve.
    /// </note>
    /// <para>
    /// The configuration class will also scan the environment for variables
    /// that match the keyPrefix passed to the constructor and adds those to
    /// the configuration as well.
    /// </para>
    /// <b><u>Key Arrays</u></b>
    /// <para>
    /// The Config class provides a simple implementation of multi-valued keys, referred
    /// to as key arrays.  Each value in a key array is specified using the square
    /// bracket notation:
    /// </para>
    /// <code language="none">
    /// key[index] = value
    /// </code>
    /// <para>
    /// where <b>key</b> is the underlying key name, <b>index</b> specifies the index
    /// of the value, and <b>value</b> is the key value.  <b>index</b> may be either a
    /// zero based integer or a subkey name.  Here are some examples:
    /// </para>
    /// <code language="none">
    /// example[0] = value 1
    /// example[1] = value 2
    /// example[2] = value 3
    /// 
    /// property[name]  = Jeff
    /// property[phone] = 555-1212
    /// property[zip]   = 98037
    /// </code>
    /// <para>
    /// These two examples specify two multi-valued configuration keys: <b>example</b> and
    /// <b>property</b> where <b>example</b> can be indexed via zero based integer indexes and
    /// <b>property</b> by subkey names.
    /// </para>
    /// <para>
    /// It is also possible to specify auto incrementing array indexes using the <b>[-]</b>
    /// syntax to avoid having to manually enter these values.  The <see cref="Config" />
    /// class will assign an index of zero to the first entry like this it sees and
    /// then automatically increments this index for subsequent entries.  The two examples
    /// below are qequivalent:
    /// </para>
    /// <code language="none">
    /// example[-] = value 1
    /// example[-] = value 2
    /// example[-] = value 3
    /// 
    /// // Equivalent to:
    /// 
    /// example[0] = value 1
    /// example[1] = value 2
    /// example[2] = value 3
    /// </code>
    /// <note>
    /// You generally shouldn't include both <b>mysetting[-]</b> and <b>mysetting[#]</b> forms
    /// of key array specifications in the same configuration file, but if you do, the explicit
    /// <b>mysetting[#]</b> setting will replace the <b>mysetting[-]</b> value regardless of
    /// the relative position of the two settings in the configuration file.
    /// </note>
    /// <para>
    /// Use <see cref="Config.GetArray(string)"/> or <see cref="Config.GetArray(string,string[])"/> 
    /// to return the zero based array of strings for a configuration setting.  Calling 
    /// <b>GetArray("example")</b> on the configuration above will return an array of the
    /// strings <b>{"value 1","value 2","value 3"}</b>.
    /// </para>
    /// <para>
    /// Use <see cref="Config.GetDictionary"/> to return a string dictionary for containing
    /// all of the subkey/value pairs for a particular key.  Calling <see cref="GetDictionary" />
    /// will return a dictionary of the property values keyed by the subkeys.
    /// </para>
    /// <para><b><u>Configuration References</u></b></para>
    /// <para>
    /// The <see cref="GetConfigRef" /> and <b>ParseValue()</b> methods provide a
    /// standardized way to parse configuration values passed as string constants or
    /// via a configuration lookup.
    /// </para>
    /// <para>
    /// A configuration reference specifies a global configuration key and
    /// a string representing the default value to use if the key is not
    /// present in the configuration or there's a problem parsing its value.
    /// </para>
    /// <para>
    /// This method provides a standard mechanism for being able to specify a configuration
    /// key and a default value in situations like this by formatting the attribute
    /// property value as:
    /// </para>
    /// <code language="none">"[" config:&lt;key name&gt;[,&lt;default value&gt;] "]"</code>
    /// <para>
    /// where &lt;key name&gt; is the configuration key name and &lt;default value&gt;
    /// specifies an optional default value.  Here's an example:
    /// </para>
    /// <code language="none">
    /// [MyAttribute(Timeout="[config:MyApp.Timeout,10s")]
    /// public void Foo() {
    /// 
    /// }
    /// </code>
    /// <para>
    /// This example specifies that the <see cref="Timeout" /> property should be 
    /// set to the configuration value returned by Config.Global("MyApp").Get("Timeout","10s").
    /// </para>
    /// <para><b><u>Custom Configuration Providers</u></b></para>
    /// <para>
    /// The Config class is able to retrieve configuation from custom sources
    /// via use of custom classes implementing the <see cref="IConfigProvider" /> interface.  
    /// This class is specified locally using the "Config.CustomProvider" setting:
    /// </para>
    /// <code language="none">
    /// Config.CustomProvider = &lt;type;&gt; : &lt;assembly path&gt;
    /// </code>
    /// <para>
    /// Where &lt;type;&gt; is the fully qualified name of the provider class and
    /// &lt;assembly path&gt; is the path to the assembly file defining the type
    /// (as described in <see cref="Parse(string,System.Type)" />.
    /// </para>
    /// <para>
    /// Config will call the custom provider's <see cref="IConfigProvider.GetConfig" />
    /// method retrieve the configuration imformation.  The parameters passed to this
    /// method will be loaded from additional "Config.*" settings.  This example
    /// demonstrates the configuration settings to use the LillTek DataCenter
    /// configuration provider.
    /// </para>
    /// <code language="none">
    /// Config.CustomProvider = LillTek.Datacenter.ConfigServiceProvider:LillTek.Datacenter.dll
    /// Config.Settings       = RouterEP=physical://detached/hub/$(Guid);CloudEP=$(LillTek.DC.CloudEP)
    /// Config.CacheFile      = Foo.cache.ini    -- Defaults to current process exe name + ".cache.ini"
    ///                                          -- Specify "(no-cache)" to discable caching
    /// Config.MachineName    = $(MachineName)   -- Defaults to the current host name
    /// Config.ExeFile        = Foo.exe          -- Defaults to the unqualified process exe name for
    ///                                             normal applications and processes or the ASP.NET
    ///                                             application name for web applications.
    /// Config.ExeVersion     = 1.0              -- Defaults to the executable's version number
    /// Config.Usage          = HighPerf         -- Defaults to the empty string
    /// </code>
    /// <para>
    /// The Config class makes use of custom providers as follows:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     The "Config.*" settings are loaded and checked to see if a 
    ///     custom provider is specified.
    ///     </item>
    ///     <item>
    ///     If a provider is present, then the provider class is instantiated
    ///     and its <see cref="IConfigProvider.GetConfig" /> method is called
    ///     using the parameters gathered from the Config.* settings.  Some
    ///     providers may choose to cache the configuration returned on disk.
    ///     The Config class will also cache a copy of this information in
    ///     a global variable.
    ///     </item>
    ///     <item>
    ///     An error will be logged if any of this failed.  Some config provider
    ///     implementations may silently fail over to use a cached copy of
    ///     the last configuration retrieved.  Others may return null indicating
    ///     failure.
    ///     </item>
    ///     <item>
    ///     When a Config instance is constructed, the configuration settings used
    ///     will be the settings returned by the cache providers if one was specified
    ///     and the operation was successful.  Otherwise the settings from the
    ///     local configuration files will be used.  Note that embedded environment 
    ///     variable references will be processed in both cases.
    ///     </item>
    /// </list>
    /// <para><b><u>ASP.NET Configuration Files</u></b></para>
    /// <para>
    /// LillTek confiuration files can also be consumed by ASP.NET applications.
    /// For this to work properly, the application must call <see cref="Helper.InitializeWebApp" />
    /// and <see cref="Config.SetConfigPath(string)" /> within the <b>Application_Start</b>
    /// event handler in the <b>Global.asax</b> file, passing the physical root path
    /// to the application as returned by <b>HostingEnvironment.ApplicationPhysicalPath</b>.
    /// The configuration file should be called <b>Web.ini</b> and should be located
    /// in the root directory.
    /// </para>
    /// <para>
    /// Custom configuration providers may be used in ASP.NET applications.  Make sure
    /// that a reference to the provider's assembly is added to the ASP.NET application
    /// so that the assembly will be copied to the application's \Bin folder.
    /// </para>
    /// <para><b><u>Developer Override File</u></b></para>
    /// <para>
    /// It is often useful for a developer or tester to be able to globally override
    /// a set of configuration settings on a machine or a set of machines.  An great
    /// example of this comes up when starting application message routers implemented
    /// by the LillTek.Messaging library.  Being able to easily specify a multicast 
    /// endpoint that is unique to the developer across all of the application
    /// instances running on his machines is very important to prevent conflicts
    /// with other developers or even production systems running on the network.
    /// </para>
    /// <para>
    /// The Config class provides for the specification of an optional configuration 
    /// file that will override any settings loaded from any other mechanism (including
    /// custom configuration providers).  This file is formatted like any other .ini
    /// file.  To enable a specific override file, simply add an environment variable
    /// called <b>LillTek.ConfigOverride</b> and set its value to the fully qualified
    /// path to the file.  In most situations, it's best to add this as a system variable,
    /// rather than a user variable.  This will ensure that applications running as
    /// Windows services will also be able to see the variable.
    /// </para>
    /// <para><b><u>Windows Azure Configuration Settings</u></b></para>
    /// <para>
    /// The <see cref="Config"/> class provides some specialized access to Windows
    /// Azure role settings.  These are name/value strings specified within the
    /// cloud service's <b>*.cscfg</b>  XML files.  This information is packaged
    /// and propagated somewhere in the role instance and is made available at
    /// a low level via the simplistic Azure <b>CloudConfigurationManager.GetSetting()</b>
    /// method.  The <see cref="Config" /> class extends this by providing access
    /// to typed setting values.
    /// </para>
    /// <para>
    /// To access an Azure role setting, use the <see cref="Global" /> settings instance 
    /// and prefix the setting name with <b>"Azure."</b> when calling one of the <b>Get()</b>
    /// methods.  Note that any environment variable references will be expanded normally.
    /// </para>
    /// <para>
    /// Note that arrays, dictionaries, or the construction of a <see cref="Config" /> instance
    /// that holds a subsection of settings is not possible for Azure role settings at this
    /// time, because the underlying Azure API does not provide a way to enumerate all of the
    /// settings.  You'll need to stick with using the <see cref="Global" /> instance and 
    /// provide fully qualified settings paths.
    /// </para>
    /// <para>
    /// Azure settings may also be accessed within the config file using the <b>$(azure.&lt;name&gt;)</b>
    /// syntax.  This will work of the right side of <b>setting = value</b> expressions or in
    /// <b>#if</b> and <b>#switch</b> expressions.  Here's an example:
    /// </para>
    /// <code language="none">
    /// #switch $(azure.environment)
    /// 
    ///     #case PROD
    ///     
    ///         ConnectionString = $(azure.proddatabase)
    ///     
    ///     #case TEST
    ///     
    ///         ConnectionString = $(azure.testdatabase)
    ///     
    ///     #default
    ///     
    ///         ConnectionString = $(azure.defaultdatabase)
    /// 
    /// #endswitch
    /// </code>
    /// <note>
    /// Azure configuration setting names are <b>case insensitive</b>.
    /// </note>
    /// </remarks>
#endif
    public sealed class Config : IEnumerable<KeyValuePair<string, string>>
    {
        //---------------------------------------------------------------------
        // Static members

        private static object       syncLock = new object();
        private static char[]       macroChars = new char[] { '$', '%' };

        private static Config       global = null;              // Global configuration (or null)
        private static string       configPath = null;          // Fully qualified path to the config file
                                                                // or null to use WINFULL configuration
#if !MOBILE_DEVICE
        private static bool         providerLoad = false;       // True if we've checked for a custom 
                                                                // provider implementation yet
#endif
        private static string       configText = null;          // Non-null if the settings are to be
                                                                // loaded from this string rather than
                                                                // a configuration file
        private static bool         devOverrideLoad = false;    // True if we've attempted to load the
                                                                // override settings
        private static Config       devOverride = null;         // The configuration override settings
                                                                // or null if these haven't been loaded yet
        private static MethodInfo   getAzureSetting = null;     // The Azure GetSetting() method or null

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Config()
        {
            Config.EnableLoadTracing      = false;
            Config.ProcessEnvironmentVars = true;
        }

        //---------------------------------------------------------------------
        // This code is used for tracing configuration setting loads.

        /// <summary>
        /// Controls whether debug tracing information is emitted when configuration settings
        /// are loaded.
        /// </summary>
        public static bool EnableLoadTracing { get; set; }

        /// <summary>
        /// Writes load traces to the debug output if tracing is enabled.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        private static void LoadTrace(string format, params object[] args)
        {
            if (EnableLoadTracing)
                Debug.WriteLine(format, args);
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Sets the method to be called to retrieve Windows Azure settings.  See
        /// the remarks for more information.
        /// </summary>
        /// <param name="method">The <b>Microsoft.WindowsAzure.CloudConfigurationManager.GetSetting(string)</b> method information.</param>
        /// <remarks>
        /// <para>
        /// Windows Azure based applications should call this method early in their
        /// initialization process.  The parameter is the <see cref="MethodInfo"/> for
        /// the method to be used to retrieve a standard Azure configuration setting.
        /// The <see cref="Config"/> class will call this method when getting a
        /// setting prefixed by <b>"Azure."</b>.
        /// </para>
        /// <para>
        /// Here's how you call this method:
        /// </para>
        /// <code language="cs">
        /// Config.SetAzureGetSettingMethod(typeof(Microsoft.WindowsAzure.CloudConfigurationManager).GetMethod("GetSetting");
        /// </code>
        /// </remarks>
        public static void SetAzureGetSettingMethod(MethodInfo method)
        {
            getAzureSetting = method;
        }

        /// <summary>
        /// Lookups up a Windows Azure setting.
        /// </summary>
        /// <param name="name">The setting name (must be prefixed by <b>"Azure."</b>).</param>
        /// <returns>The setting string if found, <c>null</c> otherwise.</returns>
        private static string GetAzureSetting(string name)
        {
            if (getAzureSetting == null || !name.ToLowerInvariant().StartsWith("azure."))
                return null;

            return (string)getAzureSetting.Invoke(null, new object[] { name.Substring("azure.".Length) });
        }

        /// <summary>
        /// Returns the global configuration for the current application.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This configuration is not prefixed so fully qualified key names will need
        /// to be used.
        /// </para>
        /// <para>
        /// This static property is useful in situations where access to configuration
        /// is necessary but the hassle of passing a configuration instance around to
        /// to great or the overhead of instantiating a new Config instance is too much.
        /// This property instantiates an instance the first time this is called and then
        /// reuses it there after.
        /// </para>
        /// </remarks>
        public static Config Global
        {
            get
            {
                lock (syncLock)
                {
                    if (global == null)
                        global = new Config();

                    return global;
                }
            }
        }

        /// <summary>
        /// This property controls whether environment variables will be automatically
        /// processed for all configuration values.
        /// </summary>
        /// <remarks>
        /// This functionality is enabled by default.  Set this property to <c>false</c> to
        /// disable environment variable processing.  Then <see cref="GetEnv" /> can be used to
        /// explicitly process environment variables for specific keys.
        /// </remarks>
        public static bool ProcessEnvironmentVars { get; set; }

        /// <summary>
        /// Loads or reloads the <see cref="Global"/> config collection will the application configuration.
        /// </summary>
        public static void Load()
        {
            lock (syncLock)
            {
                LoadTrace("*******************************************************************************");
                LoadTrace("**                            CONFIG LOADING                                 **");
                LoadTrace("*******************************************************************************");

                global = new Config();
            }
        }

        /// <summary>
        /// Creates an empty <see cref="Config" /> instance.
        /// </summary>
        /// <returns>The new instance.</returns>
        public static Config CreateEmpty()
        {
            return new Config(true);
        }

        /// <summary>
        /// Used by unit tests to clear the Global configuration instance (if present)
        /// so it will can be reloaded by a test suite.
        /// </summary>
        internal static void ClearGlobal() 
        {
            lock (syncLock) 
            {
                ProcessEnvironmentVars = true;
                providerLoad           = false;
                devOverride            = null;
                devOverrideLoad        = false;
                global                 = null;
            }
        }

        /// <summary>
        /// Initializes the configuration settings such that the settings are
        /// parsed from the string passed rather than reading them from a
        /// file.
        /// </summary>
        /// <param name="settings">
        /// The settings in the cross platform format or <c>null</c> or the empty string
        /// to indicate that settings should be loaded from the application's 
        /// configuration file.
        /// </param>
        public static void SetConfig(string settings)
        {
            if (settings != null && settings.Length == 0)
                settings = null;

            lock (syncLock)
            {
                ProcessEnvironmentVars = true;
                configText             = settings;
#if !MOBILE_DEVICE
                providerLoad           = false;
#endif
                devOverride            = null;
                devOverrideLoad        = false;
                global                 = null;
            }

            // The configuation system is initialized so give Helper a chance to 
            // complete its intialization.

            Helper.LoadGlobalConfig();
        }

        /// <summary>
        /// Combines two configuration key prefixes by adding a period (.) between
        /// them as required.
        /// </summary>
        /// <param name="key1">The first key prefix.</param>
        /// <param name="key2">The second key prefix.</param>
        /// <returns>The combined prefixes.</returns>
        /// <remarks>
        /// This method handle <c>null</c> and empty strings properly.
        /// </remarks>
        public static string CombineKeys(string key1, string key2)
        {
            if (key1 != null && key1.EndsWith("."))
                key1 = key1.Substring(0, key1.Length - 1);

            if (key2 != null && key2.StartsWith("."))
                key2 = key2.Substring(1);

            if (string.IsNullOrWhiteSpace(key1))
                return key2;
            else if (string.IsNullOrWhiteSpace(key2))
                return key1;

            return key1 + "." + key2;
        }

        /// <summary>
        /// Appends the configuration settings passed to any existing settings.
        /// </summary>
        /// <param name="settings">The settings in the cross platform format.</param>
        public static void AppendConfig(string settings)
        {
            lock (syncLock)
            {
                if (configText == null)
                    configText = string.Empty;
                else
                    configText += "\r\n";

                configText += settings;
#if !MOBILE_DEVICE
                providerLoad = false;
#endif
                devOverride = null;
                devOverrideLoad = false;
                global = null;
            }
        }

        /// <summary>
        /// Initializes the configuration file location to the configuration file
        /// co-located with and with the same name as the application's entry assembly.
        /// </summary>
        /// <param name="mainAssembly">The application's main assembly.</param>
        /// <exception cref="NotImplementedException">Thrown for Silverlight.</exception>
        /// <remarks>
        /// <para>
        /// This method must be called for WINCE applications before instantiating
        /// and instance of this class.  For WINFULL environments, this must be called
        /// to use the cross platform configuration file format.
        /// </para>
        /// <para>
        /// The method generates the configuration file path by appending the
        /// file name passed onto the end of the fully qualified path to the
        /// assembly.
        /// </para>
        /// <note>
        /// This method is not implemented for Silverlight.
        /// </note>
        /// </remarks>
        public static void SetConfigPath(Assembly mainAssembly)
        {
#if SILVERLIGHT
            throw new NotImplementedException();
#else
            string  path;
            int     p;

            path       = mainAssembly.GetName(false).CodeBase;
            p          = path.LastIndexOf('.');
            configPath = path.Substring(0, p) + ".ini";

            if (configPath.StartsWith("file:///"))
                configPath = configPath.Substring("file:///".Length);

            // The configuation system is initialized so give Helper a chance to 
            // complete its intialization.

            Helper.LoadGlobalConfig();
#endif
        }

        /// <summary>
        /// Sets the configuration file to a fully qualified path.
        /// </summary>
        /// <param name="path">The fully qualified path of the configuration file.</param>
        /// <remarks>
        /// <para>
        /// This method must be called by ASP.NET applications within the <b>Application_Start</b>
        /// event handler in the <b>Global.asax</b> file before any <see cref="Config" />
        /// instances can be created.  Most ASP.NET applications will pass
        /// <b>System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath + "Web.ini"</b> as
        /// the parameter to this method.
        /// </para>
        /// <para>
        /// For Silverlight environments, this will be the path to the configuration file
        /// in the user's isolated storage space.
        /// </para>
        /// </remarks>
        public static void SetConfigPath(string path)
        {
            lock (syncLock)
            {
                configPath = path;

                // Force a reload of the global settings instance.

                global = null;
                Config.Global.Get("Ping");

                // The configuation system is initialized so give Helper a chance to 
                // complete its intialization.

                Helper.LoadGlobalConfig();
            }
        }

        /// <summary>
        /// Initializes the configuration file location to a co-located with the
        /// the application's entry assembly but with a custom file name.
        /// </summary>
        /// <param name="mainAssembly">The application's main assembly.</param>
        /// <param name="fileName">The name of the configuration file.</param>
        /// <exception cref="NotImplementedException">Thrown for Silverlight.</exception>
        /// <remarks>
        /// <note>
        /// This method is not implemented for Silverlight.
        /// </note>
        /// <para>
        /// This method must be called for WINCE applications before instantiating
        /// and instance of this class.  For WINFULL environments, this must be called
        /// to use the cross platform configuration file format.
        /// </para>
        /// <para>
        /// The method generates the configuration file path by appending the
        /// file name passed onto the end of the fully qualified path to the
        /// assembly.
        /// </para>
        /// </remarks>
        public static void SetConfigPath(Assembly mainAssembly, string fileName)
        {
#if SILVERLIGHT
            throw new NotImplementedException();
#else
            string  path;
            int     pos;

            path = mainAssembly.GetName(false).CodeBase;
            pos  = path.LastIndexOf('/');
            path = path.Substring(0, pos + 1);

            configPath = path + fileName;

            // The configuation system is initialized so give Helper a chance to 
            // complete its intialization.

            Helper.LoadGlobalConfig();
#endif
        }

        /// <summary>
        /// Returns the path to the application's configuration file or <c>null</c>
        /// if <see cref="SetConfigPath(Assembly)" /> has not been called.
        /// </summary>
        public static string ConfigPath
        {
            get { return configPath; }
        }

#if !WINDOWS_PHONE

        /// <summary>
        /// Returns the fully qualified path of the configuration file for an assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The configuration file path.</returns>
        /// <remarks>
        /// <note>
        /// This method works only if the assembly resides on the
        /// local file system.
        /// </note>
        /// </remarks>
        public static string GetConfigPath(Assembly assembly)
        {
            string  path = assembly.CodeBase;
            int     pos;

            if (!path.StartsWith("file://"))
                throw new ArgumentException("Assembly must reside on the local file system.");

            path = Helper.StripFileScheme(path);
            pos  = path.LastIndexOf('.');

            if (pos == -1)
                return path + ".ini";
            else
                return path.Substring(0, pos) + ".ini";
        }

#endif

#if !SILVERLIGHT

        /// <summary>
        /// Rewrites a configuration file by changing the value of the specified #define macro.
        /// </summary>
        /// <param name="path">The configuration file path.</param>
        /// <param name="encoding">The character encoding of the string.</param>
        /// <param name="macro">The macro name.</param>
        /// <param name="value">The new value.</param>
        /// <returns><c>true</c> if the macro was found and changed.</returns>
        public static bool EditMacro(string path, Encoding encoding, string macro, string value)
        {
            FileStream fs;

            fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            try
            {
                return EditMacro(fs, encoding, macro, value);
            }
            finally
            {

                fs.Close();
            }
        }

        /// <summary>
        /// Rewrites a configuration file referenced as stream by changing 
        /// the value of the specified #define macro.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="encoding">The character encoding of the string.</param>
        /// <param name="macro">The macro name.</param>
        /// <param name="value">The new value.</param>
        /// <returns><c>true</c> if the macro was found and changed.</returns>
        public static bool EditMacro(Stream input, Encoding encoding, string macro, string value)
        {

            StreamReader            reader;
            StreamWriter            writer;
            EnhancedMemoryStream    es;
            string                  line;
            string                  trimmed;
            string                  name;
            int                     pos;
            bool                    found;

            input.Seek(0, SeekOrigin.Begin);
            reader = new StreamReader(input, encoding);
            es     = new EnhancedMemoryStream();
            writer = new StreamWriter(es, encoding);
            found  = false;

            for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                trimmed = line.Trim();
                if (!trimmed.StartsWith("#define "))
                {
                    writer.WriteLine(line);
                    continue;
                }

                trimmed = trimmed.Substring(8).Trim();
                pos = trimmed.IndexOfAny(new char[] { ' ', '\t' });

                if (pos == -1)
                    name = trimmed;
                else
                    name = trimmed.Substring(0, pos);

                if (name != macro)
                    writer.WriteLine(line);
                else
                {

                    writer.WriteLine("#define {0} {1}", macro, value);
                    found = true;
                }
            }

            writer.Flush();
            input.SetLength(0);
            es.Position = 0;
            es.CopyTo(input, (int)es.Length);

            return found;
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Attempts to extract the key and default values encoded within
        /// the configuration reference string passed.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <param name="key">Returns as the configuration key name if one is present (null otherwise).</param>
        /// <param name="def">Returns as the configuration default value if one is present (null otherwise).</param>
        /// <returns><c>true</c> if the string encodes a key, <c>false</c> otherwise.</returns>
        /// <remarks>
        /// <para>
        /// Sometimes it's useful to be able able to distinugish between a
        /// constant configuration value and a configuration key/default value
        /// pair.  This can come up when configuration information needs to be
        /// specified in places other that the configuration file.  A good
        /// example of this use is when configuration settings needs to be specified
        /// in a method attribute.
        /// </para>
        /// <para>
        /// A configuration reference specifies a global configuration key and
        /// a string representing the default value to use if the key is not
        /// present in the configuration or there's a problem parsing its value.
        /// </para>
        /// <para>
        /// This method provides a standard mechanism for being able to specify a configuration
        /// key and a default value in situations like this by formatting the attribute
        /// property value as:
        /// </para>
        /// <code language="none">"[" config:&lt;key name&gt;[,&lt;default value&gt;] "]"</code>
        /// <para>
        /// where &lt;key name&gt; is the configuration key name and &lt;default value&gt;
        /// specifies an optional default value.  Here's an example:
        /// </para>
        /// <code language="none">
        /// [MyAttribute(Timeout="[config:MyApp.Timeout,10s")]
        /// public void Foo() {
        /// 
        /// }
        /// </code>
        /// <para>
        /// This example specifies that the <see cref="Timeout" /> property should be set 
        /// to the configuration value returned by Config.Global("MyApp").Get("Timeout","10s").
        /// </para>
        /// </remarks>
        public static bool GetConfigRef(string value, out string key, out string def)
        {
            const int cPrefix = 8;    // "[config:".Length

            int pos;

            key = null;
            def = null;

            if (!value.StartsWith("[") || !value.EndsWith("]"))
                return false;

            if (!value.ToLowerInvariant().StartsWith("[config:"))
                return false;

            value = value.Substring(cPrefix, value.Length - cPrefix - 1);

            pos = value.IndexOf(',');
            if (pos == -1)
                key = value.Trim();
            else
            {
                key = value.Substring(0, pos).Trim();
                def = value.Substring(pos + 1).Trim();
            }

            if (key.Length == 0)
                throw new ArgumentException("Invalid configuration key name.", "value");

            return true;
        }

#if !WINDOWS_PHONE

        /// <summary>
        /// A utility for rendering a type into a string form suitable for
        /// parsing into a type instance by <see cref="Parse(string,System.Type)" />.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="fullPath">
        /// Pass <c>true</c> to generate the fully qualified path to the type's assembly file,
        /// <c>false</c> if only the file name should be returned in the serialized result.
        /// </param>
        /// <returns>The serialized type.</returns>
        public static string SerializeType(System.Type type, bool fullPath)
        {
            return string.Format("{0}:{1}", type.FullName, fullPath ? type.Assembly.Location : Path.GetFileName(type.Assembly.Location));
        }

#endif

        /// <summary>
        /// Parses the configuration reference passed, returning a <b>string</b>.  If 
        /// the reference is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </remarks>
        public static string ParseValue(string value, string def)
        {
            string  key;
            string  sDef;
            string  vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <b>boolean</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </remarks>
        public static bool ParseValue(string value, bool def)
        {
            string  key;
            string  sDef;
            bool    vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning an <b>integer</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
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
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        /// </list>
        /// </note>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static int ParseValue(string value, int def)
        {
            string  key;
            string  sDef;
            int     vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <b>long</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
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
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static long ParseValue(string value, long def)
        {
            string  key;
            string  sDef;
            long    vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <b>double</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
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
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static double ParseValue(string value, double def)
        {
            string  key;
            string  sDef;
            double  vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <b>TimeSpan</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// Timespan values are encoded as floating point values terminated with
        /// one of the unit codes: "d" (days), "h" (hours), "m" (minutes), "s"
        /// (seconds), or "ms" (milliseconds).  If the unit code is missing then 
        /// seconds will be assumed.  An infinite timespan is encoded using the 
        /// literal "infinite".
        /// </para>
        /// <para>
        /// TimeSpan values may also be specified as: <b>&lt;d&gt;.&lt;hh&gt;:&lt;mm&gt;:&lt;ss&gt;</b>
        /// </para>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static TimeSpan ParseValue(string value, TimeSpan def)
        {
            string      key;
            string      sDef;
            TimeSpan    vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning an <b>IPAddress</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// IP addresses are formatted as dotted quads.
        /// </para>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static IPAddress ParseValue(string value, IPAddress def)
        {
            string      key;
            string      sDef;
            IPAddress   vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <see cref="NetworkBinding" />.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </para>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static NetworkBinding ParseValue(string value, NetworkBinding def)
        {
            string          key;
            string          sDef;
            NetworkBinding  vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <see cref="Guid" />.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// Guids are formatted as:
        /// </para>
        /// <blockquote>{D60CD61B-695D-45d6-94CD-D6E79DA5C48B}</blockquote>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static Guid ParseValue(string value, Guid def)
        {
            string  key;
            string  sDef;
            Guid    vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed, returning a <see cref="Uri" />.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        public static Uri ParseValue(string value, Uri def)
        {

            string  key;
            string  sDef;
            Uri     vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the enumeration value passed returning an <b>Enumeration</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static object ParseValue(string value, System.Type enumType, object def)
        {
            string  key;
            string  sDef;
            object  vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, enumType, def);

                    return Config.Global.Get(key, enumType, vDef);
                }
                else
                    return Config.Parse(value, enumType, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the enumeration value passed returning an <b>Enumeration</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type being parsed.</typeparam>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static TEnum ParseValue<TEnum>(string value, object def)
        {
            return (TEnum)ParseValue(value, typeof(TEnum), def);
        }

        /// <summary>
        /// Parses arbitrary structured types that implement <see cref="IParseable" />.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <typeparam name="TValue">The resulting type.</typeparam>
        /// <param name="value">The string form of the type.</param>
        /// <param name="def">The default value to be returned if the configuration setting is <c>null</c> or invalid.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static TValue ParseCustomValue<TValue>(string value, TValue def)
            where TValue : IParseable, new()
        {
            string  key;
            string  sDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                    return Config.Global.Get<TValue>(key, def);
                else
                    return Config.Parse<TValue>(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed returning a <b>System.Type</b>.  If the value
        /// is formatted to specify a configuration key, then this method will
        /// perform a configuration file lookup. 
        /// </summary>
        /// <param name="value">The value or configuration key/default pair.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value or the default value upon an error.</returns>
        /// <remarks>
        /// <para>
        /// This is a handy way to specify and then load plugin modules.  The
        /// type assembly and type should be encoded into the value as:
        /// </para>
        /// <code language="none">&lt;type&gt;:&lt;assembly path&gt;</code>
        /// <note>
        /// This method temporarily sets the current directory to 
        /// the application's directory while loading the assembly to ensure
        /// that any assemblies referenced by the one being loaded will
        /// also be found.
        /// </note>
        /// <para>
        /// See <see cref="GetConfigRef" /> for more information on how a configuration
        /// reference can be encoded into the value string.
        /// </para>
        /// </remarks>
        public static System.Type ParseValue(string value, System.Type def)
        {
            string          key;
            string          sDef;
            System.Type     vDef;

            try
            {
                if (value == null)
                    return def;

                if (Config.GetConfigRef(value, out key, out sDef))
                {
                    if (sDef == null)
                        vDef = def;
                    else
                        vDef = Config.Parse(sDef, def);

                    return Config.Global.Get(key, vDef);
                }
                else
                    return Config.Parse(value, def);
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Parses the value passed as a string, returning the default <b>string</b> value if
        /// the value is <c>null</c> or if there's an error.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        public static string Parse(string value, string def)
        {
            if (value == null)
                return def;
            else
                return value;
        }

        /// <summary>
        /// Parses the value passed as a boolean, returning the default <b>boolean</b> value if
        /// the value is not valid.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Boolean values may be encoded using the following literals: 0/1, on/off,
        /// yes/no, true/false, enable/disable, high/low.
        /// </remarks>
        public static bool Parse(string value, bool def)
        {
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as an integer, returning the default <b>integer</b> value if
        /// the value is not valid.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <returns>
        /// The parsed value.
        /// </returns>
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
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public static int Parse(string value, int def)
        {
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as a long, returning the default <b>long</b> value if
        /// the value is not valid.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <returns>
        /// The parsed value.
        /// </returns>
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
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses an <b>enumeration</b> value where the value is case insenstive.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static object Parse(string value, System.Type enumType, object def)
        {
            return Serialize.Parse(value, enumType, def);
        }

        /// <summary>
        /// Parses an <b>enumeration</b> value where the value is case insenstive.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type being parsed.</typeparam>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public static TEnum Parse<TEnum>(string value, object def)
        {
            return Serialize.Parse<TEnum>(value, def);
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
            return Serialize.ParseCustom<TValue>(value, def);
        }

        /// <summary>
        /// Parses the value passed as a <b>double</b>, returning the default value if
        /// the value passed is not valid.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <returns>
        /// The parsed value.
        /// </returns>
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
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as a <b>TimeSpan</b>, returning the default value if
        /// the value is not valid.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
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
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as an <b>IPAddress</b>, returning the default value if
        /// the value is <c>null</c> or if there's an error.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// IP addresses are formatted as dotted quads.
        /// </remarks>
        public static IPAddress Parse(string value, IPAddress def)
        {
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as a <see cref="NetworkBinding" />, returning the default value if
        /// the value is <c>null</c> or if there's an error.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Endpoints are formatted as &lt;dotted-quad&gt;:&lt;port&gt;.
        /// </remarks>
        public static NetworkBinding Parse(string value, NetworkBinding def)
        {
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as a <see cref="Guid" />, returning the default value if
        /// the value is <c>null</c> or if there's an error.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// <para>
        /// Guids are formatted as:
        /// </para>
        /// <blockquote>{051D920B-2989-4ba2-B85E-E2C4CDFEE2E9}</blockquote>
        /// </remarks>
        public static Guid Parse(string value, Guid def)
        {
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses the value passed as a <see cref="Uri" />, returning the default value if
        /// the value is <c>null</c> or if there's an error.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <param name="def">The default value.</param>
        public static Uri Parse(string value, Uri def)
        {
            return Serialize.Parse(value, def);
        }

        /// <summary>
        /// Parses an assembly path and type name encoded as &lt;type&gt;:&lt;assembly path&gt;
        /// and attempts to load the assembly and return the return the type instance 
        /// specified.
        /// </summary>
        /// <param name="value">The encoded assembly path and type name (or <c>null</c>).</param>
        /// <param name="def">The default value to be returned if the operation fails.</param>
        /// <returns>The System.Type instance referencing the requested type.</returns>
        /// <remarks>
        /// <para>
        /// This is a handy way to specify and then load plugin modules.  The
        /// type assembly and fully qualified type name should be encoded into the 
        /// value as:
        /// </para>
        /// <code language="none">&lt;type&gt;:&lt;assembly path&gt;</code>
        /// <note>
        /// This method temporarily sets the current directory to 
        /// the application's directory while loading the assembly to ensure
        /// that any assemblies referenced by the one being loaded will
        /// also be found.
        /// </note>
        /// <para>
        /// For normal applications, the current directory will be set to the 
        /// folder holding the application's entry assembly.  For ASP.NET
        /// applications, the directory will be set to <b>&lt;root&gt;\Bin</b>
        /// where <b>&lt;root&gt;</b> is the physical root of the ASP.NET
        /// application.
        /// </para>
        /// </remarks>
        public static System.Type Parse(string value, System.Type def)
        {
#if SILVERLIGHT
            throw new NotImplementedException();
#else
            string  assemblyPath;
            string  typeName;
            int     pos;
            string  curDir;

            if (value == null)
                return def;

            pos = value.IndexOf(':');
            if (pos == -1)
                return def;

            typeName = value.Substring(0, pos);
            assemblyPath = value.Substring(pos + 1);

            if (assemblyPath == string.Empty || typeName == string.Empty)
                return def;

            // First, check to see if the requested assembly is already loaded
            // into the AppDomain

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (String.Compare(assembly.Location, assemblyPath, StringComparison.OrdinalIgnoreCase) == 0)
                    return assembly.GetType(typeName, true);

            // Try loading the assembly file

            curDir = Environment.CurrentDirectory;
            if (Helper.EntryAssemblyFolder != null)
            {
                if (Helper.IsWebApp)
                    Environment.CurrentDirectory = Helper.EntryAssemblyFolder + Helper.PathSepString + "Bin";
                else
                    Environment.CurrentDirectory = Helper.EntryAssemblyFolder;
            }

            try
            {
                Assembly assembly;

                assembly = Assembly.LoadFrom(assemblyPath);
                return assembly.GetType(typeName, true);
            }
            catch
            {
                return def;
            }
            finally
            {

                Environment.CurrentDirectory = curDir;
            }
#endif // !SILVERLIGHT
        }

        //---------------------------------------------------------------------
        // Instance members

        private string  keyPrefix;      // String used to prefix key names

        // The key/value pairs

        private Dictionary<string, string> htKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // #define and #set macros

        private Dictionary<string, string> htDefines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Table of auto incrementing key arrays

        private Dictionary<string, List<string>> keyArrays = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Used to initialize a special instance that uses a hash table
        /// to retreive keys from rather than from the configuration file.
        /// This is used to facilitate unit tests.
        /// </summary>
        /// <param name="keyPrefix">The prefix string (or <c>null</c>).</param>
        /// <param name="unitTest">Pass as <c>true</c> to enable for unit testing</param>
        /// <remarks>
        /// The keyPrefix is a string to be prepended to every key name in
        /// all method calls below.  Note that a period (.) will be appended
        /// to the prefix if it's not empty or if it doesn't already end
        /// with a period.
        /// </remarks>
        internal Config(string keyPrefix, bool unitTest) : this(keyPrefix) 
        {
        }

        /// <summary>
        /// Adds the key/value pair to the hash table for unit testing.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentException">Thrown if an auto incrementing key array name is specified.</exception>
        /// <remarks>
        /// <note>
        /// This method does not support auto incrementing key arrays with
        /// names of the form <b>mysetting[-]</b>.
        /// </note>
        /// </remarks>
        internal void Add(string key, string value) 
        {
            key = key.Trim();
            if (key.EndsWith("[-]"))
                throw new ArgumentException("Config.Add() does not support auto incrementing key array names of the form: mysetting[-]");

            htKeys.Add(GetKey(key), value);
        }

        /// <summary>
        /// Clears the hash table (used for unit testing).
        /// </summary>
        internal void Clear() 
        {
            htKeys.Clear();
        }

        /// <summary>
        /// Perform basic initialization.
        /// </summary>
        private void Init()
        {
#if WINFULL
            htDefines["WINFULL"] = "WINFULL";
#endif
        }

        /// <summary>
        /// Loads the application configuration with no key prefix, binding to the standard Win32 configuration file 
        /// associated with this application.
        /// </summary>
        /// <remarks>
        /// For WINCE applications, <see cref="SetConfigPath(Assembly)" /> must have already been called.
        /// For WINFULL applications, a previous call to <see cref="SetConfigPath(Assembly)" /> will cause
        /// the configuration to be read from the cross platform file format rather
        /// than from the .NET configuration.
        /// </remarks>
        public Config()
            : this(false)
        {
        }

        /// <summary>
        /// Loads the application configuration or an empty configuration set.
        /// </summary>
        /// <param name="empty">
        /// Pass <c>true</c> to initialize an empty set, <c>false</c> to load the
        /// standard application configuration setttings.
        /// </param>
        public Config(bool empty)
        {
            if (empty)
            {
                this.keyPrefix = null;
                this.htKeys    = new Dictionary<string, string>();
                this.htDefines = new Dictionary<string, string>();
                this.keyArrays = new Dictionary<string, List<string>>();

                return;
            }

            this.keyPrefix = null;

            if (Config.ProcessEnvironmentVars)
                LoadEnvironment();

            Init();
            LoadStandard();

            if (configPath != null)
                Load(configPath);
            else if (configText != null)
                Load(new StringReader(configText), 0);

            LoadCustom();
            LoadOverride();
            MergeKeyArrays();
        }

        /// <summary>
        /// Initializes the class with the key prefix passed, binding to the
        /// standard Win32 configuration file associated with this
        /// application.
        /// </summary>
        /// <param name="keyPrefix">The prefix string (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The keyPrefix is a string to be prepended to every key name in
        /// all method calls below.  Note that a period (.) will be appended
        /// to the prefix if it's not empty or if it doesn't already end
        /// with a period.
        /// </para>
        /// <para>
        /// For WINCE applications, <see cref="SetConfigPath(Assembly)" /> must have already been called.
        /// For WINFULL applications, a previous call to <see cref="SetConfigPath(Assembly)" /> will cause
        /// the configuration to be read from the cross platform file format rather
        /// than from the .NET configuration.
        /// </para>
        /// </remarks>
        public Config(string keyPrefix)
        {
            if (keyPrefix != null && !keyPrefix.EndsWith("."))
                keyPrefix += ".";

            this.keyPrefix = keyPrefix;

            // I'm just going to copy all of the global configuration settings
            // into this instance so that we'll avoid loading the file multiple
            // times.

            lock (syncLock)
            {
                foreach (var pair in Config.Global.htKeys)
                    this.htKeys[pair.Key] = pair.Value;

                foreach (var pair in Config.Global.htDefines)
                    this.htDefines[pair.Key] = pair.Value;

                foreach (var pair in Config.Global.keyArrays)
                    this.keyArrays[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// Used to load the configuration file from the string passed,
        /// overriding any previous <see cref="SetConfigPath(string)" /> call.  This method
        /// is to be used only for unit testing.
        /// </summary>
        /// <param name="keyPrefix">The key prefix (or <c>null</c>).</param>
        /// <param name="configFile">The simulated contents of a configuration file.</param>
        /// <remarks>
        /// The keyPrefix is a string to be prepended to every key name in
        /// all method calls below.  Note that a period (.) will be appended
        /// to the prefix if it's not empty or if it doesn't already end
        /// with a period.
        /// </remarks>
        internal Config(string keyPrefix, string configFile)
        {
            if (keyPrefix != null && !keyPrefix.EndsWith("."))
                keyPrefix += ".";

            this.keyPrefix = keyPrefix;

            Init();
            Load(new StringReader(configFile), 0);
            LoadCustom();
            LoadOverride();
            MergeKeyArrays();
        }

        /// <summary>
        /// Creates a configuration by reading the configuration text from a
        /// stream.
        /// </summary>
        /// <param name="keyPrefix">The prefix string.</param>
        /// <param name="reader">References the configuration text in standard format.</param>
        /// <remarks>
        /// <note>
        /// This method will not attempt to load or process environment
        /// variables.
        /// </note>
        /// </remarks>
        public Config(string keyPrefix, TextReader reader)
        {
            if (keyPrefix != null && !keyPrefix.EndsWith("."))
                keyPrefix += ".";

            this.keyPrefix = keyPrefix;

            Init();
            Load(reader, 0);
            LoadCustom();
            LoadOverride();
            MergeKeyArrays();
        }

        /// <summary>
        /// Returns the key prefix including the terminating "." if prefix 
        /// is not the root.
        /// </summary>
        public string KeyPrefix
        {
            get
            {
                if (keyPrefix == null)
                    return string.Empty;
                else
                    return keyPrefix;
            }
        }

        /// <summary>
        /// Returns the number of settings in the configuration instance.
        /// </summary>
        public int Count
        {
            get { return htKeys.Count; }
        }

        /// <summary>
        /// Merges any auto incrementing key arrays into the configration settings.
        /// </summary>
        private void MergeKeyArrays()
        {
            foreach (var entry in keyArrays)
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    string key = string.Format("{0}[{1}]", entry.Key, i);

                    if (!htKeys.ContainsKey(key))
                        htKeys.Add(key, entry.Value[i]);
                }
        }

        /// <summary>
        /// Handles any necessary configuration provider processing.
        /// </summary>
        /// <remarks>
        /// Looks at the configuration settings loaded so far and determines whether a
        /// custom configuration provider is specified.  If so, the method will attempt
        /// to instantiate the provider instance and load the settings, overwriting the
        /// current settings if the operation is successful.
        /// </remarks>
        private void LoadCustom()
        {

#if !MOBILE_DEVICE

            if (providerLoad)
                return;     // Already done this

            providerLoad = true;

            try
            {
                Config              config = new Config("Config");
                System.Type         providerType;
                IConfigProvider     provider;
                ArgCollection       settings;
                string              cacheFile;
                string              machineName;
                string              exeFile;
                Version             exeVersion;
                string              usage;
                Assembly            mainAssembly;
                int                 pos;
                string              s;
                string              result;

                // Get any custom provider parameters

                providerType = config.Get("CustomProvider", (System.Type)null);
                if (providerType == null)
                    return;

                mainAssembly = Helper.GetEntryAssembly();
                exeFile      = Helper.EntryAssemblyFile;

                if (Helper.IsWebApp)
                {
                    cacheFile = Helper.EntryAssemblyFolder + Helper.PathSepString + "web.cache.ini";
                }
                else
                {
                    cacheFile = mainAssembly.CodeBase;
                    pos       = cacheFile.LastIndexOf('.');
                    cacheFile = cacheFile.Substring(0, pos) + ".cache.ini";
                }

                settings    = ArgCollection.Parse(config.Get("Settings", string.Empty));
                cacheFile   = config.Get("CacheFile", cacheFile);
                machineName = config.Get("MachineName", Helper.MachineName);
                exeFile     = config.Get("ExeFile", exeFile);
                usage       = config.Get("Usage", string.Empty);

                try
                {
                    s = config.Get("ExeVersion");
                    if (s == null)
                        exeVersion = mainAssembly.GetName().Version;
                    else
                        exeVersion = new Version(s);
                }
                catch
                {
                    exeVersion = mainAssembly.GetName().Version;
                }

                // Instantiate and then call the provider

                provider = Helper.CreateInstance<IConfigProvider>(providerType);
                result   = provider.GetConfig(settings, cacheFile, machineName, exeFile, exeVersion, usage);

                if (result == null)
                    return;

                configText = result;

                // Parse the returned settings

                htDefines.Clear();
                htKeys.Clear();

                if (Config.ProcessEnvironmentVars)
                    LoadEnvironment();

                Init();
                LoadStandard();
                Load(new StringReader(configText), 0);
            }
            catch (Exception e)
            {
                SysLog.LogException(e, "Error processing custom config provider.");
            }

#endif // !MOBILE_DEVICE
        }

        /// <summary>
        /// Loads any override configuration settings in the file referenced
        /// by the "LillTek.ConfigOverride" environment variable.
        /// </summary>
        private void LoadOverride()
        {
            if (!devOverrideLoad)
            {
                // Load the file if one is specified.

                string path;

                devOverrideLoad = true;
                devOverride = new Config(true);

                path = EnvironmentVars.Get("LillTek.ConfigOverride");
                if (path != null)
                {
                    path = EnvironmentVars.Expand(path);

                    try
                    {
                        devOverride.Load(path);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e, "Error loading configuration override file [{0}].", path);
                    }
                }
            }

            if (devOverride == null)
                return;

            // Copy the override settings into this instances settings.

            foreach (string key in devOverride.htKeys.Keys)
                this.htKeys[key] = devOverride.htKeys[key];
        }

        /// <summary>
        /// Searches for any environment variables that match the key prefix and
        /// adds them to the configuration.
        /// </summary>
        private void LoadEnvironment()
        {
#if !MOBILE_DEVICE

            IDictionary<string, string> vars = EnvironmentVars.GetAll();
            string lwrKeyPrefix = keyPrefix == null ? null : keyPrefix.ToLowerInvariant();

            foreach (string name in vars.Keys)
            {
                var nameLwr = name.ToLowerInvariant();

                if (lwrKeyPrefix == null ||
                    nameLwr.StartsWith(lwrKeyPrefix) ||
                    nameLwr.StartsWith(".config"))
                {
                    htKeys[name] = vars[name];

                    // Special case variables with names of the form "AZURE_*" by adding them as 
                    // "AZURE.*" as well.  This is used to by non-Azure processes running in the
                    // context of an Azure role to pick up settings saved to the enviroment
                    // by AzureHelper.Initialize().

                    if (nameLwr.StartsWith("azure_"))
                        htKeys["AZURE." + nameLwr.Substring(6).ToUpper()] = vars[name];
                }
            }
#endif // !MOBILE_DEVICE
        }

        /// <summary>
        /// Loads any standard .NET XML configuration settings that start with the
        /// key prefix (this is a NOP on WINCE).
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method always loads keys that are prefixed by "Config." to support
        /// custom config providers.
        /// </note>
        /// </remarks>
        private void LoadStandard()
        {
#if WINFULL
            string      lwrKeyPrefix = keyPrefix == null ? null : keyPrefix.ToLowerInvariant();
            string[]    keys;
            string      name;
            string      value;

            keys = ConfigurationManager.AppSettings.AllKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                name = keys[i];
                if (lwrKeyPrefix == null ||
                    name.ToLowerInvariant().StartsWith(lwrKeyPrefix) ||
                    name.ToLowerInvariant().StartsWith("config."))
                {

                    value = ConfigurationManager.AppSettings[name];
                    htKeys[name] = value;
                }
            }
#endif
        }

        /// <summary>
        /// Specifies the type of conditional statement.
        /// </summary>
        private enum ConditionalType
        {
            If,
            Switch
        }

        /// <summary>
        /// Used below for tracking #if and #switch statements.
        /// </summary>
        private sealed class ConditionalState
        {
            public ConditionalType  ConditionalType;
            public bool             Enabled;
            public string           SwitchValue;    // The #switch value
            public bool             SwitchMatch;    // True if we've matched a #case

            public ConditionalState(ConditionalType conditionalType, bool enabled)
            {
                this.ConditionalType = conditionalType;
                this.Enabled         = enabled;
                this.SwitchValue     = null;
                this.SwitchMatch     = false;
            }

            public ConditionalState(ConditionalType conditionalType, bool enabled, string switchValue)
            {

                this.ConditionalType = conditionalType;
                this.Enabled         = enabled;
                this.SwitchValue     = switchValue;
                this.SwitchMatch     = false;
            }
        }

        /// <summary>
        /// Loads the configuration settings from the reader passed.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="includeNesting">The <b>#include</b> nesting level.</param>
        private void Load(TextReader reader, int includeNesting)
        {
            try
            {
                string                      lwrKeyPrefix = keyPrefix == null ? null : keyPrefix.ToLowerInvariant();
                int                         lineNum;
                string                      line;
                string                      orgLine;
                string                      name;
                string                      nameLwr;
                string                      value;
                int                         pos;
                bool                        enable = true;
                Stack<ConditionalState>     conditionalStack = new Stack<ConditionalState>();
                string                      section          = string.Empty;
                Stack<string>               sectionStack     = new Stack<string>();

                lineNum = 0;
                orgLine = string.Empty;
                line = reader.ReadLine();
                while (line != null)
                {
                    lineNum++;
                    orgLine = line;
                    line    = line.TrimStart();
                    if (line.Length == 0 || line.StartsWith("<") || line.StartsWith("//") || line.StartsWith("--"))
                    {
                        // Ignore blank lines and comments

                        line = reader.ReadLine();
                        continue;
                    }
                    else if (line[0] == '#')
                    {
                        // Parse config statements

                        LoadTrace("LINE[{0}, enable={1}]: {2}", lineNum, enable, line.Trim());
                        if (line.StartsWith("#define "))
                        {
                            if (enable)
                            {
                                // The line can be of the form
                                //
                                //      #define name
                                //
                                // or
                                //
                                //      #define name value

                                line = line.Substring(8).Trim();
                                pos  = line.IndexOfAny(new char[] { ' ', (char)0x09 });
                                if (pos == -1)
                                {
                                    name = line;
                                    if (name.Length == 0)
                                        throw new ConfigFormatException("Invalid identifier.", lineNum, orgLine);

                                    htDefines[name] = name.Trim();
                                    LoadTrace("#define {0}", name);
                                }
                                else
                                {
                                    name = line.Substring(0, pos).Trim();
                                    if (name.Length == 0)
                                        throw new ConfigFormatException("Invalid identifier.", lineNum, orgLine);

                                    value = line.Substring(pos).Trim();
                                    htDefines[name] = value;
                                    LoadTrace("#define {0} {1}", name, htDefines[name]);
                                }
                            }
                        }
                        else if (line.StartsWith("#set "))
                        {
                            if (enable)
                            {
                                // The line can be of the form
                                //
                                //      #set name
                                //
                                // or
                                //
                                //      #set name value

                                line = line.Substring(5).Trim();
                                pos  = line.IndexOfAny(new char[] { ' ', (char)0x09 });
                                if (pos == -1)
                                {
                                    name = line.Trim();
                                    if (name.Length == 0)
                                        throw new ConfigFormatException("Invalid identifier.", lineNum, orgLine);

                                    nameLwr = name.ToLowerInvariant();
                                    if (nameLwr == "true" || nameLwr == "false" || nameLwr.StartsWith("azure."))
                                        throw new ConfigFormatException(string.Format("Cannot redefine [{0}].", name), lineNum, orgLine);

                                    htDefines[name] = name;
                                    LoadTrace("#set {0}", name);
                                }
                                else
                                {
                                    name = line.Substring(0, pos).Trim();
                                    if (name.Length == 0)
                                        throw new ConfigFormatException("Invalid identifier.", lineNum, orgLine);

                                    value = line.Substring(pos).Trim();

                                    nameLwr = name.ToLowerInvariant();
                                    if (nameLwr == "true" || nameLwr == "false" || nameLwr.StartsWith("azure."))
                                        throw new ConfigFormatException(string.Format("Cannot redefine [{0}].", name), lineNum, orgLine);

                                    htDefines[name] = Expand(value, 0);
                                    LoadTrace("#set {0} {1}", name, htDefines[name]);
                                }
                            }
                        }
                        else if (line.StartsWith("#undef "))
                        {
                            line = line.Substring(7).Trim();
                            name = line;
                            if (name.Length == 0)
                                throw new ConfigFormatException("Invalid identifier.", lineNum, orgLine);

                            nameLwr = name.ToLowerInvariant();
                            if (nameLwr == "true" || nameLwr == "false" || nameLwr.StartsWith("azure."))
                                throw new ConfigFormatException(string.Format("Cannot undefine [{0}].", name), lineNum, orgLine);

                            if (htDefines.ContainsKey(name))
                                htDefines.Remove(name);
                        }
                        else if (line.StartsWith("#section "))
                        {
                            name = line.Substring(9).Trim();
                            if (name.Length == 0)
                                throw new ConfigFormatException("Key prefix expected.", lineNum, orgLine);

                            if (name.IndexOf("[-]") != -1)
                                throw new ConfigFormatException("Auto incrementing key array indexes not allowed in configuration sections.", lineNum, orgLine);

                            sectionStack.Push(section);

                            if (section == string.Empty)
                                section = name;
                            else
                                section += name;

                            if (!section.EndsWith("."))
                                section += '.';
                        }
                        else if (line.StartsWith("#endsection"))
                        {
                            if (sectionStack.Count == 0)
                                throw new ConfigFormatException("Not within a #section statement.", lineNum, orgLine);

                            section = sectionStack.Pop();
                        }
                        else if (line.StartsWith("#if "))
                        {
                            bool not;
                            bool v;

                            name = line.Substring(4).Trim();
                            if (name.Length == 0)
                                throw new ConfigFormatException("Invalid conditional identifier.", lineNum, orgLine);

                            not = false;
                            if (name[0] == '!')
                            {
                                not = true;
                                name = name.Substring(1);
                            }

                            if (name.Length == 0)
                                throw new ConfigFormatException("Invalid conditional identifier.", lineNum, orgLine);

                            conditionalStack.Push(new ConditionalState(ConditionalType.If, enable));

                            nameLwr = name.ToLowerInvariant();
                            if (nameLwr == "true")
                                v = true;
                            else if (nameLwr == "false")
                                v = false;
                            else if (htDefines.ContainsKey(nameLwr))
                                v = true;
                            else if (GetAzureSetting(nameLwr) != null)
                                v = true;
                            else
                                v = EnvironmentVars.Get(nameLwr) != null;

                            LoadTrace("#if: value={0}", v);
                            if (not)
                                enable = enable && !v;
                            else
                                enable = enable && v;
                        }
                        else if (line.StartsWith("#else"))
                        {
                            if (conditionalStack.Count == 0 || conditionalStack.Peek().ConditionalType != ConditionalType.If)
                                throw new ConfigFormatException("Not within an #if statement.", lineNum, orgLine);

                            enable = !enable && conditionalStack.Peek().Enabled;
                        }
                        else if (line.StartsWith("#endif"))
                        {
                            if (conditionalStack.Count == 0 || conditionalStack.Peek().ConditionalType != ConditionalType.If)
                                throw new ConfigFormatException("Not within an #if statement.", lineNum, orgLine);

                            enable = conditionalStack.Pop().Enabled;
                        }
                        else if (line.StartsWith("#switch "))
                        {
                            string v;

                            name = line.Substring(8).Trim();
                            if (name.Length == 0)
                                throw new ConfigFormatException("Invalid conditional identifier.", lineNum, orgLine);

                            nameLwr = name.ToLowerInvariant();

                            htKeys.TryGetValue(nameLwr, out v);
                            if (v == null)
                            {
                                htDefines.TryGetValue(nameLwr, out v);
                                if (v == null)
                                {

                                    v = GetAzureSetting(nameLwr);
                                    if (v == null)
                                        v = EnvironmentVars.Get(nameLwr);
                                }
                            }

                            if (v == null)
                                v = string.Empty;

                            LoadTrace("#switch: value={0}", v);
                            conditionalStack.Push(new ConditionalState(ConditionalType.Switch, enable, v));
                        }
                        else if (line.StartsWith("#case "))
                        {
                            ConditionalState    state;
                            string              v;

                            if (conditionalStack.Count == 0 || conditionalStack.Peek().ConditionalType != ConditionalType.Switch)
                                throw new ConfigFormatException("Not within an #switch statement.", lineNum, orgLine);

                            state = conditionalStack.Peek();

                            v = line.Substring(6).Trim();
                            if (String.Compare(state.SwitchValue, v, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                LoadTrace("#case: value={0} MATCH", v);
                                state.SwitchMatch = true;
                                enable = state.Enabled;
                            }
                            else
                            {
                                LoadTrace("#case: value={0} NO-MATCH", v);
                                enable = false;
                            }
                        }
                        else if (line.StartsWith("#default"))
                        {
                            ConditionalState state;

                            if (conditionalStack.Count == 0 || conditionalStack.Peek().ConditionalType != ConditionalType.Switch)
                                throw new ConfigFormatException("Not within an #switch statement.", lineNum, orgLine);

                            state = conditionalStack.Peek();
                            enable = state.Enabled && !state.SwitchMatch;

                            LoadTrace("#default: {0}", state.SwitchMatch ? "NO-MATCH" : "MATCH");
                        }
                        else if (line.StartsWith("#endswitch"))
                        {
                            if (conditionalStack.Count == 0 || conditionalStack.Peek().ConditionalType != ConditionalType.Switch)
                                throw new ConfigFormatException("Not within an #switch statement.", lineNum, orgLine);

                            enable = conditionalStack.Pop().Enabled;
                        }
                        else if (line.StartsWith("#include"))
                        {
                            if (includeNesting >= 4)
                                throw new ConfigFormatException("#include nesting cannot exceed a depth of 4.", lineNum, orgLine);

                            string includePath = Path.Combine(Helper.AppFolder, line.Substring(8).Trim());
                            string includeText;

                            LoadTrace("********** BEGIN INCLUDE {0} **********", includePath);
                            try
                            {
                                includeText = File.ReadAllText(includePath);
                            }
                            catch
                            {
                                throw new ConfigFormatException("Cannot load the include file.", lineNum, orgLine);
                            }

                            using (var includeReader = new StringReader(includeText))
                            {

                                Load(includeReader, includeNesting + 1);
                            }

                            LoadTrace("********** END INCLUDE {0} **********", includePath);
                        }
                        else
                        {
                            throw new ConfigFormatException("Invalid config command.", lineNum, orgLine);
                        }

                        line = reader.ReadLine();
                        continue;
                    }

                    LoadTrace("LINE[{0}, enable={1}]: {2}", lineNum, enable, line.Trim());
                    pos = line.IndexOf('=');
                    if (pos == -1)
                    {
                        // Ignore lines without an "=" sign

                        line = reader.ReadLine();
                        continue;
                    }

                    name  = section + line.Substring(0, pos).Trim();
                    value = line.Substring(pos + 1).Trim();

                    if (value.StartsWith("{{"))
                    {
                        var sb = new StringBuilder(256);

                        // This is a multi-line value.  Continue reading lines until
                        // we encounter the terminating "}}".

                        sb.Append(value, 2, value.Length - 2);
                        sb.Append("\r\n");

                        line = reader.ReadLine();
                        while (line != null)
                        {
                            line = line.Trim();
                            if (line.StartsWith("}}"))
                                break;

                            sb.Append(line);
                            sb.Append("\r\n");

                            line = reader.ReadLine();
                        }

                        value = sb.ToString().Trim();
                    }

                    // Add the value only if the we're not in a FALSE #if, the name is non-empty and its prefixed
                    // by the keyPrefix (if present), or the key is prefixed by "config.".  Note that key names
                    // ending with "[-]" are considered to be auto incrementing key array values and will be 
                    // added to the keyArrays dictionary to be processed later.

                    if (enable)
                        LoadTrace("*** SETTING: {0} = [{1}]", name, value);

                    if (enable && name.Length > 0 &&
                        (lwrKeyPrefix == null || name.ToLowerInvariant().StartsWith(lwrKeyPrefix) || name.ToLowerInvariant().StartsWith("config.")))
                    {
                        if (name.EndsWith("[-]"))
                        {
                            string arrayName = name.Substring(0, name.Length - 3);
                            List<string> keyArray;

                            if (!keyArrays.TryGetValue(arrayName, out keyArray))
                            {
                                keyArray = new List<string>();
                                keyArrays.Add(arrayName, keyArray);
                            }

                            keyArray.Add(value);
                        }
                        else
                            htKeys[name] = value;
                    }

                    line = reader.ReadLine();
                }

                if (conditionalStack.Count > 0)
                {
                    throw new ConfigFormatException(string.Format("#end{0} expected.", conditionalStack.Peek().ConditionalType.ToString().ToLowerInvariant()),
                                                    lineNum, orgLine);
                }

                if (sectionStack.Count > 0)
                    throw new ConfigFormatException("#endsection expected.", lineNum, orgLine);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                throw e;
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Loads the configuration file whose full path name is passed.
        /// </summary>
        /// <param name="path">Fully qualified path to the configration file.</param>
        /// <remarks>
        /// <note>
        /// For Silverlight environments, the file path will reference a file in the
        /// user's isolated storage space.
        /// </note>
        /// </remarks>
        private void Load(string path)
        {
            StreamReader reader = null;

#if !SILVERLIGHT

            if (!File.Exists(path))
                return;

            try
            {
                path   = Helper.StripFileScheme(path);
                reader = new StreamReader(path, Encoding.UTF8);
            }
            catch
            {
                // Ignore errors
            }

            if (reader != null)
                Load(reader, 0);
#else
            using (var store = IsolatedStorageFile.GetUserStoreForApplication()) 
            {
                try {

                    if (path.StartsWith("\\") || path.StartsWith("/"))
                        path = path.Substring(1);

                    reader = new StreamReader(store.OpenFile(path,FileMode.Open,FileAccess.Read),Encoding.UTF8);
                }
                catch 
                {
                    // Ignore errors
                }

                if (reader != null)
                    Load(reader, 0);
            }
#endif
        }

        /// <summary>
        /// Serializes the configuration settings to the configuration file at the path specified 
        /// by an earlier call to <b>SetConfigPath()</b>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>SetConfigPath()</b> has not be called.</exception>
        /// <remarks>
        /// This is generally useful only for Silverlight or Windows Phone applications where the
        /// configuration file is located in isolated user storage.
        /// </remarks>
        public void Save()
        {
            // The code below will first write the configuration data to a temporary file
            // and then replace the existing file in the hope to avoid file corruption.

            if (configPath == null)
                throw new InvalidOperationException("SetConfigPath() has not been called.");

#if !SILVERLIGHT
            var path     = Helper.StripFileScheme(configPath);
            var tempPath = path + ".tmp";

            using (var writer = new StreamWriter(tempPath, false, Encoding.UTF8))
            {
                foreach (var setting in htKeys)
                {
                    if (!EnvironmentVars.IsVariable(setting.Key))
                        writer.WriteLine("{0} = {1}", setting.Key, setting.Value);
                }
            }

            // Trying to keep this as atomic as possible.

            File.Delete(path);
            File.Copy(tempPath, path);
            File.Delete(tempPath);
#else
            var path = configPath;

            if (path.StartsWith("\\") || path.StartsWith("/"))
                path = path.Substring(1);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication()) 
            {
                using (var writer = new StreamWriter(store.CreateFile(path),Encoding.UTF8)) 
                {
                    foreach (var setting in htKeys) 
                    {
                        if (!EnvironmentVars.IsVariable(setting.Key))
                            writer.WriteLine("{0} = {1}",setting.Key,setting.Value);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Prepends the key prefix to the key passed and returns the result.
        /// </summary>
        /// <param name="key">The key.</param>
        private string GetKey(string key)
        {
            if (keyPrefix == null)
                return key;

            return keyPrefix + key;
        }

        /// <summary>
        /// Handles the recursive expansion of a configuration value.
        /// </summary>
        /// <param name="input">The string containing the variables to expand.</param>
        /// <param name="nesting">The nesting level.</param>
        /// <returns>The input string with exapanded variables.</returns>
        private string Expand(string input, int nesting)
        {
            if (nesting >= 16)
                throw new StackOverflowException("Too many nested macro variable expansions.");

            StringBuilder   sb;
            int             p, pStart, pEnd;
            string          name;
            string          value;

            // Return right away if there's no macro characters in the string.

            if (input.LastIndexOfAny(macroChars) == -1)
                return input;

            // Process variables of the form %name%

            sb = new StringBuilder(input.Length + 64);
            p = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent (%) characters.

                pStart = input.IndexOf('%', p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf('%', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 1, pEnd - pStart - 1);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                value = null;

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                value = GetAzureSetting(name);
                if (value == null)
                {
                    htKeys.TryGetValue(name, out value);
                    if (value == null)
                    {

                        htDefines.TryGetValue(name, out value);
                        if (value == null && Config.ProcessEnvironmentVars)
                            value = EnvironmentVars.Get(name);
                    }
                }

                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                    sb.Append(Expand(value, nesting + 1));

                // Advance past this definition

                p = pEnd + 1;
            }

            input = sb.ToString();

            // Process variables of the form $(name)

            sb = new StringBuilder(input.Length + 64);
            p = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent $(name) characters.

                pStart = input.IndexOf("$(", p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf(')', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 2, pEnd - pStart - 2);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                value = GetAzureSetting(name);
                if (value == null)
                {
                    htKeys.TryGetValue(name, out value);
                    if (value == null)
                    {
                        htDefines.TryGetValue(name, out value);
                        if (value == null && Config.ProcessEnvironmentVars)
                            value = EnvironmentVars.Get(name);
                    }
                }

                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                    sb.Append(Expand(value, nesting + 1));

                // Advance past this definition

                p = pEnd + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, string value)
        {
            htKeys[key] = value;
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, bool value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, double value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, object value)
        {
            htKeys[key] = value.ToString();
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, Guid value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, byte[] value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, int value)
        {

            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, long value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, IPAddress value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, NetworkBinding value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, TimeSpan value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Adds or modifies a key within the configuration.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// This method does not perform any macro or environment variable expansions.
        /// The configuration values will be added as they are passed.
        /// </note>
        /// </remarks>
        public void Set(string key, Uri value)
        {
            htKeys[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Removes a key from the configuration if it is present.
        /// </summary>
        /// <param name="key">The fully qualified key name.</param>
        /// <remarks>
        /// <note>
        /// This method does not throw an exception if the requested key is not
        /// present in the configuration.
        /// </note>
        /// </remarks>
        public void Remove(string key)
        {
            if (htKeys.ContainsKey(key))
                htKeys.Remove(key);
        }

        /// <summary>
        /// Returns a new <see cref="Config" /> instance holding only the key/values pairs
        /// within the specified subsection of the current <see cref="Config" /> instance.
        /// </summary>
        /// <param name="sectionKey">The section key.</param>
        /// <returns>The subsection configuration.</returns>
        public Config GetSection(string sectionKey)
        {
            var config = new Config(null, string.Empty);

            if (sectionKey == null)
                sectionKey = string.Empty;

            if (!string.IsNullOrWhiteSpace(sectionKey) && !sectionKey.EndsWith("."))
            {
                sectionKey += ".";

                if (keyPrefix != null)
                    sectionKey = keyPrefix + sectionKey;
            }

            config.keyPrefix = string.Empty;
            config.htKeys.Clear();
            config.htDefines.Clear();

            if (string.IsNullOrWhiteSpace(sectionKey))
            {
                // There is no section key so simply copy all values.

                foreach (var entry in this.htKeys)
                    config.htKeys.Add(entry.Key, entry.Value);

                return config;
            }

            sectionKey = sectionKey.ToLowerInvariant().Trim();

            foreach (var entry in this.htKeys)
                if (entry.Key.ToLowerInvariant().StartsWith(sectionKey))
                    config.htKeys.Add(entry.Key.Substring(sectionKey.Length), entry.Value);

            return config;
        }

        /// <summary>
        /// Returns the <b>string</b> value for the key passed if it exists in the configuration
        /// or <c>null</c> if it cannot be found.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value or <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is <c>null</c>.</exception>
        public string Get(string key)
        {
            string value = null;

            if (key == null)
                throw new ArgumentNullException("key");

            if (Config.ProcessEnvironmentVars)
                return GetEnv(key);
#if WINFULL
            if (key.ToLowerInvariant().StartsWith("azure."))
                value = GetAzureSetting(key);
            else if (htKeys == null)
                value = ConfigurationManager.AppSettings[GetKey(key)];
            else
                htKeys.TryGetValue(GetKey(key), out value);
#else
            htKeys.TryGetValue(GetKey(key),out value);
#endif
            if (value == null)
                return value;

            return Expand(value, 0);
        }

        /// <summary>
        /// This method searches the configuration for the key passed.  If it doesn't
        /// exist then the method returns null.  If it does exist, the method first
        /// replaces any substrings of the form $(name) or %name% with the value of the 
        /// corresponding environment variable.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The processed key value or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// The only environment string supported is %entry-path% which will be replaced
        /// with the fully qualified path to the directory holding the application's
        /// entry point assembly.
        /// </para>
        /// <note>
        /// Environment variable names are case sensitive.
        /// </note>
        /// </remarks>
        public string GetEnv(string key)
        {
            string value = null;

#if WINFULL
            if (htKeys != null)
                htKeys.TryGetValue(GetKey(key), out value);

            if (value == null)
            {
                if (key.ToLowerInvariant().StartsWith("azure."))
                    value = GetAzureSetting(key);

                if (value == null)
                    value = ConfigurationManager.AppSettings[GetKey(key)];
            }
#else
            htKeys.TryGetValue(GetKey(key),out value);
#endif
            if (value == null)
                return null;

            // $todo: This method is doing more than just expanding environment variables,
            //        it's also handling the expansion of local config setting references.
            //        This should be probably refactored.

            // Handle the expansion of any embedded config setting references.

            value = ExpandSettingReferences(value, 0);

            // Handle the expansion of any environment variables next.

            value = EnvironmentVars.Expand(Expand(value, 0));

            return value;
        }

        /// <summary>
        /// Handles the expansion of any configuration setting references embeded in the
        /// input string.  Note that these reference can use the <b>$(name)</b> or the
        /// archaic <b>#name#</b> syntax.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="nesting">The nesting level.</param>
        /// <returns>The expanded string.</returns>
        private string ExpandSettingReferences(string input, int nesting)
        {
            if (nesting >= 16)
                throw new StackOverflowException("Too many nested environment variable expansions.");

            StringBuilder   sb;
            int             p, pStart, pEnd;
            string          name;
            string          value;

            // Return right away if there's no macro characters in the string.

            if (input.IndexOfAny(macroChars) == -1)
                return input;

            // Process variables of the form %name%

            sb = new StringBuilder(input.Length + 64);
            p  = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent (%) characters.

                pStart = input.IndexOf('%', p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf('%', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 1, pEnd - pStart - 1);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                htKeys.TryGetValue(name, out value);
                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                    sb.Append(Expand(value, nesting + 1));

                // Advance past this definition

                p = pEnd + 1;
            }

            input = sb.ToString();

            // Process variables of the form $(name)

            sb = new StringBuilder(input.Length + 64);
            p = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent $(name) characters.

                pStart = input.IndexOf("$(", p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf(')', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 2, pEnd - pStart - 2);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                htKeys.TryGetValue(name, out value);
                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                {
                    sb.Append(Expand(value, nesting + 1));
                }

                // Advance past this definition

                p = pEnd + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the <b>string</b> value for the key passed if it exists in the configuration
        /// or def if it cannot be found.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        public string Get(string key, string def)
        {
            string value;

            value = Get(key);
            if (value == null)
                return def;
            else
                return Expand(value, 0);
        }

        /// <summary>
        /// Returns the <b>boolean</b> value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Boolean values may be encoded using the following literals: 0/1, on/off,
        /// yes/no, true/false, enable/disable.
        /// </remarks>
        public bool Get(string key, bool def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <b>integer</b> value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
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
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public int Get(string key, int def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <b>long</b> value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
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
        public long Get(string key, long def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <b>double</b> value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
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
        public double Get(string key, double def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Parses a key value for an assembly path and type name encoded as 
        /// [assembly path]:[type] and attempts to load the assembly and return 
        /// the type instance specified.
        /// </summary>
        /// <param name="key">The key for the encoded assembly path and type name.</param>
        /// <param name="def">The default value to be returned if the operation fails.</param>
        /// <returns>The System.Type instance referencing the requested type.</returns>
        /// <remarks>
        /// This is a handy way to specify and then load plugin modules.
        /// </remarks>
        public System.Type Get(string key, System.Type def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns an <b>enumeration</b> value where the value is case insenstive.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public object Get(string key, System.Type enumType, object def)
        {
            return Parse(Get(key), enumType, def);
        }

        /// <summary>
        /// Returns an <b>enumeration</b> value where the value is case insenstive.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public TEnum Get<TEnum>(string key, object def)
        {
            return Parse<TEnum>(Get(key), def);
        }

        /// <summary>
        /// Returns an arbitrary structured type value that implements <see cref="IParseable" />.
        /// </summary>
        /// <typeparam name="TValue">The resulting type.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value to be returned if the configuration setting is missing or invalid.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public TValue GetCustom<TValue>(string key, TValue def)
            where TValue : IParseable, new()
        {
            return ParseCustom<TValue>(Get(key), def);
        }

        /// <summary>
        /// Returns the array of configuration subsection key prefixes named by the key passed.
        /// </summary>
        /// <param name="key">The subsection array name.</param>
        /// <returns>The set of fully qualified subsection key names.</returns>
        /// <remarks>
        /// <para>
        /// This is useful for loading a variable number of nested structured configuration
        /// settings.  For example:
        /// </para>
        /// <code language="none">
        ///     #section Settings
        /// 
        ///         #section Item[0]
        /// 
        ///             MySetting1 = test1
        ///             MySetting2 = test2
        /// 
        ///         #endsection
        /// 
        ///         #section Item[1]
        /// 
        ///             MySetting1 = test3
        ///             MySetting2 = test4
        /// 
        ///         #endsection
        /// 
        ///     #endsection
        /// 
        ///     // Application code:
        /// 
        ///     Config      config = new Config("Settings");
        ///     string[]    items  = config.GetSectionArray("Item");
        /// 
        ///     foreach (string key in items)
        ///         Console.WriteLine(key);
        /// 
        ///     // Output:
        /// 
        ///     Settings.Item[0]
        ///     Settings.Item[1]
        /// 
        /// </code>
        /// </remarks>
        public string[] GetSectionKeyArray(string key)
        {
            List<string>    sections = new List<string>();
            string          subKey;
            string          prefix;
            bool            found;

            for (int i = 0; ; i++)
            {
                subKey = string.Format("{0}{1}[{2}]", keyPrefix, key, i);
                prefix = subKey.ToLowerInvariant() + ".";

                // $todo(jeff.lill): 
                //
                // This linear search is going to be slow when
                // there's a large number of settings.  This
                // shouldn't be a problem for most applications
                // but I may want to revisit this at some point.

                found = false;
                foreach (string setting in htKeys.Keys)
                    if (setting.ToLowerInvariant().StartsWith(prefix))
                    {
                        found = true;
                        break;
                    }

                if (!found)
                    break;

                sections.Add(subKey);
            }

            return sections.ToArray();
        }

        /// <summary>
        /// Returns the array of configuration subsections named by the key passed.
        /// </summary>
        /// <param name="key">The subsection array name.</param>
        /// <returns>The subsections loaded as <see cref="Config" /> instances.</returns>
        /// <remarks>
        /// This works much like <see cref="GetSectionKeyArray" /> except that
        /// this call loads configurations for each subsection key prefix.
        /// </remarks>
        public Config[] GetSectionConfigArray(string key)
        {
            string[] subKeys;
            Config[] array;

            subKeys = GetSectionKeyArray(key);
            array = new Config[subKeys.Length];

            for (int i = 0; i < subKeys.Length; i++)
                array[i] = new Config(subKeys[i]);

            return array;
        }

        /// <summary>
        /// Returns the array of <b>string</b> values for the key passed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <remarks>
        /// This works by looking for the values for keys that
        /// have the form: key[#] where # is the zero-based index
        /// of the array.  The method starts at index 0 and looks for
        /// keys the match the generated value until we don't find
        /// a match.  The method returns the resulting set of values.
        /// </remarks>
        public string[] GetArray(string key)
        {
            int         c;
            string[]    values;

            c = 0;
            while (true)
            {
                if (Get(string.Format("{0}[{1}]", key, c)) == null)
                    break;

                c++;
            }

            values = new string[c];
            for (int i = 0; i < c; i++)
                values[i] = Get(string.Format("{0}[{1}]", key, i));

            return values;
        }

        /// <summary>
        /// Returns an array of <b>string</b> values for the key passed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default result.</param>
        /// <remarks>
        /// This works by looking for the values for keys that
        /// have the form: key[#] where # is the zero-based index
        /// of the array.  The method starts at index 0 and looks for
        /// keys the match the generated value until we don't find
        /// a match.  The method returns the resulting set of values.
        /// </remarks>
        public string[] GetArray(string key, string[] def)
        {
            int         c;
            string[]    values;

            c = 0;
            while (true)
            {
                if (Get(string.Format("{0}[{1}]", key, c)) == null)
                    break;

                c++;
            }

            if (c == 0)
                return def;

            values = new string[c];
            for (int i = 0; i < c; i++)
                values[i] = Get(string.Format("{0}[{1}]", key, i));

            return values;
        }

        /// <summary>
        /// Returns a string dictionary that holds the subkey/value pairs
        /// for a named indexed multi-valued configuration key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The dictionary holding the subkey/value pairs.</returns>
        /// <remarks>
        /// <para>
        /// This works by adding all configuration settings of the form key[subkey]=value
        /// to the dictionary returned.  Note that only one value for any particular subkey 
        /// will be added to the dictionary.  Addition values will be ignored.
        /// </para>
        /// <note>
        /// All subkeys will be converted to lower case before
        /// being added to the dictionary returned.
        /// </note>
        /// </remarks>
        public Dictionary<string, string> GetDictionary(string key)
        {
            Dictionary<string, string>  ht = new Dictionary<string, string>();
            int                         p;
            string                      fullKey;
            string                      subkey;
            string                      value;

            fullKey = GetKey(key);
            foreach (string name in htKeys.Keys)
            {
                if (name.Length == 0 || name[name.Length - 1] != ']')
                    continue;

                p = name.IndexOf('[');
                if (p == -1 || String.Compare(fullKey, name.Substring(0, p), StringComparison.OrdinalIgnoreCase) != 0)
                    continue;

                subkey = name.Substring(p + 1, name.Length - p - 2);
                value = Get(key + "[" + subkey + "]");
                Assertion.Test(value != null);

                if (ht.ContainsKey(subkey))
                    continue;

                ht.Add(subkey, value);
            }

            return ht;
        }

        /// <summary>
        /// Returns the <b>TimeSpan</b> value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
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
        public TimeSpan Get(string key, TimeSpan def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <b>IPAddress</b> parsed from the key passed or
        /// null if the key doesn't exist or if there was a problem
        /// parsing the value. 
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Endpoints are formatted as dotted quads.
        /// </remarks>
        public IPAddress Get(string key, IPAddress def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <see cref="NetworkBinding" /> parsed from the key passed or
        /// null if the key doesn't exist or if there was a problem
        /// parsing the value. 
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </remarks>
        public NetworkBinding Get(string key, NetworkBinding def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <see cref="Guid" /> parsed from the key passed or
        /// null if the key doesn't exist or if there was a problem
        /// parsing the value. 
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// <para>
        /// Guids are formatted as:
        /// </para>
        /// <blockquote>{8676BA44-4CAB-4de6-A48F-EBCD1CE321CA}</blockquote>
        /// </remarks>
        public Guid Get(string key, Guid def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns the <see cref="Uri" /> parsed from the key passed or
        /// null if the key doesn't exist or if there was a problem
        /// parsing the value. 
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        public Uri Get(string key, Uri def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Returns an array of <see cref="NetworkBinding" /> values for the key passed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <remarks>
        /// This works by looking for the values for keys that
        /// have the form: key[#] where # is the zero-based index
        /// of the array.  The method starts at index 0 and looks for
        /// keys the match the generated value until we don't find
        /// a match.  The method returns the resulting set of values.
        /// </remarks>
        public NetworkBinding[] GetNetworkBindingArray(string key)
        {
            int                 c;
            NetworkBinding[]    values;

            c = 0;
            while (true)
            {
                if (Get(string.Format("{0}[{1}]", key, c), (NetworkBinding)null) == null)
                    break;

                c++;
            }

            values = new NetworkBinding[c];
            for (int i = 0; i < c; i++)
                values[i] = Get(string.Format("{0}[{1}]", key, i), (NetworkBinding)null);

            return values;
        }

        /// <summary>
        /// Parses the value passed as a <b>hex encoded byte array</b>, returning the default value if
        /// the value is <c>null</c> or if there's an error.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <param name="def">The default value.</param>
        public byte[] Parse(string value, byte[] def)
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
        /// Returns the array of bytes for the key whose value is
        /// a hex encoded byte array.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        public byte[] Get(string key, byte[] def)
        {
            return Parse(Get(key), def);
        }

        /// <summary>
        /// Renders the configuration settings to a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            var sortedKeys =
                from k in htKeys.Keys
                orderby k.ToLower()
                select k;

            foreach (var key in sortedKeys)
                sb.AppendFormatLine("{0} = {1}", key, htKeys[key]);

            return sb.ToString();
        }

        //---------------------------------------------------------------------
        // IEnumerable implementation

        /// <summary>
        /// Returns an enumerator over the instances key/value pairs.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return htKeys.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator over the instances key/value pairs.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return htKeys.GetEnumerator();
        }
    }
}
