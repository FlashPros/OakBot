﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;

namespace OakBot.Model
{
    public static class BinaryFile
    {
        # region Fields

        // Fixed storage directory in appdata
        private static readonly string BinFilesPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData) + "\\OakBot\\Bin";

        // Cypher key and initial vector for encryption purposes
        // Make sure to change this in release versions > https://www.random.org/bytes/
        private static readonly byte[] CypherKey =
            new byte[16] { 0xcf, 0xc0, 0x8f, 0x9a, 0xec, 0xe0, 0xad, 0xc6, 0x8d, 0x36, 0x25, 0xb5, 0xa6, 0x78, 0x8c, 0xf0 };

        private static readonly byte[] CypherIV =
            new byte[16] { 0xfd, 0x2d, 0xff, 0x67, 0x48, 0x01, 0xd9, 0x94, 0x0c, 0xf7, 0x5c, 0x06, 0xbf, 0x3d, 0x7e, 0x59 };

        // Instance locking
        private static object _lock = new object();
        private static object _lockEnc = new object();

        #endregion

        #region Unencrypted Binary Files

        /// <summary>
        /// Write a serializable object to an unencrypted binary file.
        /// </summary>
        /// <param name="filename">Name of the file, without extention.</param>
        /// <param name="serializable">A serializable tagged object to store in a binary file.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool WriteBinFile(string filename, object serializable)
        {
            lock (_lock)
            {
                try
                {
                    // Initialize bin directory if needed
                    if (!Directory.Exists(BinFilesPath))
                        Directory.CreateDirectory(BinFilesPath);

                    // Binary serialize to file
                    using (FileStream fs = new FileStream($"{BinFilesPath}\\{filename}.bin",
                        FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        // Serialize .net object to binary
                        (new BinaryFormatter()).Serialize(fs, serializable);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Deserialize an unecrypted binary file to a serializable object.
        /// </summary>
        /// <param name="filename">Name of the file, without extention.</param>
        /// <returns>Deserialized object (requires casting), null on error or file not found.</returns>
        public static object ReadBinFile(string filename)
        {
            lock (_lock)
            {
                try
                {
                    // File does not exists, return null
                    if (!File.Exists($"{BinFilesPath}\\{filename}.bin"))
                        return null;

                    // Binary serialize to file
                    using (FileStream fs = new FileStream($"{BinFilesPath}\\{filename}.bin",
                        FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // Deserialize binary to .net object
                        return (new BinaryFormatter()).Deserialize(fs);
                    }
                }
                catch
                {
                    // not deserializable or other error
                    return null;
                }
            }
        }

        #endregion

        #region Encrypted Binary Files

        /// <summary>
        /// Write a serializable object to an encrypted binary file.
        /// </summary>
        /// <param name="filename">Name of the file, without extention.</param>
        /// <param name="serializable">A serializable tagged object to store in a binary file.</param>
        /// <returns></returns>
        public static bool WriteEncryptedBinFile(string filename, object serializable)
        {
            lock (_lockEnc)
            {
                // Initialize bin directory if needed
                if (!Directory.Exists(BinFilesPath))
                    Directory.CreateDirectory(BinFilesPath);

                // Initialize a symmetric algorithm and set Key and IV
                Rijndael cryptor = Rijndael.Create();
                cryptor.KeySize = 128;
                cryptor.Key = CypherKey;
                cryptor.IV = CypherIV;

                //DESCryptoServiceProvider cryptor = new DESCryptoServiceProvider()
                //{
                //    Key = CypherKey,
                //    IV = CypherIV
                //};

                try
                {
                    using (FileStream fs = new FileStream($"{BinFilesPath}\\{filename}.bin", FileMode.Create, FileAccess.Write, FileShare.None))
                    using (CryptoStream cs = new CryptoStream(fs, cryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        // Serialize .net object to binary
                        (new BinaryFormatter()).Serialize(cs, serializable);
                        //(new XmlSerializer(type)).Serialize(cs, serializable);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Deserialize an encrypted binary file to a serializable object.
        /// </summary>
        /// <param name="filename">Name of the file, without extention.</param>
        /// <returns>Deserialized object (requires casting), null on error or file not found.</returns>
        public static object ReadEncryptedBinFile(string filename)
        {
            lock (_lockEnc)
            {
                // File does not exists, return null
                if (!File.Exists($"{BinFilesPath}\\{filename}.bin"))
                    return null;

                // Init a symmetric algorithm and set Key and IV
                Rijndael cryptor = Rijndael.Create();
                cryptor.KeySize = 128;
                cryptor.Key = CypherKey;
                cryptor.IV = CypherIV;

                //DESCryptoServiceProvider cryptor = new DESCryptoServiceProvider()
                //{
                //    Key = CypherKey,
                //    IV = CypherIV
                //};

                try
                {
                    using (FileStream fs = new FileStream($"{BinFilesPath}\\{filename}.bin", FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (CryptoStream cs = new CryptoStream(fs, cryptor.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        // Deserialize binary to .net object
                        return (new BinaryFormatter()).Deserialize(cs);
                        //return (new XmlSerializer(type)).Deserialize(cs);
                    }

                }
                catch
                {
                    return null;
                }
            }
        }

        #endregion
    }
}
