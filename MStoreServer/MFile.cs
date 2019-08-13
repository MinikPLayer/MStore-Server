using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

using System.Security.Cryptography;

namespace MStoreServer
{
    public static class MFile
    {


        public static string[] ReadAllLines(string path, bool decrypt = true)
        {
            if(!decrypt)
            {
                return File.ReadAllLines(path);
            }

            /*string content = File.ReadAllText(path);

            byte[] contentByte = MUtil.StringToByteArray(content);*/

            byte[] contentByte = File.ReadAllBytes(path);
            if(contentByte.Length == 0)
            {
                return new string[0];
            }

            string content = MCrypt.DecryptByteArray(contentByte);

            return MUtil.StringToStringArray(content);
            
        }

        public static string ReadAllText(string path, bool decrypt = true)
        {
            if(!File.Exists(path))
            {
                Debug.LogError("File \"" + path + "\" doesn't exist");
                return null;
            }

            if(decrypt)
            {
                //byte[] array = File.ReadAllBytes(path);
                string line = File.ReadAllText(path);
                if(line.Length == 0)
                {
                    return line;
                }


                byte[] array = MUtil.StringToByteArray(line);

                return MCrypt.DecryptByteArray(array);
            }
            else
            {
                return File.ReadAllText(path);
            }
        }

        public static byte[] ReadAllBytes(string path, bool decrypt = true)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("File \"" + path + "\" doesn't exist");
                return null;
            }

            if (!decrypt)
            {
                return File.ReadAllBytes(path);
            }

            byte[] bytes = File.ReadAllBytes(path);
            if(bytes.Length == 0)
            {
                return bytes;
            }

            string line = MCrypt.DecryptByteArray(bytes);

            bytes = MUtil.StringToByteArray(line);

            return bytes;
        }

        public static void WriteAllText(string path, string content, bool encrypt = true)
        {
            if(!encrypt)
            {
                File.WriteAllText(path, content);
                return;
            }

           

            byte[] encrypted = MCrypt.EncryptString(content);

            //File.WriteAllBytes(path, encrypted);
            //string text = MUtil.ByteArrayToString(encrypted);
            //File.WriteAllText(path, text);
            File.WriteAllBytes(path, encrypted);
        }

        public static void WriteAllBytes(string path, byte[] content, bool encrypt = true)
        {
            if(!encrypt)
            {
                File.WriteAllBytes(path, content);
                return;
            }

            string line = MUtil.ByteArrayToString(content);

            content = MCrypt.EncryptString(line);

            File.WriteAllBytes(path, content);
        }

        public static void WriteAllLines(string path, string[] lines, bool encrypt = true)
        {
            if(!encrypt)
            {
                File.WriteAllLines(path, lines);
                return;
            }

            string content = "";
            for(int i = 0;i<lines.Length - 1;i++)
            {
                content += lines[i] + '\n';
            }
            content += lines[lines.Length - 1];

            WriteAllText(path, content);
        }

        public static void AppendAllText(string path, string text, bool encrypt = true)
        {
            if(!encrypt)
            {
                File.AppendAllText(path, text);
                return;
            }

            string content = ReadAllText(path);
            content += text;
            WriteAllText(path, content);
        }
    }

    
}
