//-----------------------------------------------------------------------------
// FILE:        CryptoCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements cryptographic commands

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements cryptographic commands.
    /// </summary>
    public static class CryptoCommand
    {

        /// <summary>
        /// Executes the specified CRYPTO command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic crytpo genkey <algorithm> <keysize>

Generates an encryption key and writes the result to
standard output.  Symmetric algorithm keys and initialization
vectors will be written as a hex strings.  Private keys generated for
asymmetric algoirthms will be written encoded as XML.

    <algorithm>         Names the encryption algorithm the key is being
                        generated for.

                        Symmetric:  RC2, DES, TRIPLEDES, AES
                        Asymmetric: RSA

    <keysize>           The requested key size in bits.

-------------------------------------------------------------------------------
vegomatic crytpo encrypt -key:<symkey> [-salt:0|4|8] -in:<input> -:out:<output>

Encrypts a file using a symmetric key and optionally adding four
or eight bytes of cryptogrpahic salt.

    -key:<symkey>       The symmetric key algorithm, key, and
                        initialization vector expressed in the
                        form used by the SymmetricKey class.

    -salt:#             The optional parameter that indicates
                        whether 0, 4, or 8 bytes of salt
                        should be added before encrypting.
                        The default is 0 if this option is
                        not present.

    <input>             Path of the plaintext file.
    
    <output>            Path for generated encrypted output.

-------------------------------------------------------------------------------
vegomatic crytpo decrypt -key:<symkey> [-salt:0|4|8] -in:<input> -:out:<output>

Decrypts a file using a symmetric key and optionally removing four
or eight bytes of cryptogrpahic salt.

    -key:<symkey>       The symmetric key algorithm, key, and
                        initialization vector expressed in the
                        form used by the SymmetricKey class.

    -salt:#             The optional parameter that indicates
                        whether 0, 4, or 8 bytes of salt
                        should be added before encrypting.
                        The default is 0 if this option is
                        not present.

    <input>             Path of the encrypted file.
    
    <output>            Path for generated plaintext output.

-------------------------------------------------------------------------------
vegomatic crypto securefile encrypt -in:<input> -out:<output> -key:<rsakey>

Encrypts a file using a private RSA key into the LillTek SecureFile
format.

    -in:<input>         Path to the input plaintext file.

    -out:<output>       Path to the encrypted output file.

    -key:<rsakey>       The private RSA encryption key.

    -keychain:<path>[;<symkey>] Specifies a keychain file.

The private encryption key can be specified either by passing it in the 
-key:<rsakey> or as the first key in the keychain specified by the
-keychain option.

-keychain:<path>[;<symkey>] specifies the path to a keychain file.
This file can be dencrypted using a symmetric key if necessary.

-------------------------------------------------------------------------------
vegomatic crypto securefile decrypt -in:<input> -out:<output>
          -key:<rsakey> -keychain:<path>[;<symkey>]

Decrypts a LillTek SecureFile using private RSA keys from a keychain.

    -in:<input>         Path to the encrypted SecureFile file.

    -out:<output>       Path to the decrypted output file.

    -key:<rsakey>       Pass zero or more of these to specify the
                        private RSA keys forming the keychain.

    -keychain:<path>[;<symkey>] Specifies the optional keychain file.

This command requires that one or more private RSA key forming a
keychain be specified.  The command selects the correct key from the
chain to decrypt the file.  The keys can be specified on the command
line by using -key:<rsakey> command line options or by using the
-keychain.

The -keychain:<path>[;<symkey>] option specifies the path to a 
keychain file.  By default, this file will be read as text,
with each non-empty line not beginning with the comment markers
(""//"" or ""--"") loaded as private RSA key.  The keychain file
may also be an encrypted LillTek KeyChain file.  Specify the
optional symmetric key in this case.

-------------------------------------------------------------------------------
vegomatic crypto keychain encrypt -key:<symkey> -in:<input> -out:<output>

Creates a LillTek Keychain file from a plaintext input file.

    -key<symkey>        Symmetric key to be used to encrypt the output.

    -in:<path>          Path to the plaintext input file.

    -out:<path>         Path to the output KeyChain file.

Keys will be read from the input file.  Empty lines or lines beginning
with the comment markers (""//"" or ""--"") will be ignored.

-------------------------------------------------------------------------------
vegomatic crypto keychain decrypt -key:<symkey> -in:<input> -out:<output>

Decrypts a LillTek KeyChain file to a text file, with each RSA key being
written to a seperate line.

    -key<symkey>        Symmetric key to be used to decrypt the input.

    -in:<path>          Path to the encrypted keychain file.

    -out:<path>         Path to the output plaintext file.

";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "genkey":

                    if (args.Length != 3)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return GenKey(args[1], args[2]);

                case "encrypt":
                case "decrypt":

                    // $todo(jeff.lill): 
                    //
                    // I'm just going to perform the encrypt/decrypt
                    // operations in memory for now.

                    CommandLine     cmdArgs = new CommandLine(args, false);
                    string          keyStr  = cmdArgs.GetOption("key", null);
                    string          input   = cmdArgs.GetOption("in", null);
                    string          output  = cmdArgs.GetOption("out", null);
                    string          salt    = cmdArgs.GetOption("salt", "0");
                    SymmetricKey    key;
                    byte[]          buffer;

                    if (keyStr == null || input == null || output == null)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    key = new SymmetricKey(keyStr);
                    buffer = File.ReadAllBytes(input);

                    if (args[0].ToLowerInvariant() == "encrypt")
                    {
                        switch (salt)
                        {
                            case "0":

                                buffer = Crypto.Encrypt(buffer, key);
                                break;

                            case "4":

                                buffer = Crypto.EncryptWithSalt4(buffer, key);
                                break;

                            case "8":

                                buffer = Crypto.EncryptWithSalt8(buffer, key);
                                break;

                            default:

                                Program.Error(usage);
                                return 1;
                        }
                    }
                    else
                    {
                        // Decrypt

                        switch (salt)
                        {
                            case "0":

                                buffer = Crypto.Decrypt(buffer, key);
                                break;

                            case "4":

                                buffer = Crypto.DecryptWithSalt4(buffer, key);
                                break;

                            case "8":

                                buffer = Crypto.DecryptWithSalt8(buffer, key);
                                break;

                            default:

                                Program.Error(usage);
                                return 1;
                        }
                    }

                    File.WriteAllBytes(output, buffer);
                    return 0;

                case "securefile":

                    if (args.Length < 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    switch (args[1].ToLowerInvariant())
                    {
                        case "encrypt":

                            return EncryptSecureFile(args.Extract<string>(2));

                        case "decrypt":

                            return DecryptSecureFile(args.Extract<string>(2));
                    }

                    Program.Error(usage);
                    return 1;

                case "keychain":

                    if (args.Length < 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    switch (args[1].ToLowerInvariant())
                    {
                        case "encrypt":

                            return EncryptKeyChain(args.Extract<string>(2));

                        case "decrypt":

                            return DecryptKeyChain(args.Extract<string>(2));
                    }

                    Program.Error(usage);
                    return 1;

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int GenKey(string algorithm, string sKeySize)
        {
            int keySize;

            if (!int.TryParse(sKeySize, out keySize) || keySize <= 0)
            {
                Program.Output("key size must be >= 0");
                return 1;
            }

            switch (algorithm.ToUpper())
            {
                case CryptoAlgorithm.RC2:
                case CryptoAlgorithm.DES:
                case CryptoAlgorithm.TripleDES:
                case CryptoAlgorithm.AES:

                    SymmetricKey key = Crypto.GenerateSymmetricKey(algorithm, keySize);

                    Program.Output("");
                    Program.Output("SymKey {0}\r\n", key.ToString());
                    Program.Output("KEY:   {0}\r\n", Helper.ToHex(key.Key));
                    Program.Output("IV:    {0}\r\n", Helper.ToHex(key.IV));
                    return 0;

                case CryptoAlgorithm.RSA:

                    string privateKey;
                    string publicKey;

                    privateKey = AsymmetricCrypto.CreatePrivateKey(algorithm, keySize);
                    publicKey = AsymmetricCrypto.GetPublicKey(algorithm, privateKey);

                    Program.Output("");
                    Program.Output("PRIVATE KEY :\r\n\r\n{0}\r\n", privateKey);
                    Program.Output("PUBLIC KEY:\r\n\r\n{0}\r\n", publicKey);

                    return 0;

                default:

                    Program.Error("[{0}] is not a supported encryption algorithm.", algorithm);
                    return 1;
            }
        }

        private static int EncryptSecureFile(string[] args)
        {
            CommandLine     cmdLine        = new CommandLine(args, false);
            string          inPath         = cmdLine.GetOption("in", null);
            string          outPath        = cmdLine.GetOption("out", null);
            string          key            = cmdLine.GetOption("key", null);
            string          keyChainOption = cmdLine.GetOption("keychain", null);
            KeyChain        keyChain;

            if (inPath == null)
            {
                Program.Error("[-in:<path>] command line option is required.");
                return 1;
            }

            if (outPath == null)
            {
                Program.Error("[-out:<path>] command line option is required.");
                return 1;
            }

            if (key == null && keyChainOption != null)
            {
                string          keyPath;
                int             pos;
                SymmetricKey    symkey;

                pos = keyChainOption.IndexOf(';');
                if (pos != -1)
                {
                    // Keychain file is encrypted.

                    keyPath  = keyChainOption.Substring(0, pos);
                    symkey   = new SymmetricKey(keyChainOption.Substring(pos + 1));
                    keyChain = new KeyChain(symkey, File.ReadAllBytes(keyPath));
                }
                else
                {
                    // Keychain file is not encrypted.

                    keyChain = new KeyChain();

                    using (var input = new StreamReader(keyChainOption))
                    {
                        for (var line = input.ReadLine(); line != null; line = input.ReadLine())
                        {
                            var trimmed = line.Trim();

                            if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("--"))
                                continue;

                            keyChain.Add(trimmed);
                        }
                    }
                }

                if (keyChain.Count == 0)
                {
                    Program.Error("The keychain is empty.");
                    return 1;
                }

                if (keyChain.Count > 0)
                    key = keyChain.ToArray()[0];
            }

            if (key == null)
            {
                Program.Error("A private RSA key must be specified using a [-key] or [-keychain] option.");
                return 1;
            }

            using (var secureFile = new SecureFile(inPath, SecureFileMode.Encrypt, key))
            {
                secureFile.EncryptTo(outPath);
            }

            return 0;
        }

        private static int DecryptSecureFile(string[] args)
        {
            CommandLine     cmdLine        = new CommandLine(args, false);
            string          inPath         = cmdLine.GetOption("in", null);
            string          outPath        = cmdLine.GetOption("out", null);
            string          keyChainOption = cmdLine.GetOption("keychain", null);
            KeyChain        keyChain       = null;

            if (inPath == null)
            {
                Program.Error("[-in:<path>] command line option is required.");
                return 1;
            }

            if (outPath == null)
            {
                Program.Error("[-out:<path>] command line option is required.");
                return 1;
            }

            if (keyChainOption != null)
            {
                string          keyPath;
                int             pos;
                SymmetricKey    symkey;

                pos = keyChainOption.IndexOf(';');
                if (pos != -1)
                {
                    // Keychain file is encrypted.

                    keyPath  = keyChainOption.Substring(0, pos);
                    symkey   = new SymmetricKey(keyChainOption.Substring(pos + 1));
                    keyChain = new KeyChain(symkey, File.ReadAllBytes(keyPath));
                }
                else
                {
                    // Keychain file is not encrypted.

                    keyChain = new KeyChain();

                    using (var input = new StreamReader(keyChainOption))
                    {
                        for (var line = input.ReadLine(); line != null; line = input.ReadLine())
                        {
                            var trimmed = line.Trim();

                            if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("--"))
                                continue;

                            keyChain.Add(trimmed);
                        }
                    }
                }

                if (keyChain.Count == 0)
                {
                    Program.Error("The keychain is empty.");
                    return 1;
                }
            }
            else
                keyChain = new KeyChain();

            var keys = cmdLine.GetOptionValues("key");

            foreach (var key in keys)
                keyChain.Add(key);

            if (keyChain.Count == 0)
            {
                Program.Error("A private RSA key must be specified using a [-key] or [-keychain] option.");
                return 1;
            }

            using (var secureFile = new SecureFile(inPath, keyChain))
            {
                secureFile.DecryptTo(outPath);
            }

            return 0;
        }

        private static int EncryptKeyChain(string[] args)
        {

            CommandLine     cmdLine  = new CommandLine(args, false);
            string          inPath   = cmdLine.GetOption("in", null);
            string          outPath  = cmdLine.GetOption("out", null);
            string          key      = cmdLine.GetOption("key", null);
            KeyChain        keyChain = new KeyChain();

            if (inPath == null)
            {
                Program.Error("[-in:<path>] command line option is required.");
                return 1;
            }

            if (outPath == null)
            {
                Program.Error("[-out:<path>] command line option is required.");
                return 1;
            }

            if (key == null)
            {
                Program.Error("[-key:<symkey>] command line option is required.");
                return 1;
            }

            using (var input = new StreamReader(inPath))
            {
                for (var line = input.ReadLine(); line != null; line = input.ReadLine())
                {
                    var trimmed = line.Trim();

                    if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("--"))
                        continue;

                    keyChain.Add(trimmed);
                }
            }

            using (var output = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite))
            {
                var encrypted = keyChain.Encrypt(new SymmetricKey(key));

                output.Write(encrypted, 0, encrypted.Length);
            }

            return 0;
        }

        private static int DecryptKeyChain(string[] args)
        {
            CommandLine     cmdLine = new CommandLine(args, false);
            string          inPath  = cmdLine.GetOption("in", null);
            string          outPath = cmdLine.GetOption("out", null);
            string          key     = cmdLine.GetOption("key", null);
            KeyChain        keyChain;
            byte[]          encrypted;

            if (inPath == null)
            {
                Program.Error("[-in:<path>] command line option is required.");
                return 1;
            }

            if (outPath == null)
            {
                Program.Error("[-out:<path>] command line option is required.");
                return 1;
            }

            if (key == null)
            {
                Program.Error("[-key:<symkey>] command line option is required.");
                return 1;
            }

            using (var input = new FileStream(inPath, FileMode.Open, FileAccess.Read))
            {
                encrypted = new byte[(int)input.Length];
                input.Read(encrypted, 0, encrypted.Length);
            }

            keyChain = new KeyChain(new SymmetricKey(key), encrypted);

            using (var output = new StreamWriter(outPath, false, Helper.AnsiEncoding))
            {
                var keys = keyChain.ToArray();

                for (int i = 0; i < keys.Length; i++)
                    output.WriteLine(keys[i]);
            }

            return 0;
        }
    }
}
