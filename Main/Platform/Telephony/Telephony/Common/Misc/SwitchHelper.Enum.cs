//-----------------------------------------------------------------------------
// FILE:        SwitchHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: NeonSwitch related utlities.

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
    /// NeonSwitch related utlities.
    /// </summary>
    public static partial class SwitchHelper
    {
        private static Dictionary<string, SwitchEventCode>      stringToSwitchEvent;
        private static Dictionary<SwitchEventCode, string>      switchEventToString;

        private static Dictionary<string, ChannelState>         stringToChannelState;
        private static Dictionary<ChannelState, string>         channelStateToString;

        private static Dictionary<string, SwitchHangupReason>   stringToSwitchHangupReason;
        private static Dictionary<SwitchHangupReason, string>   switchHangupReasonToString;

        private static Dictionary<string, CallDirection>        stringToCallDirection;
        private static Dictionary<CallDirection, string>        callDirectionToString;

        private static Dictionary<string, SwitchLogLevel>       stringToSwitchLogLevel;
        private static Dictionary<SwitchLogLevel, string>       switchLogLevelToString;

        /// <summary>
        /// Initialize the string to enumeration mapping dictionaries.
        /// </summary>
        private static void InitEnumMappings()
        {
            stringToSwitchEvent = new Dictionary<string, SwitchEventCode>(StringComparer.OrdinalIgnoreCase);
            stringToSwitchEvent["CUSTOM"] = SwitchEventCode.Custom;
            stringToSwitchEvent["CLONE"] = SwitchEventCode.Clone;
            stringToSwitchEvent["CHANNEL_CREATE"] = SwitchEventCode.ChannelCreate;
            stringToSwitchEvent["CHANNEL_DESTROY"] = SwitchEventCode.ChannelDestroy;
            stringToSwitchEvent["CHANNEL_STATE"] = SwitchEventCode.ChannelState;
            stringToSwitchEvent["CHANNEL_CALLSTATE"] = SwitchEventCode.ChannelCallState;
            stringToSwitchEvent["CHANNEL_ANSWER"] = SwitchEventCode.ChannelAnswer;
            stringToSwitchEvent["CHANNEL_HANGUP"] = SwitchEventCode.ChannelHangup;
            stringToSwitchEvent["CHANNEL_HANGUP_COMPLETE"] = SwitchEventCode.ChannelHangupComplete;
            stringToSwitchEvent["CHANNEL_EXECUTE"] = SwitchEventCode.ChannelExecute;
            stringToSwitchEvent["CHANNEL_EXECUTE_COMPLETE"] = SwitchEventCode.ChannelExecuteComplete;
            stringToSwitchEvent["CHANNEL_HOLD"] = SwitchEventCode.ChannelHold;
            stringToSwitchEvent["CHANNEL_UNHOLD"] = SwitchEventCode.ChannelUnhold;
            stringToSwitchEvent["CHANNEL_BRIDGE"] = SwitchEventCode.ChannelBridge;
            stringToSwitchEvent["CHANNEL_UNBRIDGE"] = SwitchEventCode.ChannelUnbridge;
            stringToSwitchEvent["CHANNEL_PROGRESS"] = SwitchEventCode.ChannelProgress;
            stringToSwitchEvent["CHANNEL_PROGRESS_MEDIA"] = SwitchEventCode.ChannelProgressMedia;
            stringToSwitchEvent["CHANNEL_OUTGOING"] = SwitchEventCode.ChannelOutgoing;
            stringToSwitchEvent["CHANNEL_PARK"] = SwitchEventCode.ChannelPark;
            stringToSwitchEvent["CHANNEL_UNPARK"] = SwitchEventCode.ChannelUnpark;
            stringToSwitchEvent["CHANNEL_APPLICATION"] = SwitchEventCode.ChannelAction;
            stringToSwitchEvent["CHANNEL_ORIGINATE"] = SwitchEventCode.ChannelOriginate;
            stringToSwitchEvent["CHANNEL_UUID"] = SwitchEventCode.ChannelUUID;
            stringToSwitchEvent["API"] = SwitchEventCode.Api;
            stringToSwitchEvent["LOG"] = SwitchEventCode.Log;
            stringToSwitchEvent["INBOUND_CHAN"] = SwitchEventCode.InboundChannel;
            stringToSwitchEvent["OUTBOUND_CHAN"] = SwitchEventCode.OutboundChannel;
            stringToSwitchEvent["STARTUP"] = SwitchEventCode.Startup;
            stringToSwitchEvent["SHUTDOWN"] = SwitchEventCode.Shutdown;
            stringToSwitchEvent["PUBLISH"] = SwitchEventCode.Publish;
            stringToSwitchEvent["UNPUBLISH"] = SwitchEventCode.Unpublish;
            stringToSwitchEvent["TALK"] = SwitchEventCode.Talk;
            stringToSwitchEvent["NOTALK"] = SwitchEventCode.Notalk;
            stringToSwitchEvent["SESSION_CRASH"] = SwitchEventCode.SessionCrash;
            stringToSwitchEvent["MODULE_LOAD"] = SwitchEventCode.ModuleLoad;
            stringToSwitchEvent["MODULE_UNLOAD"] = SwitchEventCode.ModuleUnload;
            stringToSwitchEvent["DTMF"] = SwitchEventCode.Dtmf;
            stringToSwitchEvent["MESSAGE"] = SwitchEventCode.Message;
            stringToSwitchEvent["PRESENCE_IN"] = SwitchEventCode.PresenceIn;
            stringToSwitchEvent["NOTIFY_IN"] = SwitchEventCode.PresenceNotifyIn;
            stringToSwitchEvent["PRESENCE_OUT"] = SwitchEventCode.PresenceOut;
            stringToSwitchEvent["PRESENCE_PROBE"] = SwitchEventCode.PresenceProbe;
            stringToSwitchEvent["MESSAGE_WAITING"] = SwitchEventCode.MessageWaiting;
            stringToSwitchEvent["MESSAGE_QUERY"] = SwitchEventCode.MessageWaitingQuery;
            stringToSwitchEvent["ROSTER"] = SwitchEventCode.Roster;
            stringToSwitchEvent["CODEC"] = SwitchEventCode.Codec;
            stringToSwitchEvent["BACKGROUND_JOB"] = SwitchEventCode.BackgroundJob;
            stringToSwitchEvent["DETECTED_SPEECH"] = SwitchEventCode.DetectedSpeech;
            stringToSwitchEvent["DETECTED_TONE"] = SwitchEventCode.DetectedTone;
            stringToSwitchEvent["PRIVATE_COMMAND"] = SwitchEventCode.PrivateCommand;
            stringToSwitchEvent["HEARTBEAT"] = SwitchEventCode.Heartbeat;
            stringToSwitchEvent["TRAP"] = SwitchEventCode.CriticalError;
            stringToSwitchEvent["ADD_SCHEDULE"] = SwitchEventCode.AddSchedule;
            stringToSwitchEvent["DEL_SCHEDULE"] = SwitchEventCode.RemoveSchedule;
            stringToSwitchEvent["EXE_SCHEDULE"] = SwitchEventCode.ExecuteSchedule;
            stringToSwitchEvent["RE_SCHEDULE"] = SwitchEventCode.Reschedule;
            stringToSwitchEvent["RELOADXML"] = SwitchEventCode.ReloadXml;
            stringToSwitchEvent["NOTIFY"] = SwitchEventCode.Notify;
            stringToSwitchEvent["SEND_MESSAGE"] = SwitchEventCode.SendMessage;
            stringToSwitchEvent["RECV_MESSAGE"] = SwitchEventCode.ReceiveMessage;
            stringToSwitchEvent["REQUEST_PARAMS"] = SwitchEventCode.RequestParams;
            stringToSwitchEvent["CHANNEL_DATA"] = SwitchEventCode.ChannelData;
            stringToSwitchEvent["GENERAL"] = SwitchEventCode.General;
            stringToSwitchEvent["COMMAND"] = SwitchEventCode.Command;
            stringToSwitchEvent["SESSION_HEARTBEAT"] = SwitchEventCode.SessionHeartbeat;
            stringToSwitchEvent["CLIENT_DISCONNECTED"] = SwitchEventCode.ClientDisconnected;
            stringToSwitchEvent["SERVER_DISCONNECTED"] = SwitchEventCode.ServerDisconnected;
            stringToSwitchEvent["SEND_INFO"] = SwitchEventCode.SendInfo;
            stringToSwitchEvent["RECV_INFO"] = SwitchEventCode.ReceiveInfo;
            stringToSwitchEvent["RECV_RTCP_MESSAGE"] = SwitchEventCode.ReceiveRTCPMessage;
            stringToSwitchEvent["CALL_SECURE"] = SwitchEventCode.CallSecure;
            stringToSwitchEvent["NAT"] = SwitchEventCode.NAT;
            stringToSwitchEvent["RECORD_START"] = SwitchEventCode.RecordStart;
            stringToSwitchEvent["RECORD_STOP"] = SwitchEventCode.RecordStop;
            stringToSwitchEvent["PLAYBACK_START"] = SwitchEventCode.PlaybackStart;
            stringToSwitchEvent["PLAYBACK_STOP"] = SwitchEventCode.PlaybackStop;
            stringToSwitchEvent["CALL_UPDATE"] = SwitchEventCode.CallUpdate;
            stringToSwitchEvent["FAILURE"] = SwitchEventCode.Failure;
            stringToSwitchEvent["SOCKET_DATA"] = SwitchEventCode.SocketData;
            stringToSwitchEvent["MEDIA_BUG_START"] = SwitchEventCode.MediaBugStart;
            stringToSwitchEvent["MEDIA_BUG_STOP"] = SwitchEventCode.MediaBugStop;

            switchEventToString = new Dictionary<SwitchEventCode, string>();
            switchEventToString[SwitchEventCode.Custom] = "CUSTOM";
            switchEventToString[SwitchEventCode.Clone] = "CLONE";
            switchEventToString[SwitchEventCode.ChannelCreate] = "CHANNEL_CREATE";
            switchEventToString[SwitchEventCode.ChannelDestroy] = "CHANNEL_DESTROY";
            switchEventToString[SwitchEventCode.ChannelState] = "CHANNEL_STATE";
            switchEventToString[SwitchEventCode.ChannelCallState] = "CHANNEL_CALLSTATE";
            switchEventToString[SwitchEventCode.ChannelAnswer] = "CHANNEL_ANSWER";
            switchEventToString[SwitchEventCode.ChannelHangup] = "CHANNEL_HANGUP";
            switchEventToString[SwitchEventCode.ChannelHangupComplete] = "CHANNEL_HANGUP_COMPLETE";
            switchEventToString[SwitchEventCode.ChannelExecute] = "CHANNEL_EXECUTE";
            switchEventToString[SwitchEventCode.ChannelExecuteComplete] = "CHANNEL_EXECUTE_COMPLETE";
            switchEventToString[SwitchEventCode.ChannelHold] = "CHANNEL_HOLD";
            switchEventToString[SwitchEventCode.ChannelUnhold] = "CHANNEL_UNHOLD";
            switchEventToString[SwitchEventCode.ChannelBridge] = "CHANNEL_BRIDGE";
            switchEventToString[SwitchEventCode.ChannelUnbridge] = "CHANNEL_UNBRIDGE";
            switchEventToString[SwitchEventCode.ChannelProgress] = "CHANNEL_PROGRESS";
            switchEventToString[SwitchEventCode.ChannelProgressMedia] = "CHANNEL_PROGRESS_MEDIA";
            switchEventToString[SwitchEventCode.ChannelOutgoing] = "CHANNEL_OUTGOING";
            switchEventToString[SwitchEventCode.ChannelPark] = "CHANNEL_PARK";
            switchEventToString[SwitchEventCode.ChannelUnpark] = "CHANNEL_UNPARK";
            switchEventToString[SwitchEventCode.ChannelAction] = "CHANNEL_APPLICATION";
            switchEventToString[SwitchEventCode.ChannelOriginate] = "CHANNEL_ORIGINATE";
            switchEventToString[SwitchEventCode.ChannelUUID] = "CHANNEL_UUID";
            switchEventToString[SwitchEventCode.Api] = "API";
            switchEventToString[SwitchEventCode.Log] = "LOG";
            switchEventToString[SwitchEventCode.InboundChannel] = "INBOUND_CHAN";
            switchEventToString[SwitchEventCode.OutboundChannel] = "OUTBOUND_CHAN";
            switchEventToString[SwitchEventCode.Startup] = "STARTUP";
            switchEventToString[SwitchEventCode.Shutdown] = "SHUTDOWN";
            switchEventToString[SwitchEventCode.Publish] = "PUBLISH";
            switchEventToString[SwitchEventCode.Unpublish] = "UNPUBLISH";
            switchEventToString[SwitchEventCode.Talk] = "TALK";
            switchEventToString[SwitchEventCode.Notalk] = "NOTALK";
            switchEventToString[SwitchEventCode.SessionCrash] = "SESSION_CRASH";
            switchEventToString[SwitchEventCode.ModuleLoad] = "MODULE_LOAD";
            switchEventToString[SwitchEventCode.ModuleUnload] = "MODULE_UNLOAD";
            switchEventToString[SwitchEventCode.Dtmf] = "DTMF";
            switchEventToString[SwitchEventCode.Message] = "MESSAGE";
            switchEventToString[SwitchEventCode.PresenceIn] = "PRESENCE_IN";
            switchEventToString[SwitchEventCode.PresenceNotifyIn] = "NOTIFY_IN";
            switchEventToString[SwitchEventCode.PresenceOut] = "PRESENCE_OUT";
            switchEventToString[SwitchEventCode.PresenceProbe] = "PRESENCE_PROBE";
            switchEventToString[SwitchEventCode.MessageWaiting] = "MESSAGE_WAITING";
            switchEventToString[SwitchEventCode.MessageWaitingQuery] = "MESSAGE_QUERY";
            switchEventToString[SwitchEventCode.Roster] = "ROSTER";
            switchEventToString[SwitchEventCode.Codec] = "CODEC";
            switchEventToString[SwitchEventCode.BackgroundJob] = "BACKGROUND_JOB";
            switchEventToString[SwitchEventCode.DetectedSpeech] = "DETECTED_SPEECH";
            switchEventToString[SwitchEventCode.DetectedTone] = "DETECTED_TONE";
            switchEventToString[SwitchEventCode.PrivateCommand] = "PRIVATE_COMMAND";
            switchEventToString[SwitchEventCode.Heartbeat] = "HEARTBEAT";
            switchEventToString[SwitchEventCode.CriticalError] = "TRAP";
            switchEventToString[SwitchEventCode.AddSchedule] = "ADD_SCHEDULE";
            switchEventToString[SwitchEventCode.RemoveSchedule] = "DEL_SCHEDULE";
            switchEventToString[SwitchEventCode.ExecuteSchedule] = "EXE_SCHEDULE";
            switchEventToString[SwitchEventCode.Reschedule] = "RE_SCHEDULE";
            switchEventToString[SwitchEventCode.ReloadXml] = "RELOADXML";
            switchEventToString[SwitchEventCode.Notify] = "NOTIFY";
            switchEventToString[SwitchEventCode.SendMessage] = "SEND_MESSAGE";
            switchEventToString[SwitchEventCode.ReceiveMessage] = "RECV_MESSAGE";
            switchEventToString[SwitchEventCode.RequestParams] = "REQUEST_PARAMS";
            switchEventToString[SwitchEventCode.ChannelData] = "CHANNEL_DATA";
            switchEventToString[SwitchEventCode.General] = "GENERAL";
            switchEventToString[SwitchEventCode.Command] = "COMMAND";
            switchEventToString[SwitchEventCode.SessionHeartbeat] = "SESSION_HEARTBEAT";
            switchEventToString[SwitchEventCode.ClientDisconnected] = "CLIENT_DISCONNECTED";
            switchEventToString[SwitchEventCode.ServerDisconnected] = "SERVER_DISCONNECTED";
            switchEventToString[SwitchEventCode.SendInfo] = "SEND_INFO";
            switchEventToString[SwitchEventCode.ReceiveInfo] = "RECV_INFO";
            switchEventToString[SwitchEventCode.ReceiveRTCPMessage] = "RECV_RTCP_MESSAGE";
            switchEventToString[SwitchEventCode.CallSecure] = "CALL_SECURE";
            switchEventToString[SwitchEventCode.NAT] = "NAT";
            switchEventToString[SwitchEventCode.RecordStart] = "RECORD_START";
            switchEventToString[SwitchEventCode.RecordStop] = "RECORD_STOP";
            switchEventToString[SwitchEventCode.PlaybackStart] = "PLAYBACK_START";
            switchEventToString[SwitchEventCode.PlaybackStop] = "PLAYBACK_STOP";
            switchEventToString[SwitchEventCode.CallUpdate] = "CALL_UPDATE";
            switchEventToString[SwitchEventCode.Failure] = "FAILURE";
            switchEventToString[SwitchEventCode.SocketData] = "SOCKET_DATA";
            switchEventToString[SwitchEventCode.MediaBugStart] = "MEDIA_BUG_START";
            switchEventToString[SwitchEventCode.MediaBugStop] = "MEDIA_BUG_STOP";

            stringToChannelState = new Dictionary<string, ChannelState>(StringComparer.OrdinalIgnoreCase);
            stringToChannelState["NEW"] = ChannelState.New;
            stringToChannelState["INIT"] = ChannelState.Initialized;
            stringToChannelState["ROUTING"] = ChannelState.Routing;
            stringToChannelState["SOFT_EXECUTE"] = ChannelState.SoftExecute;
            stringToChannelState["EXECUTE"] = ChannelState.Execute;
            stringToChannelState["EXCHANGE_MEDIA"] = ChannelState.ExchangingMedia;
            stringToChannelState["PARK"] = ChannelState.Park;
            stringToChannelState["CONSUME_MEDIA"] = ChannelState.ConsumingMedia;
            stringToChannelState["HIBERNATE"] = ChannelState.Hibernate;
            stringToChannelState["RESET"] = ChannelState.Reset;
            stringToChannelState["HANGUP"] = ChannelState.Hangup;
            stringToChannelState["REPORTING"] = ChannelState.Reporting;
            stringToChannelState["DESTROY"] = ChannelState.Destroy;
            stringToChannelState["NONE"] = ChannelState.None;

            channelStateToString = new Dictionary<ChannelState, string>();
            channelStateToString[ChannelState.New] = "NEW";
            channelStateToString[ChannelState.Initialized] = "INIT";
            channelStateToString[ChannelState.Routing] = "ROUTING";
            channelStateToString[ChannelState.SoftExecute] = "SOFT_EXECUTE";
            channelStateToString[ChannelState.Execute] = "EXECUTE";
            channelStateToString[ChannelState.ExchangingMedia] = "EXCHANGE_MEDIA";
            channelStateToString[ChannelState.Park] = "PARK";
            channelStateToString[ChannelState.ConsumingMedia] = "CONSUME_MEDIA";
            channelStateToString[ChannelState.Hibernate] = "HIBERNATE";
            channelStateToString[ChannelState.Reset] = "RESET";
            channelStateToString[ChannelState.Hangup] = "HANGUP";
            channelStateToString[ChannelState.Reporting] = "REPORTING";
            channelStateToString[ChannelState.Destroy] = "DESTROY";
            channelStateToString[ChannelState.None] = "NONE";

            stringToSwitchHangupReason = new Dictionary<string, SwitchHangupReason>(StringComparer.OrdinalIgnoreCase);
            stringToSwitchHangupReason["NONE"] = SwitchHangupReason.None;
            stringToSwitchHangupReason["UNALLOCATED_NUMBER"] = SwitchHangupReason.UnallocatedNumber;
            stringToSwitchHangupReason["NO_ROUTE_TRANSIT_NET"] = SwitchHangupReason.NoRouteTransitNet;
            stringToSwitchHangupReason["NO_ROUTE_DESTINATION"] = SwitchHangupReason.NoRouteDestination;
            stringToSwitchHangupReason["CHANNEL_UNACCEPTABLE"] = SwitchHangupReason.ChannelUnacceptable;
            stringToSwitchHangupReason["CALL_AWARDED_DELIVERED"] = SwitchHangupReason.CallAwardedDelivered;
            stringToSwitchHangupReason["NORMAL_CLEARING"] = SwitchHangupReason.NormalClearing;
            stringToSwitchHangupReason["USER_BUSY"] = SwitchHangupReason.UserBusy;
            stringToSwitchHangupReason["NO_USER_RESPONSE"] = SwitchHangupReason.NoUserResponse;
            stringToSwitchHangupReason["NO_ANSWER"] = SwitchHangupReason.NoAnswer;
            stringToSwitchHangupReason["SUBSCRIBER_ABSENT"] = SwitchHangupReason.SubscriberAbsent;
            stringToSwitchHangupReason["CALL_REJECTED"] = SwitchHangupReason.CallRejected;
            stringToSwitchHangupReason["NUMBER_CHANGED"] = SwitchHangupReason.NumberChanged;
            stringToSwitchHangupReason["REDIRECTION_TO_NEW_DESTINATION"] = SwitchHangupReason.RedirectionToNewDestination;
            stringToSwitchHangupReason["EXCHANGE_ROUTING_ERROR"] = SwitchHangupReason.ExchangeRoutingError;
            stringToSwitchHangupReason["DESTINATION_OUT_OF_ORDER"] = SwitchHangupReason.DestinationOutOfOrder;
            stringToSwitchHangupReason["INVALID_NUMBER_FORMAT"] = SwitchHangupReason.InvalidNumberFormat;
            stringToSwitchHangupReason["FACILITY_REJECTED"] = SwitchHangupReason.FacilityRejected;
            stringToSwitchHangupReason["RESPONSE_TO_STATUS_ENQUIRY"] = SwitchHangupReason.ResponseToStatusInquery;
            stringToSwitchHangupReason["NORMAL_UNSPECIFIED"] = SwitchHangupReason.NormalUnspecified;
            stringToSwitchHangupReason["NORMAL_CIRCUIT_CONGESTION"] = SwitchHangupReason.NormalCircuitCongestion;
            stringToSwitchHangupReason["NETWORK_OUT_OF_ORDER"] = SwitchHangupReason.NetworkOutOfOrder;
            stringToSwitchHangupReason["NORMAL_TEMPORARY_FAILURE"] = SwitchHangupReason.NormalTemporaryFailure;
            stringToSwitchHangupReason["SWITCH_CONGESTION"] = SwitchHangupReason.SwitchCongestion;
            stringToSwitchHangupReason["ACCESS_INFO_DISCARDED"] = SwitchHangupReason.AccessInfoDiscarded;
            stringToSwitchHangupReason["REQUESTED_CHAN_UNAVAIL"] = SwitchHangupReason.RequestedChannelNotAvailable;
            stringToSwitchHangupReason["PRE_EMPTED"] = SwitchHangupReason.PreEmpted;
            stringToSwitchHangupReason["FACILITY_NOT_SUBSCRIBED"] = SwitchHangupReason.FacilityNotSubscribed;
            stringToSwitchHangupReason["OUTGOING_CALL_BARRED"] = SwitchHangupReason.OutgoingCallBarred;
            stringToSwitchHangupReason["INCOMING_CALL_BARRED"] = SwitchHangupReason.IncomingCallBarred;
            stringToSwitchHangupReason["BEARERCAPABILITY_NOTAUTH"] = SwitchHangupReason.BearerCapabilityNotAuthorized;
            stringToSwitchHangupReason["BEARERCAPABILITY_NOTAVAIL"] = SwitchHangupReason.BearerCapabilityNotAvailable;
            stringToSwitchHangupReason["SERVICE_UNAVAILABLE"] = SwitchHangupReason.ServiceNotAvailable;
            stringToSwitchHangupReason["CHAN_NOT_IMPLEMENTED"] = SwitchHangupReason.ChannelNotImplemented;
            stringToSwitchHangupReason["FACILITY_NOT_IMPLEMENTED"] = SwitchHangupReason.FacilityNotImplemented;
            stringToSwitchHangupReason["SERVICE_NOT_IMPLEMENTED"] = SwitchHangupReason.ServiceNotImplemented;
            stringToSwitchHangupReason["INVALID_CALL_REFERENCE"] = SwitchHangupReason.InvalidCallReference;
            stringToSwitchHangupReason["INCOMPATIBLE_DESTINATION"] = SwitchHangupReason.IncompatibleDestination;
            stringToSwitchHangupReason["INVALID_MSG_UNSPECIFIED"] = SwitchHangupReason.InvalidMessageUnspecified;
            stringToSwitchHangupReason["MANDATORY_IE_MISSING"] = SwitchHangupReason.RequiredParameterMissing;
            stringToSwitchHangupReason["MESSAGE_TYPE_NONEXIST"] = SwitchHangupReason.MessageTypeNotSupported;
            stringToSwitchHangupReason["WRONG_MESSAGE"] = SwitchHangupReason.WrongMessage;
            stringToSwitchHangupReason["IE_NONEXIST"] = SwitchHangupReason.ParameterNotSupported;
            stringToSwitchHangupReason["INVALID_IE_CONTENTS"] = SwitchHangupReason.InvalidParameterValue;
            stringToSwitchHangupReason["WRONG_CALL_STATE"] = SwitchHangupReason.WrongCallState;
            stringToSwitchHangupReason["RECOVERY_ON_TIMER_EXPIRE"] = SwitchHangupReason.RecoveryOnTimerExpire;
            stringToSwitchHangupReason["MANDATORY_IE_LENGTH_ERROR"] = SwitchHangupReason.RequiredParameterLengthError;
            stringToSwitchHangupReason["PROTOCOL_ERROR"] = SwitchHangupReason.ProtocolError;
            stringToSwitchHangupReason["INTERWORKING"] = SwitchHangupReason.Internetworking;
            stringToSwitchHangupReason["SUCCESS"] = SwitchHangupReason.Success;
            stringToSwitchHangupReason["ORIGINATOR_CANCEL"] = SwitchHangupReason.OriginatorCancel;
            stringToSwitchHangupReason["CRASH,"] = SwitchHangupReason.Crash;
            stringToSwitchHangupReason["SYSTEM_SHUTDOWN"] = SwitchHangupReason.SystemShutdown;
            stringToSwitchHangupReason["LOSE_RACE"] = SwitchHangupReason.LoseRace;
            stringToSwitchHangupReason["MANAGER_REQUEST"] = SwitchHangupReason.ManagerRequest;
            stringToSwitchHangupReason["BLIND_TRANSFER"] = SwitchHangupReason.BlindTransfer;
            stringToSwitchHangupReason["ATTENDED_TRANSFER"] = SwitchHangupReason.AttendedTransfer;
            stringToSwitchHangupReason["ALLOTTED_TIMEOUT"] = SwitchHangupReason.AllottedTimeout;
            stringToSwitchHangupReason["USER_CHALLENGE"] = SwitchHangupReason.UserChallenge;
            stringToSwitchHangupReason["MEDIA_TIMEOUT"] = SwitchHangupReason.MediaTimeout;
            stringToSwitchHangupReason["PICKED_OFF"] = SwitchHangupReason.PickedOff;
            stringToSwitchHangupReason["USER_NOT_REGISTERED"] = SwitchHangupReason.UserNotRegistered;
            stringToSwitchHangupReason["PROGRESS_TIMEOUT"] = SwitchHangupReason.ProgressTimeout; ;

            switchHangupReasonToString = new Dictionary<SwitchHangupReason, string>();
            switchHangupReasonToString[SwitchHangupReason.None] = "NONE";
            switchHangupReasonToString[SwitchHangupReason.UnallocatedNumber] = "UNALLOCATED_NUMBER";
            switchHangupReasonToString[SwitchHangupReason.NoRouteTransitNet] = "NO_ROUTE_TRANSIT_NET";
            switchHangupReasonToString[SwitchHangupReason.NoRouteDestination] = "NO_ROUTE_DESTINATION";
            switchHangupReasonToString[SwitchHangupReason.ChannelUnacceptable] = "CHANNEL_UNACCEPTABLE";
            switchHangupReasonToString[SwitchHangupReason.CallAwardedDelivered] = "CALL_AWARDED_DELIVERED";
            switchHangupReasonToString[SwitchHangupReason.NormalClearing] = "NORMAL_CLEARING";
            switchHangupReasonToString[SwitchHangupReason.UserBusy] = "USER_BUSY";
            switchHangupReasonToString[SwitchHangupReason.NoUserResponse] = "NO_USER_RESPONSE";
            switchHangupReasonToString[SwitchHangupReason.NoAnswer] = "NO_ANSWER";
            switchHangupReasonToString[SwitchHangupReason.SubscriberAbsent] = "SUBSCRIBER_ABSENT";
            switchHangupReasonToString[SwitchHangupReason.CallRejected] = "CALL_REJECTED";
            switchHangupReasonToString[SwitchHangupReason.NumberChanged] = "NUMBER_CHANGED";
            switchHangupReasonToString[SwitchHangupReason.RedirectionToNewDestination] = "REDIRECTION_TO_NEW_DESTINATION";
            switchHangupReasonToString[SwitchHangupReason.ExchangeRoutingError] = "EXCHANGE_ROUTING_ERROR";
            switchHangupReasonToString[SwitchHangupReason.DestinationOutOfOrder] = "DESTINATION_OUT_OF_ORDER";
            switchHangupReasonToString[SwitchHangupReason.InvalidNumberFormat] = "INVALID_NUMBER_FORMAT";
            switchHangupReasonToString[SwitchHangupReason.FacilityRejected] = "FACILITY_REJECTED";
            switchHangupReasonToString[SwitchHangupReason.ResponseToStatusInquery] = "RESPONSE_TO_STATUS_ENQUIRY";
            switchHangupReasonToString[SwitchHangupReason.NormalUnspecified] = "NORMAL_UNSPECIFIED";
            switchHangupReasonToString[SwitchHangupReason.NormalCircuitCongestion] = "NORMAL_CIRCUIT_CONGESTION";
            switchHangupReasonToString[SwitchHangupReason.NetworkOutOfOrder] = "NETWORK_OUT_OF_ORDER";
            switchHangupReasonToString[SwitchHangupReason.NormalTemporaryFailure] = "NORMAL_TEMPORARY_FAILURE";
            switchHangupReasonToString[SwitchHangupReason.SwitchCongestion] = "SWITCH_CONGESTION";
            switchHangupReasonToString[SwitchHangupReason.AccessInfoDiscarded] = "ACCESS_INFO_DISCARDED";
            switchHangupReasonToString[SwitchHangupReason.RequestedChannelNotAvailable] = "REQUESTED_CHAN_UNAVAIL";
            switchHangupReasonToString[SwitchHangupReason.PreEmpted] = "PRE_EMPTED";
            switchHangupReasonToString[SwitchHangupReason.FacilityNotSubscribed] = "FACILITY_NOT_SUBSCRIBED";
            switchHangupReasonToString[SwitchHangupReason.OutgoingCallBarred] = "OUTGOING_CALL_BARRED";
            switchHangupReasonToString[SwitchHangupReason.IncomingCallBarred] = "INCOMING_CALL_BARRED";
            switchHangupReasonToString[SwitchHangupReason.BearerCapabilityNotAuthorized] = "BEARERCAPABILITY_NOTAUTH";
            switchHangupReasonToString[SwitchHangupReason.BearerCapabilityNotAvailable] = "BEARERCAPABILITY_NOTAVAIL";
            switchHangupReasonToString[SwitchHangupReason.ServiceNotAvailable] = "SERVICE_UNAVAILABLE";
            switchHangupReasonToString[SwitchHangupReason.ChannelNotImplemented] = "CHAN_NOT_IMPLEMENTED";
            switchHangupReasonToString[SwitchHangupReason.FacilityNotImplemented] = "FACILITY_NOT_IMPLEMENTED";
            switchHangupReasonToString[SwitchHangupReason.ServiceNotImplemented] = "SERVICE_NOT_IMPLEMENTED";
            switchHangupReasonToString[SwitchHangupReason.InvalidCallReference] = "INVALID_CALL_REFERENCE";
            switchHangupReasonToString[SwitchHangupReason.IncompatibleDestination] = "INCOMPATIBLE_DESTINATION";
            switchHangupReasonToString[SwitchHangupReason.InvalidMessageUnspecified] = "INVALID_MSG_UNSPECIFIED";
            switchHangupReasonToString[SwitchHangupReason.RequiredParameterMissing] = "MANDATORY_IE_MISSING";
            switchHangupReasonToString[SwitchHangupReason.MessageTypeNotSupported] = "MESSAGE_TYPE_NONEXIST";
            switchHangupReasonToString[SwitchHangupReason.WrongMessage] = "WRONG_MESSAGE";
            switchHangupReasonToString[SwitchHangupReason.ParameterNotSupported] = "IE_NONEXIST";
            switchHangupReasonToString[SwitchHangupReason.InvalidParameterValue] = "INVALID_IE_CONTENTS";
            switchHangupReasonToString[SwitchHangupReason.WrongCallState] = "WRONG_CALL_STATE";
            switchHangupReasonToString[SwitchHangupReason.RecoveryOnTimerExpire] = "RECOVERY_ON_TIMER_EXPIRE";
            switchHangupReasonToString[SwitchHangupReason.RequiredParameterLengthError] = "MANDATORY_IE_LENGTH_ERROR";
            switchHangupReasonToString[SwitchHangupReason.ProtocolError] = "PROTOCOL_ERROR";
            switchHangupReasonToString[SwitchHangupReason.Internetworking] = "INTERWORKING";
            switchHangupReasonToString[SwitchHangupReason.Success] = "SUCCESS";
            switchHangupReasonToString[SwitchHangupReason.OriginatorCancel] = "ORIGINATOR_CANCEL";
            switchHangupReasonToString[SwitchHangupReason.Crash] = "CRASH,";
            switchHangupReasonToString[SwitchHangupReason.SystemShutdown] = "SYSTEM_SHUTDOWN";
            switchHangupReasonToString[SwitchHangupReason.LoseRace] = "LOSE_RACE";
            switchHangupReasonToString[SwitchHangupReason.ManagerRequest] = "MANAGER_REQUEST";
            switchHangupReasonToString[SwitchHangupReason.BlindTransfer] = "BLIND_TRANSFER";
            switchHangupReasonToString[SwitchHangupReason.AttendedTransfer] = "ATTENDED_TRANSFER";
            switchHangupReasonToString[SwitchHangupReason.AllottedTimeout] = "ALLOTTED_TIMEOUT";
            switchHangupReasonToString[SwitchHangupReason.UserChallenge] = "USER_CHALLENGE";
            switchHangupReasonToString[SwitchHangupReason.MediaTimeout] = "MEDIA_TIMEOUT";
            switchHangupReasonToString[SwitchHangupReason.PickedOff] = "PICKED_OFF";
            switchHangupReasonToString[SwitchHangupReason.UserNotRegistered] = "USER_NOT_REGISTERED";
            switchHangupReasonToString[SwitchHangupReason.ProgressTimeout] = "PROGRESS_TIMEOUT";

            stringToCallDirection = new Dictionary<string, CallDirection>(StringComparer.OrdinalIgnoreCase);
            stringToCallDirection["Inbound"] = CallDirection.Inbound;
            stringToCallDirection["Outbound"] = CallDirection.Outbound;

            callDirectionToString = new Dictionary<CallDirection, string>();
            callDirectionToString[CallDirection.Inbound] = "Inbound";
            callDirectionToString[CallDirection.Outbound] = "Outbound";

            stringToSwitchLogLevel = new Dictionary<string, SwitchLogLevel>(StringComparer.Ordinal);
            stringToSwitchLogLevel["NONE"] = SwitchLogLevel.None;
            stringToSwitchLogLevel["CONSOLE"] = SwitchLogLevel.Console;
            stringToSwitchLogLevel["ALERT"] = SwitchLogLevel.Alert;
            stringToSwitchLogLevel["CRIT"] = SwitchLogLevel.Critical;
            stringToSwitchLogLevel["ERR"] = SwitchLogLevel.Error;
            stringToSwitchLogLevel["WARNING"] = SwitchLogLevel.Warning;
            stringToSwitchLogLevel["NOTICE"] = SwitchLogLevel.Notice;
            stringToSwitchLogLevel["INFO"] = SwitchLogLevel.Info;
            stringToSwitchLogLevel["DEBUG"] = SwitchLogLevel.Debug;

            switchLogLevelToString = new Dictionary<SwitchLogLevel, string>();
            switchLogLevelToString[SwitchLogLevel.None] = "NONE";
            switchLogLevelToString[SwitchLogLevel.Console] = "CONSOLE";
            switchLogLevelToString[SwitchLogLevel.Alert] = "ALERT";
            switchLogLevelToString[SwitchLogLevel.Critical] = "CRIT";
            switchLogLevelToString[SwitchLogLevel.Error] = "ERR";
            switchLogLevelToString[SwitchLogLevel.Warning] = "WARNING";
            switchLogLevelToString[SwitchLogLevel.Notice] = "NOTICE";
            switchLogLevelToString[SwitchLogLevel.Info] = "INFO";
            switchLogLevelToString[SwitchLogLevel.Debug] = "DEBUG";
        }

        /// <summary>
        /// Parses the low-level FreeSWITCH event name passed into a <see cref="SwitchEventCode" /> value.
        /// </summary>
        /// <param name="value">The FreeSWITCH event name.</param>
        /// <returns>
        /// The parsed <see cref="SwitchEventCode" /> or <see cref="SwitchEventCode.Custom" />
        /// if the value could not be parsed.
        /// </returns>
        public static SwitchEventCode ParseEventCode(string value)
        {
            SwitchEventCode switchEvent;

            if (!stringToSwitchEvent.TryGetValue(value, out switchEvent))
            {
                SysLog.LogWarning("Unexpected FreeSWITCH event [{0}].", value);
                switchEvent = SwitchEventCode.Custom;
            }

            return switchEvent;
        }

        /// <summary>
        /// Returns the low-level FreeSWITCH string corresponding to a <see cref="SwitchEventCode" />.
        /// </summary>
        /// <param name="switchEvent">The switch event code.</param>
        /// <returns>The corresponding string or <b>SWITCH_EVENT_CUSTOM</b> if the code is unknown.</returns>
        public static string GetEventCodeString(SwitchEventCode switchEvent)
        {
            string eventString;

            if (!switchEventToString.TryGetValue(switchEvent, out eventString))
            {
                SysLog.LogWarning("Unexpected switch event code [{0}].", switchEvent);
                eventString = switchEventToString[SwitchEventCode.Custom];
            }

            return eventString;
        }

        /// <summary>
        /// Parses the low-level FreeSWITCH hangup reason <see cref="SwitchHangupReason" /> value.
        /// </summary>
        /// <param name="reasonString">The FreeSWITCH hangup reason.</param>
        /// <returns>
        /// The parsed <see cref="SwitchHangupReason" /> or <see cref="SwitchHangupReason.None" />
        /// if the value could not be parsed.
        /// </returns>
        public static SwitchHangupReason ParseHangupReason(string reasonString)
        {
            SwitchHangupReason reason;

            if (!stringToSwitchHangupReason.TryGetValue(reasonString, out reason))
            {
                SysLog.LogWarning("Unexpected FreeSWITCH hangup reason [{0}].", reasonString);
                reason = SwitchHangupReason.None;
            }

            return reason;
        }

        /// <summary>
        /// Returns the low-level FreeSWITCH string corresponding to a <see cref="SwitchHangupReason" />.
        /// </summary>
        /// <param name="reason">The hangup reason code.</param>
        /// <returns>The corresponding string or <b>NONE</b> if the code is unknown.</returns>
        public static string GetSwitchHangupReasonString(SwitchHangupReason reason)
        {
            string reasonString;

            if (!switchHangupReasonToString.TryGetValue(reason, out reasonString))
            {
                SysLog.LogWarning("Unexpected switch hangup reason code [{0}].", reason);
                reasonString = switchHangupReasonToString[SwitchHangupReason.None];
            }

            return reasonString;
        }

        /// <summary>
        /// Parses the low-level FreeSWITCH call direction string to a <see cref="CallDirection" /> value.
        /// </summary>
        /// <param name="directionString">The FreeSWITCH direction string.</param>
        /// <returns>
        /// The parsed <see cref="CallDirection" /> or <see cref="CallDirection.Unknown" />
        /// if the value could not be parsed.
        /// </returns>
        public static CallDirection ParseCallDirection(string directionString)
        {
            CallDirection direction;

            if (!stringToCallDirection.TryGetValue(directionString, out direction))
            {
                SysLog.LogWarning("Unexpected FreeSWITCH call direction [{0}].", directionString);
                direction = CallDirection.Unknown;
            }

            return direction;
        }

        /// <summary>
        /// Returns the low-level FreeSWITCH string corresponding to a <see cref="CallDirection" />.
        /// </summary>
        /// <param name="direction">The direction code.</param>
        /// <returns>The corresponding string or <see cref="Empty" /> if the code is unknown.</returns>
        public static string GetSwitchHangupReasonString(CallDirection direction)
        {
            string directionString;

            if (!callDirectionToString.TryGetValue(direction, out directionString))
            {
                SysLog.LogWarning("Unexpected call direction code [{0}].", direction);
                directionString = string.Empty;
            }

            return directionString;
        }

        /// <summary>
        /// Verifies that a <see cref="SwitchEventCode" /> code passed is one of the known events.
        /// </summary>
        /// <param name="switchEvent">The event code being tested.</param>
        /// <returns>
        /// The same event code if it is known to the current implementation, 
        /// <see cref="SwitchEventCode.Custom" /> otherwise.
        /// </returns>
        /// <remarks>
        /// A warning will be logged if the event code is not known.
        /// </remarks>
        public static SwitchEventCode ValidateEventCode(SwitchEventCode switchEvent)
        {
            switch (switchEvent)
            {
                case SwitchEventCode.Custom:
                case SwitchEventCode.Clone:
                case SwitchEventCode.ChannelCreate:
                case SwitchEventCode.ChannelDestroy:
                case SwitchEventCode.ChannelState:
                case SwitchEventCode.ChannelCallState:
                case SwitchEventCode.ChannelAnswer:
                case SwitchEventCode.ChannelHangup:
                case SwitchEventCode.ChannelHangupComplete:
                case SwitchEventCode.ChannelExecute:
                case SwitchEventCode.ChannelExecuteComplete:
                case SwitchEventCode.ChannelHold:
                case SwitchEventCode.ChannelUnhold:
                case SwitchEventCode.ChannelBridge:
                case SwitchEventCode.ChannelUnbridge:
                case SwitchEventCode.ChannelProgress:
                case SwitchEventCode.ChannelProgressMedia:
                case SwitchEventCode.ChannelOutgoing:
                case SwitchEventCode.ChannelPark:
                case SwitchEventCode.ChannelUnpark:
                case SwitchEventCode.ChannelAction:
                case SwitchEventCode.ChannelOriginate:
                case SwitchEventCode.ChannelUUID:
                case SwitchEventCode.Api:
                case SwitchEventCode.Log:
                case SwitchEventCode.InboundChannel:
                case SwitchEventCode.OutboundChannel:
                case SwitchEventCode.Startup:
                case SwitchEventCode.Shutdown:
                case SwitchEventCode.Publish:
                case SwitchEventCode.Unpublish:
                case SwitchEventCode.Talk:
                case SwitchEventCode.Notalk:
                case SwitchEventCode.SessionCrash:
                case SwitchEventCode.ModuleLoad:
                case SwitchEventCode.ModuleUnload:
                case SwitchEventCode.Dtmf:
                case SwitchEventCode.Message:
                case SwitchEventCode.PresenceIn:
                case SwitchEventCode.PresenceNotifyIn:
                case SwitchEventCode.PresenceOut:
                case SwitchEventCode.PresenceProbe:
                case SwitchEventCode.MessageWaiting:
                case SwitchEventCode.MessageWaitingQuery:
                case SwitchEventCode.Roster:
                case SwitchEventCode.Codec:
                case SwitchEventCode.BackgroundJob:
                case SwitchEventCode.DetectedSpeech:
                case SwitchEventCode.DetectedTone:
                case SwitchEventCode.PrivateCommand:
                case SwitchEventCode.Heartbeat:
                case SwitchEventCode.CriticalError:
                case SwitchEventCode.AddSchedule:
                case SwitchEventCode.RemoveSchedule:
                case SwitchEventCode.ExecuteSchedule:
                case SwitchEventCode.Reschedule:
                case SwitchEventCode.ReloadXml:
                case SwitchEventCode.Notify:
                case SwitchEventCode.SendMessage:
                case SwitchEventCode.ReceiveMessage:
                case SwitchEventCode.RequestParams:
                case SwitchEventCode.ChannelData:
                case SwitchEventCode.General:
                case SwitchEventCode.Command:
                case SwitchEventCode.SessionHeartbeat:
                case SwitchEventCode.ClientDisconnected:
                case SwitchEventCode.ServerDisconnected:
                case SwitchEventCode.SendInfo:
                case SwitchEventCode.ReceiveInfo:
                case SwitchEventCode.ReceiveRTCPMessage:
                case SwitchEventCode.CallSecure:
                case SwitchEventCode.NAT:
                case SwitchEventCode.RecordStart:
                case SwitchEventCode.RecordStop:
                case SwitchEventCode.PlaybackStart:
                case SwitchEventCode.PlaybackStop:
                case SwitchEventCode.CallUpdate:
                case SwitchEventCode.Failure:
                case SwitchEventCode.SocketData:
                case SwitchEventCode.MediaBugStart:
                case SwitchEventCode.MediaBugStop:
                case SwitchEventCode.AllEvents:

                    return switchEvent;

                default:

                    SysLog.LogWarning("Unexpected switch event code [{0}].", switchEvent);
                    return SwitchEventCode.Custom;
            }
        }

        /// <summary>
        /// Parses the low-level FreeSWITCH channel state string into a <see cref="ChannelState" /> value.
        /// </summary>
        /// <param name="value">The FreeSWITCH channel state string.</param>
        /// <returns>The parsed <see cref="ChannelState" /> or <see cref="ChannelState.None" />
        /// if the value could not be parsed.
        /// </returns>
        public static ChannelState ParseChannelState(string value)
        {
            ChannelState channelState;

            if (!stringToChannelState.TryGetValue(value, out channelState))
            {
                SysLog.LogWarning("Unexpected FreeSWITCH channel state [{0}].", value);
                channelState = ChannelState.None;
            }

            return channelState;
        }

        /// <summary>
        /// Returns the low-level FreeSWITCH string corresponding to a <see cref="ChannelState" />.
        /// </summary>
        /// <param name="channelState">The channel state code.</param>
        /// <returns>The corresponding string or <b>NONE</b> if the code is unknown.</returns>
        public static string GetChannelStateString(ChannelState channelState)
        {
            string stateString;

            if (!channelStateToString.TryGetValue(channelState, out stateString))
            {
                SysLog.LogWarning("Unexpected channel state code [{0}].", channelState);
                stateString = channelStateToString[ChannelState.None];
            }

            return stateString;
        }

        /// <summary>
        /// Verifies that a <see cref="ChannelState" /> code passed is one of the known states.
        /// </summary>
        /// <param name="channelState">The channel state code being tested.</param>
        /// <returns>
        /// The same state code if it is known to the current implementation, 
        /// <see cref="ChannelState.None" /> otherwise.
        /// </returns>
        /// <remarks>
        /// A warning will be logged if the state code is not known.
        /// </remarks>
        public static ChannelState ValidateChannelState(ChannelState channelState)
        {
            switch (channelState)
            {
                case ChannelState.New:
                case ChannelState.Initialized:
                case ChannelState.Routing:
                case ChannelState.SoftExecute:
                case ChannelState.Execute:
                case ChannelState.ExchangingMedia:
                case ChannelState.Park:
                case ChannelState.ConsumingMedia:
                case ChannelState.Hibernate:
                case ChannelState.Reset:
                case ChannelState.Hangup:
                case ChannelState.Reporting:
                case ChannelState.Destroy:
                case ChannelState.None:

                    return channelState;

                default:

                    SysLog.LogWarning("Unexpected channel state [{0}].", channelState);
                    return ChannelState.None;
            }
        }

        /// <summary>
        /// Parses the low-level FreeSWITCH log level string into a <see cref="SwitchLogLevel" /> value.
        /// </summary>
        /// <param name="value">The FreeSWITCH channel state string.</param>
        /// <returns>The parsed <see cref="SwitchLogLevel" /> or <see cref="SwitchLogLevel.None" />
        /// if the value could not be parsed.
        /// </returns>
        public static SwitchLogLevel ParseSwitchLogLevel(string value)
        {
            SwitchLogLevel logLevel;

            if (!stringToSwitchLogLevel.TryGetValue(value, out logLevel))
            {
                SysLog.LogWarning("Unexpected FreeSWITCH log level [{0}].", value);
                logLevel = SwitchLogLevel.None;
            }

            return logLevel;
        }

        /// <summary>
        /// Returns the low-level FreeSWITCH string corresponding to a <see cref="SwitchLogLevel" />.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns>The corresponding string or <b>NONE</b> if the code is unknown.</returns>
        public static string GetSwitchLogLevelString(SwitchLogLevel logLevel)
        {
            string logLevelString;

            if (!switchLogLevelToString.TryGetValue(logLevel, out logLevelString))
            {
                SysLog.LogWarning("Unexpected channel log level [{0}].", logLevel);
                logLevelString = switchLogLevelToString[SwitchLogLevel.None];
            }

            return logLevelString;
        }
    }
}
