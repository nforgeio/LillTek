//-----------------------------------------------------------------------------
// FILE:        SpeechEngineSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the speech engine settings.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Describes the speech engine settings.
    /// </summary>
    public class SpeechEngineSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the text-to-speech engine from the application's configuration
        /// using the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key prefix.</param>
        /// <returns>The settings.</returns>
        /// <remarks>
        /// <para>
        /// The text-to-speech engine settings are loaded from the application
        /// configuration, using the specified key prefix.  The following
        /// settings are recognized by the class:
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>PhraseCacheFolder</td>
        ///     <td><b>$(ProgramDataPath)\LillTek\NeonSwitch\PhraseCache</b></td>
        ///     <td>
        ///     The fully qualified path to the file system folder where the cache will be located.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>OneTimePhraseFolder</td>
        ///     <td><b>$(ProgramDataPath)\LillTek\NeonSwitch\PhraseCache\OneTime</b></td>
        ///     <td>
        ///     The fully qualified path to the file system folder where one-time
        ///     phrases will be located while they are played by NeonSwitch.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>PhraseFolderFanout</td>
        ///     <td><b>100</b></td>
        ///     <td>
        ///     Specifies the number of subfolders to create in the cache.  Cached audio
        ///     files will be distributed randomly across these folders to avoid having a huge
        ///     number of files in any one folder.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>PhrasePurgeInterval</td>
        ///     <td><b>1m</b></td>
        ///     <td>
        ///     The time interval at which temporary phrases as well as phrases that
        ///     have not been referenced for some period of time will be purged.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxPhraseTTL</td>
        ///     <td><b>1d</b></td>
        ///     <td>
        ///     The maximum time a phrase will be cached if it is not used.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxOneTimePhraseTTL</td>
        ///     <td><b>5m</b></td>
        ///     <td>
        ///     The maximum time a one-time phrase audio file will be retained.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DefaultVoice</td>
        ///     <td><b>auto</b></td>
        ///     <td>
        ///     <para>
        ///     The default speech synthesis voice.  This defaults to <b>auto</b>
        ///     which has the switch choose the voice depending on which voices are
        ///     currently installed.  
        ///     </para>
        ///     <para>
        ///     NeonSwitch will favor Cepstral 8KHz voices if present, especially
        ///     <b>Cepstral Allison-8kHz</b> but fall back to <b>Microsoft Anna</b>
        ///     which should be present on all Windows machines.
        ///     </para>
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static SpeechEngineSettings LoadConfig(string keyPrefix)
        {
            var config   = new Config(keyPrefix);
            var settings = new SpeechEngineSettings();

            settings.PhraseCacheFolder   = config.Get("PhraseCacheFolder", settings.PhraseCacheFolder);
            settings.OneTimePhraseFolder = config.Get("OneTimePhraseFolder", settings.OneTimePhraseFolder);
            settings.PhraseFolderFanout  = config.Get("PhraseFolderFanout", settings.PhraseFolderFanout);
            settings.PhrasePurgeInterval = config.Get("PhrasePurgeInterval", settings.PhrasePurgeInterval);
            settings.MaxPhraseTTL        = config.Get("MaxPhraseTTL", settings.MaxPhraseTTL);
            settings.MaxOneTimePhraseTTL = config.Get("MaxOneTimePhraseTTL", settings.MaxOneTimePhraseTTL);
            settings.DefaultVoice        = config.Get("DefaultVoice", settings.DefaultVoice);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance methods

        /// <summary>
        /// The fully qualified path to the file system folder where the cache will be located.
        /// Defaults to <b>$(ProgramDataPath)\LillTek\NeonSwitch\PhraseCache</b>.
        /// </summary>
        public string PhraseCacheFolder { get; set; }

        /// <summary>
        /// The fully qualified path to the file system folder where one-time
        /// phrases will be located while they are played by NeonSwitch.
        /// Defaults to <b>$(ProgramDataPath)\LillTek\NeonSwitch\PhraseCache\OneTime</b>.
        /// </summary>
        public string OneTimePhraseFolder { get; set; }

        /// <summary>
        /// Specifies the number of subfolders to create in the cache.  Cached audio
        /// files will be distributed randomly across these folders to avoid having a huge
        /// number of files in any one folder.  Defaults to <b>100</b>.
        /// </summary>
        public int PhraseFolderFanout { get; set; }

        /// <summary>
        /// The time interval at which temporary phrases as well as phrases that
        /// have not been referenced for some period of time will be purged.
        /// Defaults to <b>1 minute</b>.
        /// </summary>
        public TimeSpan PhrasePurgeInterval { get; set; }

        /// <summary>
        /// The maximum time a phrase will be cached if it is not used.
        /// Defaults to <b>1 day</b>.
        /// </summary>
        public TimeSpan MaxPhraseTTL { get; set; }

        /// <summary>
        /// The maximum time a one-time phrase audio file will be retained.
        /// Defaults to <b>5 minutes</b>.
        /// </summary>
        public TimeSpan MaxOneTimePhraseTTL { get; set; }

        /// <summary>
        /// Specifies the default voice to use for speech synthesis or <b>auto</b>
        /// to have NeonSwitch choose the voice.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default speech synthesis voice.  This defaults to <b>auto</b>
        /// which has the switch choose the voice depending on which voices are
        /// currently installed.  
        /// </para>
        /// <para>
        /// NeonSwitch will favor Cepstral 8KHz voices if present, especially
        /// <b>Cepstral Allison-8kHz</b> but fall back to <b>Microsoft Anna</b>
        /// which should be present on all Windows machines.
        /// </para>
        /// </remarks>
        public string DefaultVoice { get; set; }

        /// <summary>
        /// Returns a text-to-speech engine settings instance with reasonable defaults.
        /// </summary>
        public SpeechEngineSettings()
        {
            this.PhraseCacheFolder   = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"LillTek\NeonSwitch\PhraseCache");
            this.OneTimePhraseFolder = Path.Combine(PhraseCacheFolder, "OneTime");
            this.PhraseFolderFanout  = 100;
            this.PhrasePurgeInterval = TimeSpan.FromMinutes(1);
            this.MaxPhraseTTL        = TimeSpan.FromDays(1);
            this.MaxOneTimePhraseTTL = TimeSpan.FromMinutes(5);
            this.DefaultVoice        = "auto";
        }
    }
}
