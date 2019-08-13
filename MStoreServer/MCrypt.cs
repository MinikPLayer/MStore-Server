using System;
using System.Collections.Generic;
using System.Text;

using System.Security.Cryptography;

using System.IO;

namespace MStoreServer
{
    public static class MCrypt
    {

        public static byte[] lockFileIV = { 82, 100, 91, 154, 66, 194, 199, 194, 97, 164, 14, 74, 16, 236, 65, 18 };
        public static byte[] lockFileKey = { 59, 151, 82, 142, 68, 185, 62, 27, 224, 97, 101, 228, 119, 23, 210, 123, 202, 174, 174, 211, 229, 163, 168, 160, 124, 137, 136, 163, 74, 248, 106, 244 };

        public static byte[] filesIV;
        public static byte[] filesKey;

        public static string DecryptByteArray(byte[] input)
        {
            return DecryptByteArray(input, filesKey, filesIV);
        }

        public static string DecryptByteArray(byte[] input, byte[] Key, byte[] IV)
        {
            if (input == null || input.Length <= 0)
            {
                throw new ArgumentNullException("input");
            }
            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException("input");
            }
            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("input");
            }

            string decrypted = null;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                aesAlg.Mode = CipherMode.ECB;
                //aesAlg.Padding = PaddingMode.Zeros;
                aesAlg.BlockSize = 128;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream memoryStream = new MemoryStream(input))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(cryptoStream))
                        {
                            decrypted = streamReader.ReadToEnd();
                        }
                    }
                }

            }

            return decrypted;
        }

        public static byte[] EncryptString(string input)
        {
            return EncryptString(input, filesKey, filesIV);
        }

        public static byte[] EncryptString(string input, byte[] Key, byte[] IV)
        {
            if (input == null || input.Length <= 0)
            {
                throw new ArgumentNullException("input");
            }
            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException("input");
            }
            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("input");
            }

            

            byte[] encrypted;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                aesAlg.Mode = CipherMode.ECB;
                //aesAlg.Padding = PaddingMode.Zeros;
                aesAlg.BlockSize = 128;


                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(input);
                        }
                        encrypted = memoryStream.ToArray();
                    }
                }
            }

            return encrypted;
        }

        public static byte[] ParseLineToAESKey(string line, string strBetween = " ")
        {
            string actualText = "";
            List<byte> bytesList = new List<byte>();
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == strBetween[0])
                {
                    i += strBetween.Length - 1;

                    byte bt = 0;
                    if (!byte.TryParse(actualText, out bt))
                    {
                        Debug.LogError("Cannot parse " + actualText + " to bt ( byte )");
                        return null;
                    }

                    bytesList.Add(bt);

                    actualText = "";
                    continue;
                }

                actualText += line[i];
            }

            if (actualText.Length != 0)
            {
                byte bt = 0;
                if (!byte.TryParse(actualText, out bt))
                {
                    Debug.LogError("Cannot parse " + actualText + " to bt ( byte )");
                    return null;
                }

                bytesList.Add(bt);

                actualText = "";
            }



            return bytesList.ToArray();
        }
    }
}
