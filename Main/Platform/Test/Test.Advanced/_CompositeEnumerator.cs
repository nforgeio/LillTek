//-----------------------------------------------------------------------------
// FILE:        _CompositeEnumerator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _CompositeEnumerator
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void CompositeEnumerator_NoEnumerators()
        {
            // Should barf because there's no enumerators passed.

            ExtendedAssert.Throws<ArgumentException>(() => new CompositeEnumerator<int>(new IEnumerator<int>[0]));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void CompositeEnumerator_SingleEnumerator()
        {
            List<int> source = new List<int>();
            List<int> output = new List<int>();
            CompositeEnumerator<int> enumerator;

            source.Add(0);
            source.Add(1);
            source.Add(2);

            enumerator = new CompositeEnumerator<int>(source.GetEnumerator());

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(3, output.Count);
            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(1, output[1]);
            Assert.AreEqual(2, output[2]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void CompositeEnumerator_MultipleEnumerators()
        {
            List<int> source1 = new List<int>();
            List<int> source2 = new List<int>();
            List<int> source3 = new List<int>();
            List<int> output = new List<int>();
            CompositeEnumerator<int> enumerator;

            source1.Add(0);
            source1.Add(1);
            source1.Add(2);

            source2.Add(3);
            source2.Add(4);
            source2.Add(5);

            source3.Add(6);
            source3.Add(7);
            source3.Add(8);

            enumerator = new CompositeEnumerator<int>(source1.GetEnumerator(), source2.GetEnumerator(), source3.GetEnumerator());

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(9, output.Count);
            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(1, output[1]);
            Assert.AreEqual(2, output[2]);
            Assert.AreEqual(3, output[3]);
            Assert.AreEqual(4, output[4]);
            Assert.AreEqual(5, output[5]);
            Assert.AreEqual(6, output[6]);
            Assert.AreEqual(7, output[7]);
            Assert.AreEqual(8, output[8]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void CompositeEnumerator_EmptyEnumerators()
        {
            List<int> source1 = new List<int>();
            List<int> source2 = new List<int>();
            List<int> source3 = new List<int>();
            List<int> output = new List<int>();
            CompositeEnumerator<int> enumerator;

            // Empty enumerator at beginning

            source1.Clear();
            source2.Clear();
            source3.Clear();
            output.Clear();

            source2.Add(3);
            source2.Add(4);
            source2.Add(5);

            source3.Add(6);
            source3.Add(7);
            source3.Add(8);

            enumerator = new CompositeEnumerator<int>(source1.GetEnumerator(), source2.GetEnumerator(), source3.GetEnumerator());

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(6, output.Count);
            Assert.AreEqual(3, output[0]);
            Assert.AreEqual(4, output[1]);
            Assert.AreEqual(5, output[2]);
            Assert.AreEqual(6, output[3]);
            Assert.AreEqual(7, output[4]);
            Assert.AreEqual(8, output[5]);

            // Empty enumerator in middle

            source1.Clear();
            source2.Clear();
            source3.Clear();
            output.Clear();

            source1.Add(0);
            source1.Add(1);
            source1.Add(2);

            source3.Add(6);
            source3.Add(7);
            source3.Add(8);

            enumerator = new CompositeEnumerator<int>(source1.GetEnumerator(), source2.GetEnumerator(), source3.GetEnumerator());

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(6, output.Count);
            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(1, output[1]);
            Assert.AreEqual(2, output[2]);
            Assert.AreEqual(6, output[3]);
            Assert.AreEqual(7, output[4]);
            Assert.AreEqual(8, output[5]);

            // Empty enumerator at end

            source1.Clear();
            source2.Clear();
            source3.Clear();
            output.Clear();

            source1.Add(0);
            source1.Add(1);
            source1.Add(2);

            source2.Add(3);
            source2.Add(4);
            source2.Add(5);

            enumerator = new CompositeEnumerator<int>(source1.GetEnumerator(), source2.GetEnumerator(), source3.GetEnumerator());

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(6, output.Count);
            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(1, output[1]);
            Assert.AreEqual(2, output[2]);
            Assert.AreEqual(3, output[3]);
            Assert.AreEqual(4, output[4]);
            Assert.AreEqual(5, output[5]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void CompositeEnumerator_Reset()
        {
            List<int> source1 = new List<int>();
            List<int> source2 = new List<int>();
            List<int> source3 = new List<int>();
            List<int> output = new List<int>();
            CompositeEnumerator<int> enumerator;

            source1.Add(0);
            source1.Add(1);
            source1.Add(2);

            source2.Add(3);
            source2.Add(4);
            source2.Add(5);

            source3.Add(6);
            source3.Add(7);
            source3.Add(8);

            enumerator = new CompositeEnumerator<int>(source1.GetEnumerator(), source2.GetEnumerator(), source3.GetEnumerator());

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(9, output.Count);
            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(1, output[1]);
            Assert.AreEqual(2, output[2]);
            Assert.AreEqual(3, output[3]);
            Assert.AreEqual(4, output[4]);
            Assert.AreEqual(5, output[5]);
            Assert.AreEqual(6, output[6]);
            Assert.AreEqual(7, output[7]);
            Assert.AreEqual(8, output[8]);

            // Verify that we can reset the enumerator and rewalk the collections.

            output.Clear();
            enumerator.Reset();

            while (enumerator.MoveNext())
                output.Add(enumerator.Current);

            Assert.AreEqual(9, output.Count);
            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(1, output[1]);
            Assert.AreEqual(2, output[2]);
            Assert.AreEqual(3, output[3]);
            Assert.AreEqual(4, output[4]);
            Assert.AreEqual(5, output[5]);
            Assert.AreEqual(6, output[6]);
            Assert.AreEqual(7, output[7]);
            Assert.AreEqual(8, output[8]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void CompositeEnumerator_SourceChanged()
        {
            // Verify that InvalidOperationExceptions are thrown when the when 
            // performing operations on the enumerator after the source has
            // signalled that it has been modified.

            List<int> source = new List<int>();
            CompositeEnumerator<int> enumerator;

            source.Add(0);
            source.Add(1);
            source.Add(2);

            enumerator = new CompositeEnumerator<int>(source.GetEnumerator());
            enumerator.OnSourceChanged();
            ExtendedAssert.Throws<InvalidOperationException>(() => enumerator.MoveNext());

            enumerator = new CompositeEnumerator<int>(source.GetEnumerator());
            enumerator.OnSourceChanged();
            ExtendedAssert.Throws<InvalidOperationException>(() => { int i = enumerator.Current; });

            enumerator = new CompositeEnumerator<int>(source.GetEnumerator());
            enumerator.OnSourceChanged();
            ExtendedAssert.Throws<InvalidOperationException>(() => { enumerator.Reset(); });
        }
    }
}

