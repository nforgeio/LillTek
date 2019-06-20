//-----------------------------------------------------------------------------
// FILE:        MPC550.cs
// OWNER:       JEFFL
// COPYRIGHT:   Copyright (c) 2005 by Jeff Lill.  All rights reserved.
// DESCRIPTION: Imports the MPC550.DLL entry points.

#if WINCE

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using LillTek.Common;

namespace LillTek.LowLevel
{
    public sealed class MPC550IO
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns true if the MPC550 driver is available in the current hardware and O/S environment.
        /// </summary>
        public static bool Present
        {
            get
            {

                // Basically, all I'm going to do is look for the MPC550IO.DLL file
                // in the \Windows directory ands return true if I find it.  I could
                // get fancy and check the registry also verify that the MPC550IODrv.DLL
                // is also installed.

                return File.Exists(@"\Windows\MPC550IO.DLL");
            }
        }

        // Entry points into MPC550IO.DLL.

        [DllImport("mpc550io.dll")]
        static extern IntPtr MPC550IO_Open(bool fPortAIn, bool fPortBIn, bool fPortCIn);

        [DllImport("mpc550io.dll")]
        static extern void MPC550IO_Close(IntPtr hBoard);

        [DllImport("mpc550io.dll")]
        static extern byte MPC550IO_DIGet(IntPtr hBoard, byte port);

        [DllImport("mpc550io.dll")]
        static extern void MPC550IO_DOSet(IntPtr hBoard, byte port, byte value);

        [DllImport("mpc550io.dll")]
        static extern void MPC550IO_ADGet(IntPtr hBoard, int port, int voltRange, out float volts);

        [DllImport("mpc550io.dll")]
        static extern void MPC550IO_DASet(IntPtr hBoard, int port, ref float volts);

        [DllImport("mpc550io.dll")]
        static extern uint MPC550IO_GetCount(IntPtr hBoard, int counter, bool reset);

        //-----------------------------------------------------------------------------------------
        // Dynamic members

        private const string AlreadyOpenMsg      = "Driver is already open.";
        private const string NotOpenMsg          = "Driver is not open.";
        private const string DriverNotPresentMsg = "MPC550IO.DLL driver not present.";

        private IntPtr      hBoard;     // The board's driver handle
        private ADGain[]    adGain;     // The gain codes for each A/D input port

        /// <summary>
        /// Constructor.
        /// </summary>
        public MPC550IO()
        {
            hBoard = IntPtr.Zero;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~MPC550IO()
        {
            if (hBoard != IntPtr.Zero)
            {
                MPC550IO_Close(hBoard);
                hBoard = IntPtr.Zero;
            }
        }

        /// <summary>
        /// A/D voltage gain codes.  Note that these values must not be
        /// changed without first changing the corresponding constants
        /// defined in the underlying Windows CE driver.
        /// </summary>
        public enum ADGain
        {
            UNIPOLAR_5  = 0,    // 0..+5 volts
            UNIPOLAR_10 = 1,    // 0..+10 volts
            BIPOLAR_5   = 2,    // -5..+5 volts
            BIPOLAR_10  = 3     // -10..+10 volts
        }

        /// <summary>
        /// Configuration parameters.
        /// </summary>
        public sealed class Config
        {
            /// <summary>
            /// True to configure the A group ports as inputs, false as outputs.
            /// This defaults to true.
            /// </summary>
            public bool PortAIn;

            /// <summary>
            /// True to configure the B group ports as inputs, false as outputs.
            /// This defaults to false.
            /// </summary>
            public bool PortBIn;

            /// <summary>
            /// True to configure the C group ports as inputs, false as outputs.
            /// This defaults to false.
            /// </summary>
            public bool PortCIn;

            /// <summary>
            /// Specifies the gains to be used when performing the A/D
            /// conversion on each A/D input port.  This will be initialized
            /// to a 16 element array of ADGain.UNIPOLAR_10.
            /// </summary>
            public ADGain[] ADGain;

            /// <summary>
            /// Constructor;
            /// </summary>
            public Config()
            {
                PortAIn = true;
                PortBIn = false;
                PortCIn = false;

                ADGain = new ADGain[16];
                for (int i = 0; i < 16; i++)
                    ADGain[i] = MPC550IO.ADGain.UNIPOLAR_10;
            }
        }

        /// <summary>
        /// Opens and configures the board driver.
        /// </summary>
        /// <param name="config">The configuration parameters.</param>
        public void Open(Config config)
        {
            if (hBoard != IntPtr.Zero)
                throw new InvalidOperationException(AlreadyOpenMsg);

            if (!Present)
                throw new InvalidOperationException(DriverNotPresentMsg);

            try
            {
                adGain = config.ADGain;

                hBoard = MPC550IO_Open(config.PortAIn, config.PortBIn, config.PortCIn);
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
                MPC550IO_Close(hBoard);
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
            return MPC550IO_DIGet(hBoard, (byte)port);
        }

        /// <summary>
        /// Sets the values for the digital I/O port group specified.
        /// </summary>
        /// <param name="port">A=0, B=1, C=2</param>
        /// <param name="value">Bit map of the 8 ports to set the group.</param>
        public void DOSet(int port, int value)
        {
            MPC550IO_DOSet(hBoard, (byte)port, (byte)value);
        }

        /// <summary>
        /// Returns the current voltage on the specified A/D port.
        /// </summary>
        /// <param name="port">The A/D port (0..15).</param>
        /// <returns>The port voltage.</returns>
        public float ADGet(int port)
        {
            float volts;

            MPC550IO_ADGet(hBoard, port, (int)adGain[port], out volts);
            return volts;
        }

        /// <summary>
        /// Sets the voltage on the specified D/A port.
        /// </summary>
        /// <param name="port">The D/A port (0..7).</param>
        /// <param name="volts">The voltage to set.</param>
        public void DASet(int port, float volts)
        {
            MPC550IO_DASet(hBoard, port, ref volts);
        }

        /// <summary>
        /// Returns the maximum value the counter can reach before wrapping.
        /// </summary>
        public uint CTRMaxValue
        {
            get { return (uint)0x0000FFFF; }
        }

        /// <summary>
        /// Returns the current value of the specified counter, optionally
        /// resetting it to 0.
        /// </summary>
        /// <param name="counter">The counter (0..2)</param>
        /// <param name="reset">True to reset the counter.</param>
        /// <returns>The counter value.</returns>
        public uint CTRGet(int counter, bool reset)
        {
            return MPC550IO_GetCount(hBoard, counter, reset);
        }
    }
}

#endif // WINCE