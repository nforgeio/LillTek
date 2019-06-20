//-----------------------------------------------------------------------------
// FILE:        _GenericResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;

using LillTek.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _GenericResult
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GenericResult_Result()
        {
            Assert.AreEqual(10, GenericResult<int>.GetOrThrow(new GenericResult<int>(10)));
            Assert.AreEqual("Hello World", GenericResult<string>.GetOrThrow(new GenericResult<string>("Hello World")));
        }

        private class MyException1 : Exception
        {
            public MyException1(string message)
                : base(message)
            {
            }
        }

        private class MyException2 : Exception
        {
            public MyException2(string message)
                : base(message)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GenericResult_Faults()
        {
            try
            {
                GenericResult.ClearExceptions();

                GenericResult<string> result;

                result = new GenericResult<string>(new MyException1("hello"));
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a RemoteException");
                }
                catch (RemoteException)
                {
                    // Expected
                }

                GenericResult.RegisterException(typeof(MyException1));

                result = new GenericResult<string>(new MyException1("hello"));
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a MyException1");
                }
                catch (MyException1)
                {
                    // Expected
                }

                result = new GenericResult<string>(new MyException2("hello"));
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a RemoteException");
                }
                catch (RemoteException)
                {
                    // Expected
                }

                GenericResult.RegisterCommonExceptions();

                result = new GenericResult<string>(new TimeoutException("hello"));
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a TimeoutException");
                }
                catch (TimeoutException)
                {
                    // Expected
                }

                result = new GenericResult<string>(new SecurityException("hello"));
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a SecurityException");
                }
                catch (SecurityException)
                {
                    // Expected
                }

                result = new GenericResult<string>(new NotImplementedException());
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a NotImplementedException");
                }
                catch (NotImplementedException)
                {
                    // Expected
                }

                result = new GenericResult<string>(new ArgumentException());
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a ArgumentException");
                }
                catch (ArgumentException)
                {
                    // Expected
                }

                result = new GenericResult<string>(new ArgumentNullException());
                try
                {
                    GenericResult<string>.GetOrThrow(result);
                    Assert.Fail("Expected a ArgumentNullException");
                }
                catch (ArgumentNullException)
                {
                    // Expected
                }
            }
            finally
            {
                GenericResult.ClearExceptions();
            }
        }
    }
}

