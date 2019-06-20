//-----------------------------------------------------------------------------
// FILE:        _SyncRouter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for MsgRouter

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    /// <summary>
    /// Extends MsgRouter for testing purposes.
    /// </summary>
    public class _SyncRouter : MsgRouter
    {
        private object syncLock = new object();
        private string name;
        private Queue<Msg> recvQueue;
        private IPEndPoint cloudEP;
        private IPEndPoint udpEP;
        private IPEndPoint tcpEP;
        private TimeSpan maxIdle;

        public _SyncRouter(string name, IPEndPoint cloudEP, IPEndPoint udpEP, IPEndPoint tcpEP, string algorithm, byte[] key, byte[] IV)
            : base()
        {
            base.EnableEncryption(new SymmetricKey(string.Format("{0}:{1}:{2}", algorithm, Convert.ToBase64String(key), Convert.ToBase64String(IV))));

            this.name = name;
            this.recvQueue = new Queue<Msg>();
            this.cloudEP = cloudEP;
            this.udpEP = udpEP;
            this.tcpEP = tcpEP;
            this.maxIdle = TimeSpan.FromHours(1);
            base.TcpDelay = false;
        }

        public TimeSpan MaxIdle
        {
            get { return maxIdle; }
            set { maxIdle = value; }
        }

        public void Start()
        {
            base.RouterEP = new MsgEP("physical://foo.com:50/test/" + Helper.NewGuid().ToString());
            base.BkInterval = TimeSpan.FromSeconds(1);
            base.Start(IPAddress.Any, cloudEP, udpEP, tcpEP, 10, maxIdle);
        }

        public void Clear()
        {
            lock (syncLock)
            {
                recvQueue.Clear();
            }
        }

        public int ReceiveCount
        {
            get
            {
                lock (syncLock)
                    return recvQueue.Count;
            }
        }

        public Msg DequeueReceived()
        {
            lock (syncLock)
                return (Msg)recvQueue.Dequeue();
        }

        public void WaitReceived(int count)
        {
            DateTime start = SysTime.Now;
            TimeSpan maxTime = new TimeSpan(0, 0, 0, 10);
            int c;

            while (true)
            {

                lock (syncLock)
                    c = recvQueue.Count;

                if (c >= count)
                    return;

                if (SysTime.Now - start >= maxTime)
                    throw new TimeoutException();

                Thread.Sleep(0);
            }
        }

        protected override void OnReceive(Msg msg)
        {
            lock (syncLock)
                recvQueue.Enqueue(msg);
        }
    }
}

