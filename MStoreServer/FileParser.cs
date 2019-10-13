using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MStoreServer
{
    public static class FileParser
    {
        /// <summary>
        /// Returns TRUE if all data is in this line, FALSE if empty data section
        /// </summary>
        /// <param name="line"></param>
        /// <param name="category"></param>
        /// <param name="data"></param>
        /// <param name="dataSeperationChar"></param>
        /// <returns>True if data is in this line, false if empty data section</returns>
        public static bool ParseOption(string line, out string category, out string data, char dataSeperationChar = ':')
        {
            line = line.Remove(0, 1);
            category = "";
            data = "";
            bool dataLine = false;

            bool quotationOpened = false;
            for(int i = 0;i<line.Length;i++)
            {
                if(line[i] == dataSeperationChar && quotationOpened == false)
                {
                    dataLine = true;
                    continue;
                }

                if(line[i] == '\"')
                {
                    quotationOpened = !quotationOpened;
                    continue;
                }

                if(line[i] == '#' && !quotationOpened)
                {
                    line = line.Remove(i);
                }

                if(!dataLine)
                {
                    category += line[i];
                }
                else
                {
                    data += line[i];
                }
            }

            quotationOpened = false;

            for(int i = 0;i<data.Length;i++)
            {
                //Console.WriteLine("Trying " + data[i].ToString());
                if(data[i] == '\"')
                {
                    quotationOpened = !quotationOpened;
                    continue;
                }


            }

            for(int i = 0;i<data.Length;i++)
            {
                if(data[i] != ' ')
                {
                    return true;
                }
            }

            return false;
        }



        private static bool FileParseExample(string configFilePath)
        {
            //games = new List<Game>();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Init");

            if (!File.Exists(configFilePath))
            {
                NetworkEngine.WriteError("Games config file on path \"" + configFilePath + "\" doesn't exist");
                return false;
            }

            string[] lines = File.ReadAllLines(configFilePath);

            int lastCat = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                switch (lines[i][0])
                {
                    case '+':
                        //games.Add(new Game("", games.Count));
                        lastCat = -1;
                        Console.WriteLine("Add new item");
                        break;
                    case '-':
                        string category = "";
                        string data = "";
                        bool result = FileParser.ParseOption(lines[i], out category, out data);

                        if (result)
                        {
                            switch (category)
                            {
                                case "example":
                                    //games[games.Count - 1].name = data;
                                    Console.WriteLine("Add example categories");
                                    break;
                                default:
                                    NetworkEngine.WriteError("Category " + category + " not found, error in file on line " + (i+1).ToString());
                                    return false;
                                    break;
                            }
                            lastCat = -1;
                        }
                        else
                        {
                            switch (category)
                            {
                                case "example2":
                                    //lastCat = 2;
                                    Console.WriteLine("Add example categories with data in next line");
                                    break;
                                default:
                                    NetworkEngine.WriteError("Category " + category + " not found, error in file on line " + (i+1).ToString());
                                    return false;
                                    break;
                            }
                        }
                        break;
                    default:
                        switch(lastCat)
                        {
                            case -1:
                                NetworkEngine.WriteError("Data without category ( " + lines[i] + " ) on line " + (i+1).ToString());
                                return false;
                                break;
                            default:
                                NetworkEngine.WriteError("Data without category ( " + lines[i] + " ) on line " + (i + 1).ToString());
                                return false;
                                break;
                            case 2:
                                Console.WriteLine("Some example addings");
                                //users[users.Count - 1].Add(new User(line));
                                break;
                        }
                        break;
                }
            }

            return true;
        }
    }
}
