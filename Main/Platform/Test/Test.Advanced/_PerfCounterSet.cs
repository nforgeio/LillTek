//-----------------------------------------------------------------------------
// FILE:        _PerfCounterSet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for performance counter classes

using System;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _PerfCounterSet
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PerfCounterSet_Install_Uninstall()
        {
            PerfCounterSet counters = new PerfCounterSet(false, true, "Test_Install_Uninstall", "Test Help");
            PerfCounter A, B;

            try
            {
                counters.Add(new PerfCounter("A", "A Help", PerformanceCounterType.NumberOfItems32));
                counters.Add(new PerfCounter("B", "B Help", PerformanceCounterType.NumberOfItems32));

                counters.Install();

                A = counters["A"];
                Assert.IsNotNull(A.Counter);
                Assert.AreEqual(0, A.RawValue);

                Assert.AreEqual(1, A.Increment());
                Assert.AreEqual(1, A.RawValue);

                B = counters["B"];
                B.IncrementBy(55);
                Assert.AreEqual(55, B.RawValue);
            }
            finally
            {
                counters.Uninstall();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PerfCounterSet_Instances()
        {
            PerfCounterSet counters = new PerfCounterSet(true, true, "Test_Instances", "Test Help");
            PerfCounter A_ONE, A_TWO, B_ONE, B_TWO;

            try
            {
                counters.Add(new PerfCounter("A", "A Help", PerformanceCounterType.NumberOfItems32));
                counters.Add(new PerfCounter("B", "B Help", PerformanceCounterType.NumberOfItems32));

                counters.Install();

                A_ONE = counters["A", "ONE"];
                A_TWO = counters["A", "TWO"];
                B_ONE = counters["B", "ONE"];
                B_TWO = counters["B", "TWO"];

                A_ONE.IncrementBy(1);
                A_TWO.IncrementBy(2);
                B_ONE.IncrementBy(3);
                B_TWO.IncrementBy(4);

                Assert.AreEqual(1, A_ONE.RawValue);
                Assert.AreEqual(2, A_TWO.RawValue);
                Assert.AreEqual(3, B_ONE.RawValue);
                Assert.AreEqual(4, B_TWO.RawValue);
            }
            finally
            {
                counters.Uninstall();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PerfCounterSet_Related()
        {
            PerfCounterSet counters = new PerfCounterSet(false, true, "Test_Related", "Test Help");
            PerfCounter A, B, C;

            try
            {
                A = new PerfCounter("A", "A Help", PerformanceCounterType.NumberOfItems32);
                A.RelatedCounters = new string[] { "C" };
                counters.Add(A);

                B = new PerfCounter("B", "B Help", PerformanceCounterType.NumberOfItems32);
                B.RelatedCounters = new string[] { "C" };
                counters.Add(B);

                counters.Add(new PerfCounter("C", "C Help", PerformanceCounterType.NumberOfItems32));

                counters.Install();

                A = counters["A"];
                B = counters["B"];
                C = counters["C"];

                Assert.AreEqual(0, C.RawValue);
                A.Increment();
                Assert.AreEqual(1, C.RawValue);
                B.IncrementBy(5);
                Assert.AreEqual(6, C.RawValue);
                A.Decrement();
                Assert.AreEqual(5, C.RawValue);
            }
            finally
            {
                counters.Uninstall();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PerfCounterSet_Related2()
        {
            PerfCounterSet counters = new PerfCounterSet(false, true, "Test_Related2", "Test Help");
            PerfCounter A, B, C;

            try
            {
                counters.Add(new PerfCounter("A", "A Help", PerformanceCounterType.NumberOfItems32));
                counters.Add(new PerfCounter("B", "B Help", PerformanceCounterType.NumberOfItems32));
                counters.Add(new PerfCounter("C", "C Help", PerformanceCounterType.NumberOfItems32));

                counters.Relate("A", "C");
                counters.Relate("B", "C");

                counters.Install();

                A = counters["A"];
                B = counters["B"];
                C = counters["C"];

                Assert.AreEqual(0, C.RawValue);
                A.Increment();
                Assert.AreEqual(1, C.RawValue);
                B.IncrementBy(5);
                Assert.AreEqual(6, C.RawValue);
                A.Decrement();
                Assert.AreEqual(5, C.RawValue);
            }
            finally
            {
                counters.Uninstall();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PerfCounterSet_Related_Instance()
        {
            PerfCounterSet counters = new PerfCounterSet(true, true, "Test_Related_Instance", "Test Help");
            PerfCounter A1, B1, C1;
            PerfCounter A2, B2, C2;

            try
            {
                counters.Add(new PerfCounter("A", "A Help", PerformanceCounterType.NumberOfItems32));
                counters.Add(new PerfCounter("B", "B Help", PerformanceCounterType.NumberOfItems32));
                counters.Add(new PerfCounter("C", "C Help", PerformanceCounterType.NumberOfItems32));

                counters.Relate("A", "C");
                counters.Relate("B", "C");

                counters.Install();

                A1 = counters["A", "1"];
                B1 = counters["B", "1"];
                C1 = counters["C", "1"];
                A2 = counters["A", "2"];
                B2 = counters["B", "2"];
                C2 = counters["C", "2"];

                Assert.AreEqual(0, C1.RawValue);
                A1.Increment();
                Assert.AreEqual(1, C1.RawValue);
                B1.IncrementBy(5);
                Assert.AreEqual(6, C1.RawValue);
                A1.Decrement();
                Assert.AreEqual(5, C1.RawValue);

                Assert.AreEqual(0, C2.RawValue);
                A2.IncrementBy(100);
                Assert.AreEqual(100, C2.RawValue);
                B2.IncrementBy(100);
                Assert.AreEqual(200, C2.RawValue);
            }
            finally
            {
                counters.Uninstall();
            }
        }
    }
}

