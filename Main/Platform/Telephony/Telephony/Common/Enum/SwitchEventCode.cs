//-----------------------------------------------------------------------------
// FILE:        SwitchEventCode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the possible NeonSwitch events.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Identifies the possible NeonSwitch events.
    /// </summary>
    /// <remarks>
    /// These map directly to the underlying FreeSWITCH events and the documentation
    /// was taken from the <a href="http://wiki.freeswitch.org/wiki/Event_list">FreeSWITCH wiki</a>.
    /// </remarks>
    public enum SwitchEventCode
    {
        /// <summary>
        /// A placeholder for events not hardcoded into NeonSwitch.
        /// </summary>
        Custom = (int)switch_event_types_t.SWITCH_EVENT_CUSTOM,

        /// <summary>
        /// <b>Internal</b>: These events seem to be generated internally by the
        /// <b>mod_event_socket</b> module and are not likely to be ever encountered
        /// by a NeonSwitch application.
        /// </summary>
        Clone = (int)switch_event_types_t.SWITCH_EVENT_CLONE,

        /// <summary>
        /// Raised when an extension is going to do something. It can either be dialing 
        /// someone or it can be an incoming call to an extension.
        /// </summary>
        ChannelCreate = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_CREATE,

        /// <summary>
        /// Raised when a channel is about to be destroyed.
        /// </summary>
        ChannelDestroy = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_DESTROY,

        /// <summary>
        /// Raised when a channel's state has changed.
        /// </summary>
        ChannelState = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_STATE,

        /// <summary>
        /// Raised when a channel's call state has changed.
        /// </summary>
        ChannelCallState = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_CALLSTATE,

        /// <summary>
        /// Raised when a call has been answered on a channel.
        /// </summary>
        ChannelAnswer = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_ANSWER,

        /// <summary>
        /// Raised when one of the listeners on a call has hungup.
        /// </summary>
        ChannelHangup = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_HANGUP,

        /// <summary>
        /// Raised when a channel hangup operation initiated by the NeonSwitch
        /// application has completed.
        /// </summary>
        ChannelHangupComplete = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_HANGUP_COMPLETE,

        /// <summary>
        /// Raised when NeonSwitch needs to perform an action on a channel,
        /// typically looking at the dial plan for something to do.
        /// </summary>
        ChannelExecute = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_EXECUTE,

        /// <summary>
        /// Raised when NeonSwitch has completed executing actions on a channel.
        /// </summary>
        ChannelExecuteComplete = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_EXECUTE_COMPLETE,

        /// <summary>
        /// Raised when the channel is placed on hold.
        /// </summary>
        ChannelHold = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_HOLD,

        /// <summary>
        /// Raised when the channel has been reactivated after being on hold.
        /// </summary>
        ChannelUnhold = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_UNHOLD,

        /// <summary>
        /// Raised when a call on the channel is being bridged between two endpoints.
        /// </summary>
        ChannelBridge = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_BRIDGE,

        /// <summary>
        /// Raised when a call on the channel is being unbridged from two endpoints.
        /// </summary>
        ChannelUnbridge = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_UNBRIDGE,

        /// <summary>
        /// Raised when the channel has begun the process of handling a call.
        /// This is an indication that the switch has notified the calling device
        /// that it is trying to connect the call.
        /// </summary>
        ChannelProgress = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_PROGRESS,

        /// <summary>
        /// Raised when the channel is still in the process of handling a call
        /// and has starting sending the <b>ringing</b> sound to the calling device.
        /// This event will be raised after <see cref="ChannelProgress" />.
        /// </summary>
        ChannelProgressMedia = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_PROGRESS_MEDIA,

        /// <summary>
        /// Raised when an outbound call is being placed on a channel.
        /// </summary>
        ChannelOutgoing = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_OUTGOING,

        /// <summary>
        /// Raised when a channel is parked.
        /// </summary>
        ChannelPark = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_PARK,

        /// <summary>
        /// Raised when a channel is unparked.
        /// </summary>
        ChannelUnpark = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_UNPARK,

        /// <summary>
        /// Raised when an application action is to be performed on a channel.
        /// </summary>
        ChannelAction = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_APPLICATION,

        /// <summary>
        /// Raised when an outbound call or the bridging of two endpoints completes.
        /// </summary>
        ChannelOriginate = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_ORIGINATE,

        /// <summary>
        /// Raised when the unique ID of a channel has changed.  This will happen
        /// when a call is originated or bridged while specifying a new
        /// origination ID.
        /// </summary>
        ChannelUUID = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_UUID,

        /// <summary>
        /// Raised when an API function has been invoked.
        /// </summary>
        Api = (int)switch_event_types_t.SWITCH_EVENT_API,

        /// <summary>
        /// Raised when an entry has been writtent to the FreeSwitch log.
        /// </summary>
        Log = (int)switch_event_types_t.SWITCH_EVENT_LOG,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to be generated by FreeSWITCH.
        /// </summary>
        InboundChannel = (int)switch_event_types_t.SWITCH_EVENT_INBOUND_CHAN,

        /// <summary>
        /// Does not appear to be generated by FreeSWITCH.
        /// </summary>
        OutboundChannel = (int)switch_event_types_t.SWITCH_EVENT_OUTBOUND_CHAN,

        /// <summary>
        /// Raised when NeonSwitch has completed its boot process including the loading of
        /// all configured modules and is ready to begin processing calls.
        /// </summary>
        Startup = (int)switch_event_types_t.SWITCH_EVENT_STARTUP,

        /// <summary>
        /// Raised when NeonSwitch has begin the process of shutting itself down.
        /// </summary>
        Shutdown = (int)switch_event_types_t.SWITCH_EVENT_SHUTDOWN,

        /// <summary>
        /// <b>Tenative:</b> Raised when NeonSwitch starts a SIP user agent that registers
        /// the switch presence with an external provider such as a SIP trunking service.
        /// </summary>
        Publish = (int)switch_event_types_t.SWITCH_EVENT_PUBLISH,

        /// <summary>
        /// <b>Tenative:</b> Raised when NeonSwitch stops the SIP user agent that manages
        /// the registration of the switch presence with an external provider such as a SIP 
        /// trunking service.
        /// </summary>
        Unpublish = (int)switch_event_types_t.SWITCH_EVENT_UNPUBLISH,

        /// <summary>
        /// Raised when the sound level on the channel indicates that someone has started talking
        /// or that other sound is being transmitted.
        /// </summary>
        Talk = (int)switch_event_types_t.SWITCH_EVENT_TALK,

        /// <summary>
        /// Raised when the sound level on the channel indicates that someone has stopped talking
        /// or that other sound has stopped being transmitted.
        /// </summary>
        Notalk = (int)switch_event_types_t.SWITCH_EVENT_NOTALK,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to be generated by FreeSWITCH.
        /// </summary>
        SessionCrash = (int)switch_event_types_t.SWITCH_EVENT_SESSION_CRASH,

        /// <summary>
        /// Raised when a module has been loaded by NeonSwitch.
        /// </summary>
        ModuleLoad = (int)switch_event_types_t.SWITCH_EVENT_MODULE_LOAD,

        /// <summary>
        /// Raised when a module has been unloaded by NeonSwitch.
        /// </summary>
        ModuleUnload = (int)switch_event_types_t.SWITCH_EVENT_MODULE_UNLOAD,

        /// <summary>
        /// Raised when a DTMF diget has been detected on the channel.
        /// </summary>
        Dtmf = (int)switch_event_types_t.SWITCH_EVENT_DTMF,

        /// <summary>
        /// Raised when a chat message has been generated and submitted for delivery.
        /// </summary>
        Message = (int)switch_event_types_t.SWITCH_EVENT_MESSAGE,

        /// <summary>
        /// <b>Tenative:</b> A user has indicated that a user has registered with the switch.
        /// </summary>
        PresenceIn = (int)switch_event_types_t.SWITCH_EVENT_PRESENCE_IN,

        /// <summary>
        /// <b>Tenative:</b> Not sure what this is.
        /// </summary>
        PresenceNotifyIn = (int)switch_event_types_t.SWITCH_EVENT_NOTIFY_IN,

        /// <summary>
        /// <b>Tenative:</b> A user has indicated that a user has unregistered with the switch.
        /// </summary>
        PresenceOut = (int)switch_event_types_t.SWITCH_EVENT_PRESENCE_OUT,

        /// <summary>
        /// <b>Tenative:</b> The switch is requesting the presence state for a specific user.
        /// </summary>
        PresenceProbe = (int)switch_event_types_t.SWITCH_EVENT_PRESENCE_PROBE,

        /// <summary>
        /// Raised in response to a <see cref="MessageWaitingQuery" /> for a specific user
        /// to indicate whether the user has any voicmail messages waiting to be heard.
        /// </summary>
        MessageWaiting = (int)switch_event_types_t.SWITCH_EVENT_MESSAGE_WAITING,

        /// <summary>
        /// Raised by an application or module to determine whether a specific user 
        /// has voicemail messages waiting.  A <see cref="MessageWaiting" /> event
        /// may be subsequently raised with the response.
        /// </summary>
        MessageWaitingQuery = (int)switch_event_types_t.SWITCH_EVENT_MESSAGE_QUERY,

        /// <summary>
        /// <b>Tenative:</b> Not entirely sure about what this does.  It looks like this
        /// is a request to generate the set of users currently registered with switch.
        /// </summary>
        Roster = (int)switch_event_types_t.SWITCH_EVENT_ROSTER,

        /// <summary>
        /// Raised by a codec as it processes media on a channel.
        /// </summary>
        Codec = (int)switch_event_types_t.SWITCH_EVENT_CODEC,

        /// <summary>
        /// Raised when a specific background job has completed.
        /// </summary>
        BackgroundJob = (int)switch_event_types_t.SWITCH_EVENT_BACKGROUND_JOB,

        /// <summary>
        /// Raised when speech has been detected on a channel.
        /// </summary>
        DetectedSpeech = (int)switch_event_types_t.SWITCH_EVENT_DETECTED_SPEECH,

        /// <summary>
        /// Raised when a tone (such as a fax tone) is detected on a channel.
        /// </summary>
        DetectedTone = (int)switch_event_types_t.SWITCH_EVENT_DETECTED_TONE,

        /// <summary>
        /// Used internally by FreeSWITCH.
        /// </summary>
        PrivateCommand = (int)switch_event_types_t.SWITCH_EVENT_PRIVATE_COMMAND,

        /// <summary>
        /// Raised periodically (every 20 seconds by default) by NeonSwitch to 
        /// relay global status information such as up-time, session count, and
        /// the session creation rate for the switch.
        /// </summary>
        Heartbeat = (int)switch_event_types_t.SWITCH_EVENT_HEARTBEAT,

        /// <summary>
        /// <b>Tenative:</b> Raised when a critical switch error has occured.
        /// </summary>
        CriticalError = (int)switch_event_types_t.SWITCH_EVENT_TRAP,

        /// <summary>
        /// Raised to schedule a task to be perfomed at a schedule time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This functionality is provided by FreeSWITCH to persistantly
        /// schedule tasks.  The schedules are saved to the embedded SQL-Lite
        /// database.
        /// </para>
        /// <para>
        /// Although this functionality is available to NeonSwitch application,
        /// the expectation is that most applications will use their own
        /// task scheduling methods.
        /// </para>
        /// </remarks>
        AddSchedule = (int)switch_event_types_t.SWITCH_EVENT_ADD_SCHEDULE,

        /// <summary>
        /// Raised to unschedule a task.
        /// </summary>
        RemoveSchedule = (int)switch_event_types_t.SWITCH_EVENT_DEL_SCHEDULE,

        /// <summary>
        /// Raised when a scheduled task is to be executed.
        /// </summary>
        ExecuteSchedule = (int)switch_event_types_t.SWITCH_EVENT_EXE_SCHEDULE,

        /// <summary>
        /// Raised to reschedule a task's execution.
        /// </summary>
        Reschedule = (int)switch_event_types_t.SWITCH_EVENT_RE_SCHEDULE,

        /// <summary>
        /// Raised when the FreeSWITCH XML configuration has been reloaded.
        /// </summary>
        ReloadXml = (int)switch_event_types_t.SWITCH_EVENT_RELOADXML,

        /// <summary>
        /// Raised to instruct the SIP stack to send a NOTIFY message to
        /// a particular user.
        /// </summary>
        Notify = (int)switch_event_types_t.SWITCH_EVENT_NOTIFY,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to be generated by FreeSWITCH.
        /// </summary>
        SendMessage = (int)switch_event_types_t.SWITCH_EVENT_SEND_MESSAGE,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to be generated by FreeSWITCH.
        /// </summary>
        ReceiveMessage = (int)switch_event_types_t.SWITCH_EVENT_RECV_MESSAGE,

        /// <summary>
        /// <b>Tenative:</b> This appears to be used internally by FreeSWITCH as a bit
        /// of a hack to pass collections of header and variables around.  This
        /// does not appear to be actually raised as an event.
        /// </summary>
        RequestParams = (int)switch_event_types_t.SWITCH_EVENT_REQUEST_PARAMS,

        /// <summary>
        /// <b>Tenative:</b> Not entirely sure what this is for.  It appears to be used
        /// internally to hold call related parameters for calls being originated by
        /// FreeSWITCH.  This event may not actually be raised.
        /// </summary>
        ChannelData = (int)switch_event_types_t.SWITCH_EVENT_CHANNEL_DATA,

        /// <summary>
        /// <b>Tenative:</b> This appears to be used internally by FreeSWITCH as a bit
        /// of a hack to pass collections of header and variables around.  This
        /// does not appear to be actually raised as an event.
        /// </summary>
        General = (int)switch_event_types_t.SWITCH_EVENT_GENERAL,

        /// <summary>
        /// <b>Tenative:</b> This appears to be used internally by FreeSWITCH and installed
        /// modules to queue private commands to themselves.
        /// </summary>
        Command = (int)switch_event_types_t.SWITCH_EVENT_COMMAND,

        /// <summary>
        /// Raised periodically (when enabled) for call channels with statistics about 
        /// the length of call, etc.
        /// </summary>
        SessionHeartbeat = (int)switch_event_types_t.SWITCH_EVENT_SESSION_HEARTBEAT,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to raised by FreeSWITCH.
        /// </summary>
        ClientDisconnected = (int)switch_event_types_t.SWITCH_EVENT_CLIENT_DISCONNECTED,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to raised by FreeSWITCH.
        /// </summary>
        ServerDisconnected = (int)switch_event_types_t.SWITCH_EVENT_SERVER_DISCONNECTED,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to raised by FreeSWITCH.
        /// </summary>
        SendInfo = (int)switch_event_types_t.SWITCH_EVENT_SEND_INFO,

        /// <summary>
        /// <b>Obsolete:</b> Does not appear to raised by FreeSWITCH.
        /// </summary>
        ReceiveInfo = (int)switch_event_types_t.SWITCH_EVENT_RECV_INFO,

        /// <summary>
        /// Raised by the SIP stack when RTCP messages are received.
        /// </summary>
        ReceiveRTCPMessage = (int)switch_event_types_t.SWITCH_EVENT_RECV_RTCP_MESSAGE,

        /// <summary>
        /// Raised for calls that are being encrypted for transmission.  The code
        /// for doing this appears to be disabled in the default FreeSWITCH build.
        /// </summary>
        CallSecure = (int)switch_event_types_t.SWITCH_EVENT_CALL_SECURE,

        /// <summary>
        /// Raised by FreeSWITCH as it makes/detects changes to the NAT port mappings
        /// made by the local router.
        /// </summary>
        NAT = (int)switch_event_types_t.SWITCH_EVENT_NAT,

        /// <summary>
        /// Raised when channel recording has started.
        /// </summary>
        RecordStart = (int)switch_event_types_t.SWITCH_EVENT_RECORD_START,

        /// <summary>
        /// Raised when channel recording has stopped.
        /// </summary>
        RecordStop = (int)switch_event_types_t.SWITCH_EVENT_RECORD_STOP,

        /// <summary>
        /// Raised when file playback has started on a channel.
        /// </summary>
        PlaybackStart = (int)switch_event_types_t.SWITCH_EVENT_PLAYBACK_START,

        /// <summary>
        /// Raised when file playback has stopped on a channel.
        /// </summary>
        PlaybackStop = (int)switch_event_types_t.SWITCH_EVENT_PLAYBACK_STOP,

        /// <summary>
        /// Raised by the SIP stack when an update to the call's status
        /// information has changed.
        /// </summary>
        CallUpdate = (int)switch_event_types_t.SWITCH_EVENT_CALL_UPDATE,

        /// <summary>
        /// Raised when there's a critical switch failure.  This is currently
        /// raised by the SIP stack if it was unable to initalize itself.
        /// </summary>
        Failure = (int)switch_event_types_t.SWITCH_EVENT_FAILURE,

        /// <summary>
        /// Raised when data is sent by the dial plan to an outbound event socket.
        /// </summary>
        SocketData = (int)switch_event_types_t.SWITCH_EVENT_SOCKET_DATA,

        /// <summary>
        /// Raised when a bug has been placed on a channel to snoop on the media
        /// being transferred.
        /// </summary>
        MediaBugStart = (int)switch_event_types_t.SWITCH_EVENT_MEDIA_BUG_START,

        /// <summary>
        /// Raised when a bug has been removed from a channel and media snooping
        /// has ceased.
        /// </summary>
        MediaBugStop = (int)switch_event_types_t.SWITCH_EVENT_MEDIA_BUG_STOP,

        /// <summary>
        /// A special code used internally for subscribing to all events.  This
        /// will never be raised by NeonSwitch.
        /// </summary>
        AllEvents = (int)switch_event_types_t.SWITCH_EVENT_ALL,
    }
}
