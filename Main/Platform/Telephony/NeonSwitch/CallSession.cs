//-----------------------------------------------------------------------------
// FILE:        CallSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides call state and commands for session based applications
//              invoked via the SwitchApp.NewCallSessionEvent.

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

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Provides call state and commands for session based applications
    /// invoked via the <see cref="Switch" />.<see cref="Switch.CallSessionEvent" />.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class wraps the low-level <see cref="ManagedSession" /> class providing a more
    /// .NET friendly set of members.
    /// </note>
    /// <note>
    /// This class is <b>not threadsafe</b>.  It is designed to be used in the single
    /// threaded application model for within a session based NeonSwitch application
    /// invoked via the <see cref="Switch" />.<see cref="Switch.CallSessionEvent" />.
    /// </note>
    /// <para>
    /// NeonSwitch session applications are executed from dialplans or from event
    /// driven applications.  These applications are typically simple and don't usually
    /// run for an extended period of time.  Session applications are best suited 
    /// to performing call setup or termination actions that are bit too complex for
    /// a dialplan or simple IVR application.
    /// </para>
    /// <para>
    /// Call sessions are invoked via <see cref="Switch" />.<see cref="Switch.CallSessionEvent" />
    /// on a per call basis.  The NeonSwitch application will need to enlist a handler
    /// for this event while executing it <see cref="SwitchApp" />.<see cref="SwitchApp.Main" />
    /// method.  <see cref="Switch.CallSessionEvent" /> will be raised whenever
    /// the application is invoked on a call (typically from a dialplan).  The 
    /// <see cref="CallSessionArgs" /> passed to mthe handler will include
    /// a <see cref="CallSession "/> instance that will be used to control the
    /// call and a string with the being passed to the application.
    /// </para>
    /// <para>
    /// Call session application event handlers are typically characterized by an
    /// event look implementing a simple state machine that prompts the caller,
    /// gathers responses, and then performs call operations.  The handler should
    /// check the <see cref="CallSession.IsActive" /> frequently during the'
    /// course of handling the call or alternatively, enlist in the 
    /// <see cref="CallSession.HangupEvent" />.  <see cref="CallSession.HangupEvent" />
    /// will be raised when the call has been cleared (hungup) and <see cref="CallSession.IsActive" />
    /// will return <c>false</c> at this point as well.  The application's event handler
    /// should stop processing and return as soon as the call has been cleared to free
    /// up switch resource.
    /// </para>
    /// <para>
    /// Applications may also enlist in the <see cref="CallSession.EventReceived" /> and
    /// <see cref="CallSession.DtmfReceived" /> events.  <see cref="CallSession.DtmfReceived" />
    /// will be raised when a DTMF digit is detected on the call and <see cref="CallSession.EventReceived" />
    /// when a switch event is received for the call.
    /// </para>
    /// <para>
    /// Some of the important session properties include <see cref="CallSession.CallState" />,
    /// <see cref="CallSession.IsAnswered" />, <see cref="CallSession.IsActive" />,
    /// <see cref="CallSession.IsBridged" />, <see cref="CallSession.HangupReason" />,
    /// <see cref="CallSession.CallID" />, <see cref="CallSession.MediaReady" />, 
    /// <see cref="CallSession.CallDetails" /> and <see cref="CallSession.AutoHangup" />.  
    /// See the documentation for the individual properties for more information.
    /// </para>
    /// <para>
    /// The <see cref="CallSession.AutoHangup" /> property deserves special mention.  This
    /// is a boolean value that controls what happens to the call after the call session handler
    /// handler exits.  This defaults to <c>true</c> which instructs the switch to hang the call
    /// up after the application is finished with the call.  Many applications may need the call
    /// to continue processing until one of the endpoints hangs up.  This applications will need
    /// to set <see cref="CallSession.AutoHangup" /> to <c>false</c> at some point before
    /// they return the event handler.
    /// </para>
    /// <para>
    /// There are two many session methods to describe completely here so we'll describe only
    /// the most common methods.  See the method documentation for more information.
    /// </para>
    /// <para>
    /// <see cref="CallSession.PreAnswer" /> and <see cref="CallSession.Answer" /> and <see cref="CallSession.Hangup"/>
    /// to clear the call.  Most of the <see cref="SwitchAction" /> may be executed by an
    /// application by passing an action instance to <see cref="Execute(SwitchAction)" />.
    /// Low-level FreeSWITCH application actions can be invoked via <see cref="Execute(string)" />,
    /// <see cref="Execute(string,string)" /> and <see cref="Execute(string,string,object[])" />.
    /// </para>
    /// <para>
    /// Applications that need to interact with the caller will use the <see cref="CallSession.Speak(string)" />,
    /// <see cref="CallSession.PlayAudio(AudioSource)" />, <see cref="CallSession.CollectDigits(TimeSpan)" />, and
    /// <see cref="CallSession.PromptForDigits" /> methods.  Calls can also be recorded using
    /// <see cref="CallSession.RecordBegin" />, <see cref="CallSession.RecordPause" /> and
    /// <see cref="CallSession.RecordEnd" />.
    /// </para>
    /// <para>
    /// The class also includes the an index that can be used to get and set call state variables as
    /// well as to retrieve the values of global switch variables.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class CallSession
    {
        private ManagedSession  session;
        private string          collectedDigits;
        private bool            autoHangup;
        private string          recordPath;
        private bool            recordPaused;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The low-level FreeSWITCH session.</param>
        internal CallSession(ManagedSession session)
        {
            this.session         = session;
            this.collectedDigits = string.Empty;
            this.AutoHangup      = true;

            // Setup the low-level event handlers.

            session.DtmfReceivedFunction =
                (digit, duration) =>
                {
                    if (DtmfReceived == null)
                        return string.Empty;

                    var args = new DtmfInputEventArgs(digit, duration);

                    DtmfReceived(this, args);

                    if (args.Break)
                        return "break";
                    else
                        return string.Empty;
                };

            session.EventReceivedFunction =
                (evt) =>
                {
                    if (EventReceived != null)
                        EventReceived(this, new SwitchEventArgs(new SwitchEvent(evt.InternalEvent)));

                    return string.Empty;
                };

            session.HangupFunction =
                () =>
                {
                    if (HangupEvent != null)
                        HangupEvent(this, new HangupEventArgs(this.HangupReason, this.CallDetails));
                };
        }

        /// <summary>
        /// Raised when DTMF digits are received on the call.
        /// </summary>
        public EventHandler<DtmfInputEventArgs> DtmfReceived;

        /// <summary>
        /// Raised when the nswitch receives an event for this call.
        /// </summary>
        public EventHandler<SwitchEventArgs> EventReceived;

        /// <summary>
        /// Raised when the call is hungup. 
        /// </summary>
        public EventHandler<HangupEventArgs> HangupEvent;

        /// <summary>
        /// Returns the current call state.
        /// </summary>
        public ChannelState CallState
        {
            get { return (ChannelState)session.HookState; }
        }

        /// <summary>
        /// Indicates whether call is ready to transfer or is already transferring media.
        /// </summary>
        public bool MediaReady
        {
            get { return session.mediaReady(); }
        }

        /// <summary>
        /// Identifies the voice to be used when speaking text or phrases where no voice
        /// is specified or <c>null</c> to use the switch's default voice.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The names of the voices installed on the switch can be discovered by examining
        /// the <see cref="Switch"/>.<see cref="Switch.InstalledVoices" /> collection.
        /// </para>
        /// <note>
        /// NeonSwitch will use the default switch voice if the requested voice is not
        /// installed or enabled.
        /// </note>
        /// </remarks>
        public string Voice { get; set; }

        /// <summary>
        /// Returns the call details record (CDR) as XML.
        /// </summary>
        public string CallDetails
        {
            get { return session.getXMLCDR(); }
        }

        /// <summary>
        /// Indicates whether the switch will automatically hangup the call
        /// (if it's not already hungup) after the call session application
        /// exits.  This defaults to <c>true</c> when the session is first
        /// created.
        /// </summary>
        public bool AutoHangup
        {
            get { return autoHangup; }

            set
            {
                autoHangup = value;
                session.SetAutoHangup(value);
            }
        }

        /// <summary>
        /// Indicates whether the call has been answered.
        /// </summary>
        public bool IsAnswered
        {
            get { return session.answered(); }
        }

        /// <summary>
        /// Indicates whether the call is still active and ready for processing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property will return between the time the call starts and it is hungup
        /// or transferred.  Call session applications must check this property frequently
        /// within their event loop and exit immediately once this returns <c>false</c>.
        /// </para>
        /// <note>
        /// After a call becomes inactive, you may use the <see cref="HangupReason" />
        /// property to get the reason why the call was cleared or <see cref="CallDetails" />
        /// to obtain the call detail record.
        /// </note>
        /// </remarks>
        public bool IsActive
        {
            get { return session.Ready(); }
        }

        /// <summary>
        /// Indicates whether the call has been bridged to another channel.
        /// </summary>
        public bool IsBridged
        {
            get { return session.bridged(); }
        }

        /// <summary>
        /// Returns the reason the call was hung up.
        /// </summary>
        public SwitchHangupReason HangupReason
        {
            get { return (SwitchHangupReason)session.cause; }
        }

        /// <summary>
        /// Returns the globally unique call ID.
        /// </summary>
        public Guid CallID
        {
            get { return Guid.Parse(session.uuid); }
        }

        /// <summary>
        /// Establishes early media transfer with the caller but does not 
        /// actually answer the call.
        /// </summary>
        public void PreAnswer()
        {
            session.preAnswer();
        }

        /// <summary>
        /// Answers the call.
        /// </summary>
        public void Answer()
        {
            session.Answer();
        }

        /// <summary>
        /// Hangs up the call.
        /// </summary>
        public void Hangup()
        {
            ActionRenderingContext.Execute(new HangupAction(CallID));
        }

        /// <summary>
        /// Flushes any unprocessed DTMF digits accumulated by the session.
        /// </summary>
        public void FlushDigits()
        {
            session.flushDigits();
        }

        /// <summary>
        /// Flushes any unprocessed events accumulated by the session.
        /// </summary>
        public void FlushEvents()
        {
            session.flushEvents();
        }

        /// <summary>
        /// Collects DTMF digits for the duration specified.
        /// </summary>
        /// <param name="timeout">The time to wait for the digits to be collected.</param>
        /// <remarks>
        /// <para>
        /// You'll need to subscribe to the <see cref="DtmfReceived" /> event
        /// to actually receive these DTMF digits and the duration that they were pressed.
        /// </para>
        /// <note>
        /// This method blocks the current thread for the duration of the timeout specified
        /// or until the <see cref="DtmfReceived"/> event handler sets <see cref="DtmfInputEventArgs"/>.<see cref="DtmfInputEventArgs.Break"/>
        /// to <c>true</c>.
        /// </note>
        /// </remarks>
        public void CollectDigits(TimeSpan timeout)
        {
            CollectDigits(timeout, timeout);
        }

        /// <summary>
        /// Collects DTMF digits for the duration specified or until the timeout to receive
        /// an individual digit has been exceeded.
        /// </summary>
        /// <param name="timeout">The time to wait for the digits to be collected.</param>
        /// <param name="digitTimeout">The maximum time to wait for any single digit.</param>
        /// <remarks>
        /// <para>
        /// You'll need to subscribe to the <see cref="DtmfReceived" /> event
        /// to actually receive these DTMF digits and the duration that they were pressed.
        /// </para>
        /// <note>
        /// This method blocks the current thread for the duration of the timeout constraints
        /// or until the <see cref="DtmfReceived"/> event handler sets 
        /// <see cref="DtmfInputEventArgs"/>.<see cref="DtmfInputEventArgs.Break"/> to <c>true</c>.
        /// </note>
        /// </remarks>
        public void CollectDigits(TimeSpan timeout, TimeSpan digitTimeout)
        {
            int timeoutSeconds      = SwitchHelper.GetScheduleSeconds(timeout);
            int digitTimeoutSeconds = SwitchHelper.GetScheduleSeconds(digitTimeout);
            var sb                  = new StringBuilder();

            if (timeoutSeconds <= 0 || digitTimeoutSeconds <= 0)
                return;

            session.CollectDigits(timeoutSeconds, digitTimeoutSeconds);
        }

        /// <summary>
        /// Collects DTMF digits pressed by the caller until the maximum number of digis is
        /// received, one of a set of terminator digits is pressed, or the overall operation
        /// timeout has been exceeded.
        /// </summary>
        /// <param name="maxDigits">The maximum number of digits to be collected.</param>
        /// <param name="terminators">Zero or more terminating DTMF digits or <c>null</c>.</param>
        /// <param name="timeout">The overall operation timeout.</param>
        /// <returns>The collected digits.</returns>
        public string GetDigits(int maxDigits, string terminators, TimeSpan timeout)
        {
            int timeoutSeconds = SwitchHelper.GetScheduleSeconds(timeout);

            if (maxDigits <= 0 || timeoutSeconds <= 0)
                return string.Empty;    // Nothing to do

            if (terminators == null)
                terminators = string.Empty;

            return session.GetDigits(maxDigits, terminators, timeoutSeconds);
        }

        /// <summary>
        /// Collects DTMF digits pressed by the caller until the maximum number of digis is
        /// received, one of a set of terminator digits is pressed, an individual digit press
        /// has timed out or the overall operation timeout has been exceeded.
        /// </summary>
        /// <param name="maxDigits">The maximum number of digits to be collected.</param>
        /// <param name="terminators">Zero or more terminating DTMF digits or <c>null</c>.</param>
        /// <param name="timeout">The overall operation timeout.</param>
        /// <param name="digitTimeout">The timeoout for individual key presses.</param>
        /// <returns>The collected digits.</returns>
        public string GetDigits(int maxDigits, string terminators, TimeSpan timeout, TimeSpan digitTimeout)
        {
            int timeoutSeconds      = SwitchHelper.GetScheduleSeconds(timeout);
            int digitTimeOutSeconds = SwitchHelper.GetScheduleSeconds(digitTimeout);

            if (maxDigits <= 0 || timeoutSeconds <= 0 || digitTimeOutSeconds <= 0)
                return string.Empty;    // Nothing to do

            if (terminators == null)
                terminators = string.Empty;

            return session.GetDigits(maxDigits, terminators, timeoutSeconds, digitTimeOutSeconds);
        }

        /// <summary>
        /// Speaks the <see cref="Phrase" /> passed using the current voice if the phrase doesn't 
        /// specify a voice.
        /// </summary>
        /// <param name="phrase">The phrase to be spoken.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="phrase" /> is <c>null</c>.</exception>
        public void Speak(Phrase phrase)
        {
            if (phrase == null)
                throw new ArgumentNullException("phrase");

            if (phrase.Voice == null && this.Voice != null)
                phrase = phrase.Clone(Voice);

            ActionRenderingContext.Execute(new PlayAudioAction(CallID, AudioSource.Speech(phrase)));
        }

        /// <summary>
        /// Speaks the text passed using the current voice.
        /// </summary>
        /// <param name="text">The text to be spoken.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text" /> is <c>null</c>.</exception>
        public void Speak(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            ActionRenderingContext.Execute(new PlayAudioAction(CallID, AudioSource.Speech(Phrase.PhoneVoiceText(Voice, text))));
        }

        /// <summary>
        /// Speaks the formatted text passed using the current text-to-speech engine and voice.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="format" /> or <paramref name="args" /> is <c>null</c>.</exception>
        public void Speak(string format, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            ActionRenderingContext.Execute(new PlayAudioAction(CallID, AudioSource.Speech(Phrase.PhoneVoiceText(Voice, format, args))));
        }

        /// <summary>
        /// Plays an audio source until it is finished or until <see cref="Break" /> or
        /// <see cref="BreakAll" /> is called.
        /// </summary>
        /// <param name="source">The audio source.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source" /> is <c>null</c>.</exception>
        public void PlayAudio(AudioSource source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            ActionRenderingContext.Execute(new PlayAudioAction(CallID, source));
        }

        /// <summary>
        /// Plays an audio source until it is finished or until one of a set of DTMF
        /// digits is pressed or until <see cref="Break" /> or <see cref="BreakAll" /> 
        /// is called.
        /// </summary>
        /// <param name="source">The audio source.</param>
        /// <param name="stopDtmf">The set of DTMF digits that will stop the operation (<see cref="Dtmf" />).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="stopDtmf"/> includes an invalid DTMF digit.</exception>
        public void PlayAudio(AudioSource source, string stopDtmf)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            if (stopDtmf != null)
                Dtmf.Validate(stopDtmf);

            ActionRenderingContext.Execute(new PlayAudioAction(CallID, source) { StopDtmf = stopDtmf });
        }

        /// <summary>
        /// Terminates execution of the current command, typically used to stop 
        /// playing audio on a call.
        /// </summary>
        public void Break()
        {
            ActionRenderingContext.Execute(new BreakAction(CallID));
        }

        /// <summary>
        /// Terminates execution of the current and any pending commands.
        /// </summary>
        public void BreakAll()
        {
            ActionRenderingContext.Execute(new BreakAction(CallID, true));
        }

        /// <summary>
        /// Synchronously executes a low-level NeonSwitch/FreeSwitch command.
        /// </summary>
        /// <param name="command">The application or module name.</param>
        /// <returns>The command results.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command" /> is <c>null</c> or empty.</exception>
        /// <remarks>
        /// <note>
        /// The session will not receive DTMF digits or switch events while the
        /// command is executing.
        /// </note>
        /// </remarks>
        public void Execute(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentNullException("command", "[command] cannot be NULL or empty.");

            session.Execute(command, string.Empty);
        }

        /// <summary>
        /// Synchronously executes a low-level NeonSwitch/FreeSwitch command
        /// with arguments.
        /// </summary>
        /// <param name="command">The application or module name.</param>
        /// <param name="args">Ther command arguments.</param>
        /// <returns>The command results.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command" /> is <c>null</c> or empty.</exception>
        /// <remarks>
        /// <note>
        /// The session will not receive DTMF digits or switch events while the
        /// command is executing.
        /// </note>
        /// </remarks>
        public void Execute(string command, string args)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentNullException("command", "[command] cannot be NULL or empty.");

            if (args == null)
                args = string.Empty;

            session.Execute(command, args);
        }

        /// <summary>
        /// Synchronously executes a low-level NeonSwitch/FreeSwitch command
        /// with formatted arguments.
        /// </summary>
        /// <param name="command">The application or module name.</param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">Ther command arguments.</param>
        /// <returns>The command results.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown <paramref name="command" /> is <c>null</c> or empty or any of the
        /// other parameters are <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The session will not receive DTMF digits or switch events while the
        /// command is executing.
        /// </note>
        /// </remarks>
        public void Execute(string command, string format, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentNullException("command", "[command] cannot be NULL or empty.");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            session.Execute(command, string.Format(format, args));
        }

        /// <summary>
        /// Synchronously executes a <see cref="SwitchAction" /> on the current call.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action" /> is <c>null</c>.</exception>
        public void Execute(SwitchAction action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            ActionRenderingContext.Execute(action);
        }

        /// <summary>
        /// Plays audio prompt files and then reads DTMF digits until constraints have been
        /// satisfied and returns them.
        /// </summary>
        /// <param name="promptArgs">The <see cref="DigitPrompt" /> describing the prompts and constraints to use.</param>
        /// <returns>
        /// A <see cref="PromptResponse" /> indicating whether the caller 
        /// entered a valid response and the DTMF digits received.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="promptArgs" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>
        /// The <see cref="DigitPrompt" /> parameter provides a rich way to specify the prompt and
        /// error prompt to be presented to the caller, what qualifies as a valid response, how long the
        /// caller will be given to respond, and how many times to retry the operation.
        /// </para>
        /// <para>
        /// The method returns a <see cref="PromptResponse" /> which indicates whether the caller
        /// entered a valid response and also includes the DTMF digits entered.
        /// </para>
        /// </remarks>
        public PromptResponse PromptForDigits(DigitPrompt promptArgs)
        {
            if (promptArgs == null)
                throw new ArgumentNullException("promptArgs");

            string digits;

            digits = session.PlayAndGetDigits(promptArgs.MinDigits,
                                                promptArgs.MaxDigits,
                                                promptArgs.MaxTries,
                                                (int)promptArgs.Timeout.TotalMilliseconds,
                                                promptArgs.Terminators,
                                                promptArgs.PromptAudio,
                                                promptArgs.RetryAudio,
                                                promptArgs.DigitsRegex,
                                                null,
                                                (int)promptArgs.DigitTimeout.TotalMilliseconds,
                                                null);

            return promptArgs.GetResponse(digits);
        }

        /// <summary>
        /// Starts recording the call to the specified file.
        /// </summary>
        /// <param name="path">
        /// <para>
        /// Path to the recorded file.
        /// </para>
        /// <note>
        /// The file type must be <b>.wav</b>.  Only WAV files are supported by NeonSwitch.
        /// </note>
        /// </param>
        /// <param name="limit">Optionally specifies the maximum length of the recording.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path" /> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if the file type is not <b>.wav</b>.</exception>
        /// <remarks>
        /// <note>
        /// That the <paramref name="path" /> must specify the <b>.wav</b> file type.
        /// NeonSwitch only supports recording to WAV files.
        /// </note>
        /// <para>
        /// Call <see cref="RecordBegin" /> to begin a new recording (automatically stopping any 
        /// recording already in progress).  Use the <see cref="RecordPause" /> property to pause
        /// and restart recording and <see cref="RecordEnd" /> to end recording.
        /// </para>
        /// </remarks>
        public void RecordBegin(string path, TimeSpan? limit)
        {
            // Stop any existing recording.

            if (recordPath != null)
            {
                ActionRenderingContext.Execute(new RecordAction(this.CallID, false, path, null));
                recordPath = null;
            }

            // Validate the parameters.

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path", "Cannot pass [path] as NULL or empty to [RecordBegin].");

            if (String.Compare(Path.GetExtension(path), ".wav", true) != 0)
                throw new ArgumentException(string.Format("[RecordBegin] cannot record to [{0}] since it does have the [.wav] file extension.", path));

            // Start recording.

            recordPath = path;
            recordPaused = false;
            ActionRenderingContext.Execute(new RecordAction(this.CallID, true, path, limit));
        }

        /// <summary>
        /// Pauses or restarts call recording.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when setting this property when no recording is in progress.</exception>
        public bool RecordPause
        {
            get { return recordPath != null && recordPaused; }

            set
            {
                if (recordPath == null)
                    throw new InvalidOperationException("No recording is in progress.");

                if (value == recordPaused)
                    return;     // No change.

                recordPaused = value;
                ActionRenderingContext.Execute(new RecordAction(!recordPaused, recordPath, null));
            }
        }

        /// <summary>
        /// Stops recording a call.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no recording is in progress.</exception>
        public void RecordEnd()
        {
            if (recordPath == null)
                throw new InvalidOperationException("No recording is in progress.");

            ActionRenderingContext.Execute(new RecordAction(false, recordPath, null));

            recordPath = null;
            recordPaused = false;
        }

        /// <summary>
        /// Used to set and access session variables as well as read global switch
        /// variables.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The value if the variable exists, <c>null</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>
        /// You can use this indexer to set or retrieve session variables by passing the variable
        /// name.  The index will return <c>null</c> for variables that don't exist.  You may
        /// also remove a variable by passing <c>null</c> to the indexer.
        /// </para>
        /// <para>
        /// The indexer can also be used to retrieve global switch variables, if the variable
        /// name passed does not match a session variable.  You can also use
        /// <see cref="Switch"/>.<see cref="Switch.GetGlobal"/> to accomplish the same thing.
        /// </para>
        /// </remarks>
        public string this[string name]
        {
            get
            {
                if (name == null)
                    throw new ArgumentException("name");

                return session.GetVariable(name);
            }

            set
            {
                if (name == null)
                    throw new ArgumentException("name");

                session.SetVariable(name, value);
            }
        }
    }
}
