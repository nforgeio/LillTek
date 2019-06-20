//-----------------------------------------------------------------------------
// FILE:        AssemblySigningKey.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the important public keys used to sign LillTek related assemblies.

using System;

namespace LillTek
{
    /// <summary>
    /// Holds the important public keys used to sign LillTek related assemblies.
    /// </summary>
	public static class AssemblySigningKey
	{
        /// <summary>
        /// The public key from the <b>\LillTek\Platform\PlatformKey.snk</b> key used
        /// to sign all LillTek related platform assemblies.
        /// </summary>
        public const string PlatformKey =
          "0024000004800000940000000602000000240000525341310004000001000100f9712b6af696e9"
        + "7e4d8b2157311f5f6c12f98888968dda88d86c30d36479bdf674c977391368cc809a551b6e3ea8"
        + "db5a2f091dc4825e992ba49f840b8192313bfb89d5ca260e93221a6ad2cfd049ea07a8cd9be3ab"
        + "aca0fcce4c639a48f154aa1d0afd3ed9316a24e59621d9171917e06e5915891742e21c3bb37242"
        + "602844e7";

        /// <summary>
        /// The public key from the <b>\LillTek\Platform\UnitTestKey.snk</b> key used
        /// to sign all LillTek related unit test assemblies.
        /// </summary>
        public const string UnitTestKey = 
            "0024000004800000940000000602000000240000525341310004000001000100c378d62888d1d4"
          + "368ea68472b659c22c1401c23e157242552253fbd7aa28863dc75fd4a8fee1629bdcc3b40ff5a0"
          + "d896c2c63655a2fec9702c5903fc91e24ea5571b123585ef90e1d0dd42e9e450b2a8431955cd2b"
          + "95c6fbb320f57a92c085999bed96cf221f90ee44b9f047e1e5f8ae810ec988814279897054297e"
          + "721c13a4";
	}
}