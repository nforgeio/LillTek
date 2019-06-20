//-----------------------------------------------------------------------------
// FILE:        SBC1625IO.cs
// OWNER:       JEFFL
// COPYRIGHT:   Copyright (c) 2005 by Jeff Lill.  All rights reserved.
// DESCRIPTION: Imports the SBC1625IO.DLL entry points.

#if WINCE

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using LillTek.Common;

namespace LillTek.LowLevel
{
    public sealed class SBC1625IO
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns true if the SBC1625IO driver is available in the current hardware and O/S environment.
        /// </summary>
        public static bool Present
        {
            get
            {
                // Basically, all I'm going to do is look for the SBC1625IO.DLL file
                // in the \Windows directory ands return true if I find it.  I could
                // get fancy and check the registry also verify that the MSC1625IODrv.DLL
                // is also installed.

                return File.Exists(@"\Windows\SBC1625IO.DLL");
            }
        }

        // Entry points into SBC1625IO.DLL.

        [DllImport("sbc1625io.dll")]
        static extern IntPtr SBC1625IO_Open(bool fEnableIO, bool fPortAIn, bool fPortBIn, bool fPortCIn);

        [DllImport("sbc1625io.dll")]
        static extern void SBC1625IO_Close(IntPtr hBoard);

        [DllImport("sbc1625io.dll")]
        static extern byte SBC1625IO_DIGet(IntPtr hBoard, byte port);

        [DllImport("sbc1625io.dll")]
        static extern void SBC1625IO_DOSet(IntPtr hBoard, byte port, byte value);

        [DllImport("sbc1625io.dll")]
        static extern void SBC1625IO_WatchDogEnable(IntPtr hBoard, bool enable);

        [DllImport("sbc1625io.dll")]
        static extern void SBC1625IO_WatchDogSet(IntPtr hBoard, uint count);

        [DllImport("sbc1625io.dll")]
        static extern uint SBC1625IO_WatchDogCountRate(IntPtr hBoard);

        [DllImport("sbc1625io.dll")]
        static extern bool SBC1625IO_WatchDogExpired(IntPtr hBoard);

        //-----------------------------------------------------------------------------------------
        // Dynamic members

        private const string AlreadyOpenMsg      = "Driver is already open.";
        private const string NotOpenMsg          = "Driver is not open.";
        private const string DriverNotPresentMsg = "SBC1625IO.DLL driver not present.";

        private IntPtr hBoard;     // The board's driver handle

        /// <summary>
        /// Constructor.
        /// </summary>
        public SBC1625IO()
        {
            hBoard = IntPtr.Zero;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SBC1625IO()
        {
            if (hBoard != IntPtr.Zero)
            {
                SBC1625IO_Close(hBoard);
                hBoard = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Configuration parameters.
        /// </summary>
        public sealed class Config
        {
            /// <summary>
            /// Configure the general purpose I/O ports with this instance.  Set this to 
            /// false if all you want to do is to configure the driver to access the 
            /// board's watchdog timer.
            /// </summary>
            public bool EnableIO;

            /// <summary>
            /// True to configure the A group ports as inputs, false as outputs
            /// </summary>
            public bool PortAIn;

            /// <summary>
            /// True to configure the B group ports as inputs, false as outputs
            /// </summary>
            public bool PortBIn;

            /// <summary>
            /// True to configure the C group ports as inputs, false as outputs
            /// </summary>
            public bool PortCIn;

            /// <summary>
            /// Constructor;
            /// </summary>
            public Config()
            {
                EnableIO = true;
                PortAIn  = true;
                PortBIn  = false;
                PortCIn  = false;
            }
        }

        /// <summary>
        /// Opens and configures the board driver.
        /// </summary>
        /// <param name="config">The configuration parameters.</param>
        /// <remarks>
        /// Pass config.EnableIO=false if all you want to do is to configure the driver to
        /// access the board's watchdog timer.
        /// </remarks>
        public void Open(Config config)
        {
            if (hBoard != IntPtr.Zero)
                throw new InvalidOperationException(AlreadyOpenMsg);

            if (!Present)
                throw new InvalidOperationException(DriverNotPresentMsg);

            try
            {
                hBoard = SBC1625IO_Open(config.EnableIO, config.PortAIn, config.PortBIn, config.PortCIn);
                if (hBoard == IntPtr.Zero)
                    throw new InvalidOperationException(DriverNotPresentMsg);
            }
            catch
            {
                throw new InvalidOperationException(DriverNotPresentMsg);
            }
        }

        /// <summary>
        /// Closes the driver, releasing any resources associated with it.
        /// </summary>
        public void Close()
        {
            if (hBoard != IntPtr.Zero)
            {
                SBC1625IO_Close(hBoard);
                hBoard = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Returns the values for the digital I/O port group specified.
        /// </summary>
        /// <param name="port">A=0, B=1, C=2</param>
        /// <returns>Bit map of the 8 ports in the group.</returns>
        public int DIGet(int port)
        {
            return SBC1625IO_DIGet(hBoard, (byte)port);
        }

        /// <summary>
        /// Sets the values for the digital I/O port group specified.
        /// </summary>
        /// <param name="port">A=0, B=1, C=2</param>
        /// <param name="value">Bit map of the 8 ports to set the group.</param>
        public void DOSet(int port, int value)
        {
            SBC1625IO_DOSet(hBoard, (byte)port, (byte)value);
        }

        /// <summary>
        /// Enables/disables the watchdog timer.
        /// </summary>
        /// <param name="enable">The new enable state.</param>
        /// <remarks>
        /// If the watchdog timer is being enabled after being disabled, this
        /// method will set the timer count to maximum value supported
        /// by the board.
        /// </remarks>
        public void WatchDogEnable(bool enable)
        {
            SBC1625IO_WatchDogEnable(hBoard, enable);
        }

        /// <summary>
        /// Sets the watchdog timer to the count passed.
        /// </summary>
        /// <param name="count">The new count.</param>
        public void WatchDogSet(uint count)
        {
            SBC1625IO_WatchDogSet(hBoard, count);
        }

        /// <summary>
        /// Returns the rate at which the watchdog timer is decremented
        /// in counts per second.
        /// </summary>
        /// <returns></returns>
        public uint WatchDogCountRate()
        {
            return SBC1625IO_WatchDogCountRate(hBoard);
        }

        /// <summary>
        /// Returns true if the last device reset was caused by the
        /// expiration of the watchdog timer.
        /// </summary>
        /// <returns></returns>
        public bool WatchDogExpired()
        {
            return SBC1625IO_WatchDogExpired(hBoard);
        }
    }
}

#endif // WINCE