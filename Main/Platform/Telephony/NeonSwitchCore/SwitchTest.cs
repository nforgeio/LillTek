//-----------------------------------------------------------------------------
// FILE:        SwitchTest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manual test code.

#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Manual test code.
    /// </summary>
    public static class SwitchTest
    {
        public static void Test()
        {
            //Switch.UserDirectoryEvent += 
            //    (s,a) => {

            //        a.SwitchEvent.Dump("Directory Event");

            //        a.Handled                 = true;
            //        a.AccessDenied            = false;
            //        a.Password                = "2063561304";
            //        a.VoiceMailPassword       = "1000";
            //        a.CallingRight             = CallingRight.Unrestricted;
            //        a.AccountCode             = "1000";
            //        a.CallerContext           = "default";
            //        a.EffectiveCallerIDName   = "Extension 1000";
            //        a.EffectiveCallerIDNumber = "1000";
            //        a.OutboundCallerIDName    = "Jeff Lill";
            //        a.OutboundCallerIDNumber  = "2063561304";
            //    };

            Switch.DialPlanEvent +=
                (s, a) =>
                {
                    a.SwitchEvent.Dump("Dialplan Event");

                    if (a.DialedNumber == "9198")
                        return;

                    a.Handled = true;

                    //a.Actions.Add(new PreAnswerAction());
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.Busy,5))));

                    //a.Actions.Add(new RingReadyAction());
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(10)));
                    //a.Actions.Add(new AnswerAction());
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(TelephoneTone.CallingCard)));

                    a.Actions.Add(new AnswerAction());

                    //a.Actions.Add(new TransferAction("9198"));

                    //a.Actions.Add(new LogAction("** DEBUG BEFORE ***"));
                    //a.Actions.Add(new LogLevelAction(SwitchLogLevel.Debug));
                    //a.Actions.Add(new LogAction("** DEBUG BEFORE ***"));
                    //a.Actions.Add(new LogAction(SwitchLogLevel.Alert,"** TEST ALERT ***"));
                    //a.Actions.Add(new TransferAction("9198") { Delay=TimeSpan.FromSeconds(20) });
                    //a.Actions.Add(new PlayAudioAction(AudioSource.File("${sounds_dir}/music/8000/danza-espanola-op-37-h-142-xii-arabesca.wav")));

                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(TelephoneTone.CallingCard)));
                    //a.Actions.Add(new BroadcastAction(CallLeg.Both,"${sounds_dir}/en/us/callie/directory/8000/dir-please_try_again.wav"));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(10)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(TelephoneTone.CallingCard)));
                    //a.Actions.Add(new BroadcastAction(CallLeg.Both,"${sounds_dir}/en/us/callie/directory/8000/dir-please_try_again.wav",TimeSpan.FromSeconds(3)));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(10)));

                    //a.Actions.Add(new PlayAudioAction(AudioSource.Speech(Phrase.PhoneText("Hello, I am Microsoft Helen.  I am a much better voice than the crappy free switch default.  Want to talk dirty with me?  Connecting you now."))));
                    //a.Actions.Add(new SetVariableAction("effective_caller_id_number","425-577-6203"));
                    //a.Actions.Add(new BridgeAction("sofia/gateway/pstn-out/2063561304"));

                    //a.Actions.Add(new PlayDtmfAction("2063561304"));
                    //a.Actions.Add(new PlayDtmfAction("1234",TimeSpan.FromSeconds(1)));

                    //var playAudio = new PlayAudioAction(AudioSource.File("${sounds_dir}/music/8000/danza-espanola-op-37-h-142-xii-arabesca.wav"));
                    //playAudio.StopDtmf = "#";
                    //playAudio.EventVariables["Hello"] = "World!";
                    //a.Actions.Add(playAudio);

                    //var playAudio =
                    //    new PlayAudioAction(
                    //        AudioSource.Combine(
                    //            AudioSource.Tone(TelephoneTone.Busy),
                    //            AudioSource.Silence(TimeSpan.FromSeconds(2)),
                    //            AudioSource.Speech("Hello World"),
                    //            AudioSource.Silence(TimeSpan.FromSeconds(2)),
                    //            AudioSource.LocalStream("${hold_music}")
                    //        ));
                    //playAudio.StopDtmf = "*#";
                    //a.Actions.Add(new SetPersonaAction(Persona.Default));
                    //a.Actions.Add(playAudio);

                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.USRing,5))));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(1)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.CallingCard,5))));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(1)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.NotInService,5))));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(1)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.DistinctiveRing,5))));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(1)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.Busy,5))));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(1)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.DialTone))));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(1)));
                    //a.Actions.Add(new PlayAudioAction(AudioSource.Tone(new TelephoneTone(TelephoneTone.PbxDialTone))));

                    //a.Actions.Add(new EchoAction());

                    //a.Actions.Add(new SwitchAction("set","tts_engine=flite"));
                    //a.Actions.Add(new SwitchAction("set","tts_voice=kal"));
                    //a.Actions.Add(new SwitchAction("speak","Hello World!"));

                    //a.Actions.Add(new PlayAudioAction(AudioSource.File("${sounds_dir}/music/8000/danza-espanola-op-37-h-142-xii-arabesca.wav")));
                    //a.Actions.Add(new SleepAction(TimeSpan.FromSeconds(10)));
                    //a.Actions.Add(new BreakAction(true));

                    //a.Actions.Add(new HangupAction());

                    a.Actions.Add(new StartSessionAction("NeonSwitch", string.Empty));
                };

            Switch.EventReceived +=
                (s, a) =>
                {
                    a.SwitchEvent.Dump("Event Received: {0}", a.SwitchEvent.EventType);
                };

            Switch.CallSessionEvent +=
                (s, a) =>
                {
                    TestSession(a.Session);
                };
        }

        private static void TestSession(CallSession session)
        {
            session.Answer();

            while (session.IsActive)
            {
                session.Speak("Hello World!");
                session.Hangup();
            }
        }
    }
}

#endif
