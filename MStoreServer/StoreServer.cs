using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Globalization;

using System.Net.Sockets;

using System.Threading;

using System.IO.Compression;

using System.Security.Cryptography;

namespace MStoreServer
{
    public class StoreServer
    {
        private static List<User> users;
        private static List<Game> games;
        
        private static List<Voucher> vouchers;

        private static List<Group> groups;

        private static int defaultPermissionLevlID = -1;


        private NetworkEngine socket;

        public class Voucher
        {
            public string code;

            public long coinsAddon;
            public long gameID;

            public int usesLeft;

            public Voucher(string _code, long _coinsAddon, long _gameID, int _usesCount)
            {
                code = _code;
                coinsAddon = _coinsAddon;
                gameID = _gameID;

                usesLeft = _usesCount;
            }
        }

        public struct GroupPrice
        {
            public Price price;
            public Group group;

            public GroupPrice(Price _price, Group _group)
            {
                price = _price;
                group = _group;
            }

            public GroupPrice(long coins, Group _group)
            {
                price = new Price(coins);
                group = _group;
            }
        }

        public struct Price
        {
            private Int64 coins;

            /// <summary>
            /// 1 coin = ~1cent
            /// </summary>
            /// <param name="_coins"></param>
            public Price(Int64 _coins)
            {
                coins = _coins;
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public struct Currency
            {
                private decimal coinsPerOne;
                public string endStr;
                public Currency(decimal _coinsPerOne, string _endStr = "")
                {
                    coinsPerOne = _coinsPerOne;
                    endStr = _endStr;
                }

                public decimal GetValue(Int64 coins)
                {                    
                    return decimal.Round(coins / coinsPerOne, 2, MidpointRounding.AwayFromZero);
                }

                public Int64 GetCoins(decimal currencyCount)
                {
                    return (Int64)(currencyCount * coinsPerOne);
                }

                public static Currency euro = new Currency(100, " Euro");
                public static Currency zloty = new Currency((decimal)(100 / 4.29), "zl");
                public static Currency coins = new Currency(1, " coins");
            }

            public override string ToString()
            {
                Debug.LogWarning("Using default currency ( coins ) to convert price to string");
                return ToString(Price.Currency.coins);
            }

            public string ToString(User user)
            {
                return GetPriceStr(GetUserCurrency(user));
            }

            public string ToString(Currency currency)
            {
                return GetPriceStr(currency);
            }

            public string GetPriceStr(Currency currency)
            {
                if (this == Price.free) return "Free";
                return GetPrice(currency).ToString() + currency.endStr;
            }

            public decimal GetPrice(Currency currency)
            {
                return currency.GetValue(coins);
            }

            public void SetPrice(Currency currency, decimal price)
            {
                coins = currency.GetCoins(price);
            }

            public static Price free = new Price(0);

            public static implicit operator Price(long coinsNumber)
            {
                return new Price(coinsNumber);
            }

            public static bool operator==(Price price1, Price price2)
            {
                if(price1.coins == price2.coins)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public static bool operator!=(Price price1, Price price2)
            {
                if (price1.coins == price2.coins)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            public static Price operator+(Price price1, long addon)
            {
                price1.coins += addon;

                return price1;
            }

            public static Price operator -(Price price1, long addon)
            {
                price1.coins -= addon;

                return price1;
            }

            public static bool operator <(Price price1, Price price2)
            {
                return price1.coins < price2.coins;
            }

            public static bool operator >(Price price1, Price price2)
            {
                return price1.coins > price2.coins;
            }
        }

        public class Game
        {
            public string name = "\0";
            public Int64 id = -1;

            public string path = "\0";
            public string filename = "\0";

            public string execName = "\0";

            public string iconPath = "\0";

            public int version = -1;

            public Size diskSize = new Size(-1);
            public Size downloadSize = new Size(-1);

            public Price price;

            public List<GroupPrice> groupPrices = new List<GroupPrice>();


            public Game(string _name = "\0", Int64 _id = -1, string _path = "\0", Price _price = default(Price), string _filename = "\0", string _iconPath = "\0", int _version = -1)
            {
                name = _name;
                id = _id;
                path = _path;
                price = _price;

                filename = _filename;
                iconPath = _iconPath;

                version = _version;
            }

            public struct Size
            {
                public long bytes;

                public Size(long _bytes)
                {
                    bytes = _bytes;
                }

                static string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
                public override string ToString()
                {
                    int level = 0;
                    long __bytes = bytes;

                    while (__bytes > 1024)
                    {
                        __bytes /= 1024;
                        level++;
                    }

                    if (level >= sizeSuffixes.Length)
                    {
                        return bytes.ToString();
                    }

                    return __bytes.ToString() + sizeSuffixes[level];


                }

                public static implicit operator Size(long data)
                {
                    return new Size(data);
                }

            }
        }


        public class User
        {
            public NetworkEngine.Client socket = null;
            public List<Game> games;

            public string userName = "\0";
            public string password = "\0";

            public string token = "\0";

            public Group permissionLevel;

            public Int64 id = -1;

            public Price coins = new Price(-1);

            private bool workingOnGames = false;
            public void AddGame(Game game)
            {
                while(workingOnGames)
                {
                    Thread.Sleep(10);
                }

                workingOnGames = true;

                games.Add(game);

                workingOnGames = false;
            }

            public User(Int64 _id, string _userName, string _password, List<Game> _games, string _token, NetworkEngine.Client _socket = null, Int64 _coins = 0, Group _permissionLevel = null)
            {
                games = _games;
                id = _id;
                userName = _userName;
                password = _password;
                games = _games;
                token = _token;
                socket = _socket;
                coins.SetPrice(Price.Currency.coins, _coins);
                permissionLevel = _permissionLevel;

                if(_permissionLevel == null)
                {
                    permissionLevel = FindGroup(defaultPermissionLevlID);
                    if(permissionLevel == null)
                    {
                        Debug.LogError("Cannot find default permission ID");
                        return;
                    }
                }
            }
        }

        public class Group
        {
            public int id;
            public string name;

            public bool isAdmin = false;

            public Group(int _id, string _name, bool _isAdmin)
            {
                id = _id;
                name = _name;

                isAdmin = _isAdmin;
            }
        }

        public struct UserCredentials
        {
            public string login;
            public string password;

            public UserCredentials(string userLogin, string userPass)
            {
                login = userLogin;
                password = userPass;
            }
        }

        public static bool CheckIfUserHaveGame(User user, Game game)
        {
            if(user == null || game == null)
            {
                return false;
            }

            for(int i = 0;i<user.games.Count;i++)
            {
                if(user.games[i].id == game.id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates string from user library
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public string CreateUserLibraryString(User client)
        {
            if(client == null)
            {
                Debug.LogError("User is null");
                return "NF";
            }

            string dataToSend = "";
            for(int i = 0;i<client.games.Count;i++)
            {
                dataToSend += client.games[i].id.ToString();
                dataToSend += "\n";
            }

            return dataToSend;
                
        }

        /// <summary>
        /// Checks if char is a number
        /// </summary>
        /// <param name="character"></param>
        /// <returns></returns>
        public static bool IsNumber(char character)
        {
            if (character >= 48 && character <= 57) return true;
            return false;
        }

        /// <summary>
        /// Converts game info to string
        /// </summary>
        /// <param name="game"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public string ParseGameInfo(Game game, User user)
        {
            if (user == null) Debug.LogWarning("User is null");
            string info = "";

            info += game.id;
            info += "\n";
            info += game.name;
            info += "\n";
            info += game.price.GetPriceStr(GetUserCurrency(user));
            info += "\n";
            info += game.path;
            info += "\n";
            info += game.filename;
            info += "\n";
            info += game.execName;
            info += "\n";

            //Size
            info += game.downloadSize.bytes.ToString();
            info += "\n";
            info += game.diskSize.bytes.ToString();
            info += "\n";

            //Version
            info += game.version.ToString();
            info += "\n";
            

            return info;
            
        }

        /// <summary>
        /// Converts user info to string
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public string ParseUserInfo(User user)
        {
            string info = "";

            info += user.id.ToString() + '\n';
            info += user.token + '\n';
            info += user.userName + '\n';
            info += user.coins.ToString(user) + '\n';

            
            //info += user.games.Count.ToString() + '\n';

            return info;
        }

        public string ParseVoucherInfo(Voucher voucher)
        {

            // Game voucher
            if(voucher.gameID >= 0)
            {
                return voucher.code + ":" + voucher.gameID + ";" + voucher.usesLeft.ToString();
            }
            else // Coins voucher
            {
                return voucher.code + ":c" + voucher.coinsAddon + ";" + voucher.usesLeft.ToString();
            }

            
        }

        private bool updatingVoucherFile = false;
        public void UpdateVoucherFile(Voucher voucherCode)
        {
            if(!File.Exists(voucherConfigDir))
            {
                Debug.LogError("Voucher config dir doesn't exist");
                return;
            }

            while(updatingVoucherFile)
            {
                Thread.Sleep(10);
            }

            updatingVoucherFile = true;


            bool updated = false;
            string[] lines = MFile.ReadAllLines(voucherConfigDir);
            for(int i = 0;i<lines.Length;i++)
            {
                if(lines[i].StartsWith(voucherCode.code))
                {
                    updated = true;

                    if (voucherCode.usesLeft < -1)
                    {
                        voucherCode.usesLeft = -1;
                    }

                    if(voucherCode.usesLeft == 0)
                    {
                        lines[i] = "";
                        break;
                    }

                    lines[i] = voucherCode.code + ":";
                    if(voucherCode.gameID != -1)
                    {
                        lines[i] += voucherCode.gameID.ToString();
                    }
                    else
                    {
                        
                        lines[i] += 'c' + voucherCode.coinsAddon.ToString();
                    }
                    lines[i] += ";" + voucherCode.usesLeft.ToString();
                   
                    break;
                }
            }
            if(!updated)
            {
                lines[lines.Length - 1] += "\n" + ParseVoucherInfo(voucherCode);
            }

            MFile.WriteAllLines(voucherConfigDir, lines);

            updatingVoucherFile = false;
        }

        public string ParseStoreGameInfo(Game game, User user = null)
        {
            string info = "";
            info += game.id + "\n";
            info += game.name + "\n";
            if(user == null)
            {
                info += game.price.GetPriceStr(Price.Currency.coins) + "\n";
            }
            info += game.price.GetPriceStr(GetUserCurrency(user)) + "\n";

            return info;
        }


        /*public bool UseVoucher(string code, User user, out Voucher voucherUsed)
        {
            for(int i = 0;i<vouchers.Count;i++)
            {
                if(vouchers[i].code == code)
                {

                    if (vouchers[i].gameID != -1)
                    {
                        Game game = FindGame(vouchers[i].gameID);
                        if(game == null)
                        {
                            Debug.LogError("Voucher for not existing game");
                            voucherUsed = new Voucher("", -1, -1, -1);

                            if (vouchers[i].usesLeft <= 1)
                            {
                                vouchers.RemoveAt(i);
                            }
                            else
                            {
                                vouchers[i] = new Voucher(vouchers[i].code, vouchers[i].coinsAddon, vouchers[i].gameID, vouchers[i].usesLeft - 1);
                            }
                            return false;
                        }

                        user.games.Add(game);
                        voucherUsed = new Voucher(vouchers[i].code, vouchers[i].coinsAddon, vouchers[i].gameID, vouchers[i].usesLeft - 1);

                        if (vouchers[i].usesLeft <= 1)
                        {
                            vouchers.RemoveAt(i);
                        }
                        else
                        {
                            vouchers[i] = new Voucher(vouchers[i].code, vouchers[i].coinsAddon, vouchers[i].gameID, vouchers[i].usesLeft - 1);
                        }
                        return true;
                    }
                    else
                    {
                        user.coins += vouchers[i].coinsAddon;
                        voucherUsed = new Voucher(vouchers[i].code, vouchers[i].coinsAddon, vouchers[i].gameID, vouchers[i].usesLeft - 1);

                        if (vouchers[i].usesLeft <= 1)
                        {
                            vouchers.RemoveAt(i);
                        }
                        else
                        {
                            vouchers[i] = new Voucher(vouchers[i].code, vouchers[i].coinsAddon, vouchers[i].gameID, vouchers[i].usesLeft - 1);
                        }
                        return true;
                    }
                }
            }

            voucherUsed = new Voucher("", -1, -1, -1);

            return false;
        }*/

        public bool UseVoucher(User user, Voucher voucher)
        {
            if (voucher.gameID != -1)
            {
                Game game = FindGame(voucher.gameID);
                if (game == null)
                {
                    Debug.LogError("Voucher for not existing game");
                    //voucherUsed = new Voucher("", -1, -1, -1);

                    /*if (voucher.usesLeft <= 1)
                    {
                        vouchers.Remove(voucher);
                    }
                    else
                    {
                        voucher.usesLeft--;
                    }*/
                    return false;
                }

                user.games.Add(game);
                if (voucher.usesLeft == -1) return true;
                voucher.usesLeft--;

                if (voucher.usesLeft <= 1)
                {
                    //vouchers.Remove(voucher);
                    RemoveVoucher(voucher);
                }
                else
                {
                    voucher.usesLeft--;
                }
                return true;
            }
            else
            {
                user.coins += voucher.coinsAddon;
                if (voucher.usesLeft == -1) return true;
                voucher.usesLeft--;

                if (voucher.usesLeft <= 1)
                {
                    //vouchers.Remove(voucher);
                    RemoveVoucher(voucher);
                }
                else
                {
                    voucher.usesLeft--;
                }
                return true;
            }
        }

        /*public bool CheckVoucher(string code)
        {
            Voucher notUsed;
            return CheckVoucher(code, out notUsed);
        }*/

        public Voucher FindVoucher(string code)
        {
            for(int i = 0;i<vouchers.Count;i++)
            {
                if(vouchers[i].code == code)
                {
                    return vouchers[i];
                }
            }


            return null;
        }

        /// <summary>
        /// Reacts to data from user
        /// </summary>
        /// <param name="data"></param>
        /// <param name="client"></param>
        public void ClientDataReceived(string data, NetworkEngine.Client client)
        {
            

            Console.WriteLine("Data received: " + data);
            string command = data;
            if (data.Length > 5)
            {
                command = data.Remove(5);
            }
            
            data = data.Remove(0, 5);
            User user = FindUser(client);
            switch (command)
            {
                //User info
                case "URNFO":
                    {
                        Debug.Log("User info requested");
                        if (user == null)
                        {
                            Send(client, "User not found", "ERROR");
                            break;
                        }
                        Send(client, ParseUserInfo(user), "URNFO");
                        break;
                    }

                //Request library
                case "RQLBR":
                    {
                        Debug.Log("Library requested");
                        Send(client, CreateUserLibraryString(FindUser(client)), "RQLBR");
                        break;
                    }


                //Game info
                case "GMNFO":
                    {
                        if (data.Length == 0)
                        {
                            Debug.LogError("Empty game id");
                            Send(client, "Empty game id", "ERROR");
                            break;
                        }
                        Int64 gameID = 0;
                        if (Int64.TryParse(data, out gameID))
                        {
                            Game requestedGame = FindGame(gameID);

                            if (requestedGame == null)
                            {
                                Debug.LogError("Game not found");
                                Send(client, "Game not found", "ERROR");
                                break;
                            }
                            Debug.Log("Sending info for the game " + requestedGame.id);
                            Send(client, ParseGameInfo(requestedGame, FindUser(client)), "GMNFO");
                        }
                        else
                        {
                            Debug.LogError("Cannot parse game id");
                            Send(client, "Game id is invalid", "ERROR");
                            break;
                        }
                        break;
                    }

                    
                // Store games list
                case "SGLST":
                    {
                        if(user == null)
                        {
                            Send(client, "NA", "ERROR");
                            return;
                        }

                        Price maximumPrice = new Price(-1);
                        Price minimumPrice = new Price(-1);
                        string gameNameFilter = "";

                        string actualFilter = "";
                        for(int i = 0;i<data.Length;i++)
                        {
                            if(data[i] == '\n')
                            {
                                if(actualFilter.Length == 0)
                                {
                                    Send(client, "BF", "ERROR");
                                    return;
                                }

                                char filterChar = actualFilter[0];
                                actualFilter = actualFilter.Remove(0, 1);
                                if (actualFilter.Length == 0)
                                {
                                    Send(client, "BF", "ERROR");
                                    return;
                                }

                                switch (filterChar)
                                {
                                    //Max
                                    case 'M':
                                        {
                                            int value = 0;
                                            if(!int.TryParse(actualFilter, out value))
                                            {
                                                Send(client, "BF", "ERROR");
                                                return;
                                            }

                                            maximumPrice.SetPrice(GetUserCurrency(user), value);

                                            break;
                                        }

                                    //Min
                                    case 'm':
                                        {
                                            int value = 0;
                                            if (!int.TryParse(actualFilter, out value))
                                            {
                                                Send(client, "BF", "ERROR");
                                                return;
                                            }

                                            minimumPrice.SetPrice(GetUserCurrency(user), value);

                                            break;
                                        }

                                    //Search
                                    case 'S':
                                        {
                                            gameNameFilter = actualFilter.ToLower();
                                            break;
                                        }

                                    default:
                                        //Bad filter
                                        Send(client, "BF", "ERROR");
                                        return;
                                }


                                actualFilter = "";
                                continue;
                            }

                            actualFilter += data[i];
                        }

                        string dataToSend = "";

                        //Adding all games info - to be corrected
                        for(int i = 0;i<games.Count;i++)
                        {
                            if(minimumPrice.GetPrice(Price.Currency.coins) > 0)
                            {
                                if(games[i].price < minimumPrice)
                                {
                                    continue;
                                }
                            }

                            if (maximumPrice.GetPrice(Price.Currency.coins) > 0)
                            {
                                if (games[i].price > maximumPrice)
                                {
                                    continue;
                                }
                            }

                            if(gameNameFilter.Length != 0)
                            {
                                if (!games[i].name.ToLower().Contains(gameNameFilter))
                                {
                                    continue;
                                }
                            }

                            bool userHasGame = false;
                            //Checking if user already has game
                            for(int j = 0;j<user.games.Count;j++)
                            {
                                if(user.games[j].id == games[i].id)
                                {
                                    userHasGame = true;
                                    break;
                                }
                                

                                
                            }
                            if (userHasGame) continue;
                            dataToSend += ParseStoreGameInfo(games[i], user) + "\r";
                        }

                        if(dataToSend.Length == 0)
                        {
                            dataToSend = "\0";
                        }
                        Send(client, dataToSend, "SGLST");

                        break;
                    }
                case "BGAME":
                    {
                        if(user == null)
                        {
                            Send(client, "NA", "ERROR");
                            return;
                        }

                        Int64 _id = 0;
                        if(!Int64.TryParse(data, out _id))
                        {
                            Send(client, "NF", "ERROR");
                            return;
                        }

                        Game game = FindGame(_id);
                        if(game == null)
                        {
                            Send(client, "NF", "ERROR");
                            return;
                        }

                        for(int i = 0;i<user.games.Count;i++)
                        {
                            if(user.games[i].id == _id)
                            {
                                //Already bought
                                Send(client, "AB", "ERROR");
                                return;
                            }
                        }

                        if(user.coins < game.price)
                        {
                            Send(client, "TP", "ERROR");
                            return;
                        }

                        // Okay, so user is buying the game
                        user.coins -= (long)game.price.GetPrice(Price.Currency.coins);
                        user.games.Add(game);

                        UpdateUserValuesInFile(user);

                        Send(client, "OK", "BGAME");

                        break;
                    }
                case "VCHER":
                    {
                        
                        if(user == null)
                        {
                            Send(client, "NA", "ERROR");
                            return;
                        }

                        if(data.Length == 0)
                        {
                            //Bad argument
                            Send(client, "BA", "ERROR");
                            return;
                        }

                        Voucher v;

                        v = FindVoucher(data);

                        if(v == null)
                        {
                            Send(client, "BV", "ERROR");
                            return;
                        }

                        if(v.gameID > 0)
                        {
                            Game gm = FindGame(v.gameID);
                            if(gm == null)
                            {
                                Send(client, "NF", "ERROR");
                                return;
                            }

                            if(CheckIfUserHaveGame(user, gm))
                            {
                                //Already bought
                                Send(client, "AB", "ERROR");
                                return;
                            }
                        }


                        Voucher used = FindVoucher(data);
                        if(used == null)
                        {
                            Debug.LogError("Cannot find voucher with code \"" + data + "\"");
                            return;
                        }

                        bool useResult = UseVoucher(user, used);
                        if(useResult == false)
                        {
                            Send(client, "NF", "ERROR");
                            return;
                        }

                        UpdateUserValuesInFile(user);
                        UpdateVoucherFile(used);

                        Send(client, "OK", "VCHER");

                        break;
                    }


                // Admin commands
                case "ADMIN":
                    {
                        if(!user.permissionLevel.isAdmin)
                        {
                            Send(client, "NA", "ERROR");
                            return;
                        }

                        if (data.Length == 0)
                        {
                            Send(client, "BA", "ERROR");
                            return;
                        }

                        string workingString = "";
                        data = MUtil.GetStringToSpecialCharAndDelete(data, ';', out workingString);

                        if (data.Length == 0 && workingString.Length < 5)
                        {
                            Send(client, "BA", "ERROR");
                            return;
                        }

                        string cmdType = "";
                        string cmdTypeArg = "";

                        cmdType = workingString.Substring(0, 3);
                        cmdTypeArg = workingString.Substring(3, 2);

                        // Admin command
                        workingString = workingString.Remove(0, 5);

                        bool dataIsNumber = true;
                        long userID = -1;

                        if(!long.TryParse(workingString, out userID))
                        {
                            //Debug.LogError("Cannot convert \"" + workingString + "\" to targetID ( long )");
                            //Send(client, "BC", "ERROR");
                            //return;
                            dataIsNumber = false;
                        }




                        
                        /*if(data.Length < 5)
                        {
                            Debug.LogError("No admin command sent");
                            Send(client, "BC", "ERROR");
                            return;
                        }*/
                        
                        /*for(int i = 0;i<3;i++)
                        {
                            cmdType += data[i];
                        }
                        for(int i = 3;i<5;i++)
                        {
                            cmdTypeArg += data[i];
                        }*/

                        //data = data.Remove(0, 5);

                        switch (cmdType)
                        {
                            case "VCH":
                                {
                                    string voucherCode = workingString;
                                    int uses = -1;
                                    long gameID = -1;
                                    long coins = -1;

                                    Voucher voucher = FindVoucher(voucherCode);


                                    string pomStr = "";
                                    if (data.Length > 0)
                                    {
                                        
                                        data = MUtil.GetStringToSpecialCharAndDelete(data, ';', out pomStr);

                                        if (!int.TryParse(pomStr, out uses))
                                        {
                                            Debug.LogError("Cannot parse \"" + pomStr + "\" to uses ( int )");
                                            Send(client, "BC", "ERROR");
                                            return;
                                        }
                                    }

                                    if (data.Length > 0)
                                    {

                                        data = MUtil.GetStringToSpecialCharAndDelete(data, ';', out pomStr);

                                        bool coinsVoucher = false;
                                        if(pomStr[0] == 'C' || pomStr[0] == 'c')
                                        {
                                            coinsVoucher = true;
                                            pomStr = pomStr.Remove(0, 1);
                                        }

                                        long number = -1;

                                        if (!long.TryParse(pomStr, out number))
                                        {
                                            Debug.LogError("Cannot parse \"" + pomStr + "\" to gameID ( long )");
                                            Send(client, "BC", "ERROR");
                                            return;
                                        }

                                        if(coinsVoucher)
                                        {
                                            coins = number;
                                        }
                                        else
                                        {
                                            gameID = number;
                                        }
                                    }


                                    

                                    switch (cmdTypeArg)
                                    {
                                        
                                        // Add
                                        case "AD":
                                            {
                                                if (voucher != null)
                                                {
                                                    Debug.LogError("Voucher \"" + voucherCode + "\" already exist");
                                                    Send(client, "VE", "ERROR");
                                                    return;
                                                }

                                                if (gameID < 0 && coins < 0)
                                                {
                                                    Debug.LogError("No gameID and coins number specified!");
                                                    //Bad argument
                                                    Send(client, "BA", "ERROR");
                                                    return;
                                                }

                                                Voucher targetVoucher = new Voucher(voucherCode, coins, gameID, uses);

                                                //vouchers.Add(targetVoucher);
                                                AddVoucher(targetVoucher);
                                                UpdateVoucherFile(targetVoucher);

                                                Send(client, "OK", "VCHAD");

                                                break;
                                            }

                                        // Delete
                                        case "DL":
                                            {
                                                if(voucher == null)
                                                {
                                                    Debug.LogError("Voucher \"" + voucherCode + "\" doesn't exist");
                                                    Send(client, "VE", "ERROR");
                                                    return;
                                                }

                                                RemoveVoucher(voucher);
                                                voucher.usesLeft = 0;
                                                UpdateVoucherFile(voucher);

                                                Send(client, "OK", "VCHDL");
                                                break;
                                            }

                                        // Break
                                        case "CH":
                                            {
                                                if(voucher == null)
                                                {
                                                    Debug.LogError("Voucher \"" + voucherCode + "\" doesn't exist");
                                                    Send(client, "VE", "ERROR");
                                                    return;
                                                }

                                                // Uses - 1st argument ( type of value to change ), gameID - 2nd arg ( new Value )

                                                switch (uses)
                                                {
                                                    // Uses
                                                    case 0:
                                                        {
                                                            

                                                            voucher.usesLeft = (int)gameID;
                                                            Debug.Log("[ADMIN] New uses left: " + voucher.usesLeft);
                                                            Send(client, "OK0", "VCHCH");
                                                            break;
                                                        }

                                                    // Coins
                                                    case 1:
                                                        {
                                                            voucher.coinsAddon = gameID;
                                                            Debug.Log("[ADMIN] New coins addon: " + voucher.coinsAddon);
                                                            Send(client, "OK1", "VCHCH");
                                                            break;
                                                        }

                                                    case 2:
                                                        {
                                                            voucher.gameID = gameID;
                                                            Debug.Log("[ADMIN] New gameID: " + voucher.gameID);
                                                            Send(client, "OK2", "VCHCH");
                                                            break;
                                                        }

                                                    default:
                                                        Debug.LogError("Unknown change command");
                                                        Send(client, "CC", "ERROR");
                                                        return;
                                                }

                                                UpdateVoucherFile(voucher);

                                                break;
                                            }

                                        // List
                                        case "LS":
                                            {
                                                string dataToSend = "";
                                                for(int i = 0;i<vouchers.Count;i++)
                                                {
                                                    dataToSend += ParseVoucherInfo(vouchers[i]) + "\n";
                                                }

                                                Send(client, dataToSend, "VCHLS");

                                                break;
                                            }

                                        // Info
                                        case "NF":
                                            {
                                                if(voucher == null)
                                                {
                                                    Debug.LogError("Voucher not found");
                                                    Send(client, "NF", "ERROR");
                                                    return;
                                                }

                                                string voucherInfo = ParseVoucherInfo(voucher);
                                                Send(client, voucherInfo, "VCHNF");

                                                break;
                                            }

                                        default:
                                            Debug.LogError("Don't know what to do with VCH arg: \"" + cmdTypeArg + "\"");
                                            Send(client, "BT", "ERROR");
                                            return;
                                    }

                                    break;
                                }

                            // Money
                            case "MNY":
                                {
                                    if(!dataIsNumber)
                                    {
                                        Debug.LogError("Cannot convert \"" + workingString + "\" to userID ( long )");
                                        Send(client, "BC", "ERROR");
                                        return;
                                    }
                                    User targetUser = FindUser(userID);

                                    if (targetUser == null)
                                    {
                                        Send(client, "NF", "ERROR");
                                        return;
                                    }

                                    decimal val = -1;
                                    if (!decimal.TryParse(data, out val))
                                    {
                                        Debug.LogError("Cannot convert \"" + data + "\" to value ( decimal )");
                                        Send(client, "BC", "ERROR");
                                        return;
                                    }

                                    switch (cmdTypeArg)
                                    {
                                        // Change
                                        case "CH":
                                            {
                                                targetUser.coins.SetPrice(GetUserCurrency(targetUser), val);

                                                Send(client, "OK", "MNYCH");
                                                break;
                                            }
                                        // Add
                                        case "AD":
                                            {
                                                targetUser.coins.SetPrice(GetUserCurrency(targetUser), targetUser.coins.GetPrice(Price.Currency.coins) + val);

                                                Send(client, "OK", "MNYAD");

                                                Debug.Log("[ADMIN] Money added");
                                                break;
                                            }
                                        // Substract
                                        case "SB":
                                            {
                                                targetUser.coins.SetPrice(GetUserCurrency(targetUser), targetUser.coins.GetPrice(Price.Currency.coins) - val);
                                                if(targetUser.coins.GetPrice(Price.Currency.coins) < 0)
                                                {
                                                    targetUser.coins.SetPrice(Price.Currency.coins, 0);
                                                }

                                                Send(client, "OK", "MNYSB");
                                                break;
                                            }

                                        default:
                                            Debug.LogError("Don't know what to do with MNY arg: \"" + cmdTypeArg + "\"");
                                            Send(client, "BT", "ERROR");
                                            return;
                                    }

                                    UpdateUserValuesInFile(targetUser);

                                    break;
                                }

                            case "USR":
                                {

                                    if (!dataIsNumber)
                                    {
                                        Debug.LogError("Cannot convert \"" + workingString + "\" to userID ( long )");
                                        Send(client, "BC", "ERROR");
                                        return;
                                    }
                                    User targetUser = FindUser(userID);

                                    if (targetUser == null)
                                    {
                                        Send(client, "NF", "ERROR");
                                        return;
                                    }

                                    switch (cmdTypeArg)
                                    {
                                        // Info
                                        case "NF":
                                            {
                                                string usrInfoStr = ParseUserInfo(targetUser);
                                                Send(client, usrInfoStr, "USRNF");

                                                break;
                                            }

                                        // User ID
                                        case "ID":
                                            {
                                                string userName = data;
                                                targetUser = FindUser(userName);
                                                if(targetUser == null)
                                                {
                                                    Debug.LogError("Cannot find user \"" + userName + "\"");
                                                    Send(client, "NF", "ERROR");
                                                    return;
                                                }

                                                Send(client, targetUser.id.ToString(), "USRID");

                                                break;
                                            }

                                        default:
                                            Debug.LogError("Don't know what to do with MNY arg: \"" + cmdTypeArg + "\"");
                                            Send(client, "BT", "ERROR");
                                            return;
                                    }

                                    break;
                                }

                            // User Game
                            case "UGM":
                                {
                                    string usIDStr = "";
                                    workingString = MUtil.GetStringToSpecialCharAndDelete(workingString, ';', out usIDStr);

                                    // User ID
                                    long usID = -1;
                                    if (!long.TryParse(usIDStr, out usID))
                                    {
                                        Debug.ConversionError(usIDStr, "usID", usID);
                                        return;
                                    }

                                    Debug.Log("[ADMIN] Library requested");
                                    User usr = FindUser(usID);

                                    if (usr == null)
                                    {
                                        Debug.LogError("User with id " + usID + " not found");
                                        Send(client, "BU", "ERROR");
                                        return;
                                    }

                                    bool searchById = false;

                                    

                                    Game game = null;

                                    if(workingString.Length > 0)
                                    {
                                        if(workingString[0] == 'i')
                                        {
                                            searchById = true;
                                        }
                                        else if(workingString[0] == 'n')
                                        {
                                            searchById = false;
                                        }
                                        else
                                        {
                                            Debug.LogError("No search type specified");
                                            Send(client, "ST", "ERROR");
                                            return;
                                        }

                                        workingString = MUtil.GetStringToSpecialCharAndDelete(workingString, ';', out usIDStr);

                                        long gmID = -1;
                                        if (!long.TryParse(usIDStr, out gmID))
                                        {
                                            Debug.ConversionError(usIDStr, "gmID", gmID);
                                            return;
                                        }

                                        game = FindGame(gmID);
                                    }

                                    switch (cmdTypeArg)
                                    {
                                        // List
                                        case "LS":
                                            {

                                                Send(client, CreateUserLibraryString(usr), "RQLBR");
                                                break;
                                            }

                                        // Add
                                        case "AD":
                                            {
                                                if(game == null)
                                                {
                                                    Debug.LogError("Cannot find game");
                                                    Send(client, "BG", "ERROR");
                                                    return;
                                                }

                                                for(int i = 0;i<user.games.Count;i++)
                                                {
                                                    if(user.games[i].id == game.id)
                                                    {
                                                        Debug.LogError("Cannot add game that user already has");
                                                        Send(client, "GE", "ERROR");
                                                        return;
                                                    }
                                                }

                                                user.AddGame(game);

                                                Send(client, "OK", "ADMIN");

                                                break;
                                            }

                                        default:
                                            break;
                                    }


                                    break;
                                }

                            default:
                                Debug.LogError("Command \"" + cmdType + "\" not found");
                                Send(client, "BC", "ERROR");
                                return;
                        }
                        
                      
                        break;
                    }
            }
        }


        /// <summary>
        /// Deletes user from users list
        /// </summary>
        /// <param name="client"></param>
        public static void DeleteUser(ref NetworkEngine.Client client)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].userName == client.notes)
                {
                    users[i].socket = null;
                    users[i] = null;
                    users.RemoveAt(i);
                }
            }
        }

        public static Group FindGroup(string name)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].name == name)
                {
                    return groups[i];
                }
            }

            return null;
        }

        public static Group FindGroup(int id)
        {
            for(int i = 0;i<groups.Count;i++)
            {
                if(groups[i].id == id)
                {
                    return groups[i];
                }
            }
            return null;
        }

        public static Game FindGame(string name)
        {
            name = name.ToLower();

            for(int i = 0;i<games.Count;i++)
            {
                if(games[i].name.ToLower() == name)
                {
                    return games[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Finds game by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Game FindGame(Int64 id)
        {
            for(int i = 0;i<games.Count;i++)
            {
                if(games[i].id == id)
                {
                    return games[i];
                }
            }

            return null;
        }

        public User FindUser(long id)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].id == id)
                {
                    return users[i];
                }
            }
            return null;
        }

        public User FindUser(string userName)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].userName == userName)
                {
                    return users[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Finds user by client socket
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public User FindUser(NetworkEngine.Client client)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if(users[i].userName == client.notes)
                {
                    return users[i];
                }
            }
            return null;
        }

        public enum RegisterStatus
        {
            successfull,
            clientDisconnected,
            userAlreadyRegistered
        }

        public enum LoginStatus
        {
            successfull,
            notRegistered,
            badPassword,
            clientDisconnected,
            badCommand,
            unknownError,
        };

        /// <summary>
        /// Receives user credentials
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public UserCredentials GetClientCredentials(NetworkEngine.Client client)
        {
            //client.ForceReceive();



            string username = client.Receive_LowLevel(-2, false, false);

            if (!Send(client, "N", "CMMND"))
            {
                return new UserCredentials("NR", "-");//LoginStatus.unknownError;
            }

            string password = client.Receive_LowLevel(-2, false, false);

            return new UserCredentials(username, password);
        }

        public readonly object usersListLock = new object();


        // TO DO: Add currency that depends on nationality
        /// <summary>
        /// Gets user currency [ always returning zloty right now ]
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static Price.Currency GetUserCurrency(User user)
        {
            return Price.Currency.zloty;
        }

        /// <summary>
        /// Finds user by index in users list
        /// </summary>
        /// <param name="userIndex"></param>
        /// <returns></returns>
        public User GetUser(int userIndex)
        {
            lock(usersListLock)
            {
                return users[userIndex];
            }
        }

        /// <summary>
        /// Finds user by token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static User FindUserByToken(string token)
        {
            for(int i = 0;i<users.Count;i++)
            {
                if (users[i].token == token) return users[i];
            }

            return null;
        }



        const int tokenLenght = 32;
        /// <summary>
        /// Generates new user token
        /// </summary>
        private string GenerateToken()
        {
            string token = "";
            do
            {
                token = "";
                Random randomGenerator = new Random();
                for (int i = 0; i < tokenLenght; i++)
                {
                    token += (char)randomGenerator.Next(32, 127);
                }

            } while (FindUserByToken(token) != null);

            return token;
        }

        /// <summary>
        /// Adds new user to list
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <param name="client"></param>
        /// <returns>User info</returns>
        private User AddUser(UserCredentials userCredentials, NetworkEngine.Client client, List<Game> games = null, long coins = 0, Group permissionLevel = null)
        {
            User user;
            if (games == null)
            {
                user = new User(users.Count, userCredentials.login, userCredentials.password, new List<Game>(), GenerateToken(), client, coins, permissionLevel);
            }
            else
            {
                user = new User(users.Count, userCredentials.login, userCredentials.password, games, GenerateToken(), client, coins, permissionLevel);
            }
            
            //Mutex mtx = new Mutex();

            return AddUser(user, client);
        }

        private User AddUser(User user, NetworkEngine.Client client)
        {

            lock (usersListLock)
            {
                users.Add(user);
            }

            return user;
        }

        /// <summary>
        /// [Temporary] Adds some free games to new user's account
        /// </summary>
        /// <param name="user"></param>
        public void AddNewUserGames(User user)
        {

        }

        private static bool updatingUsersDataValues = false;
        private void UpdateUserValuesInFile(User user)
        {
            if (!File.Exists(usersConfigDir))
            {
                Debug.LogError("Users config dir doesn't exist!");
                return;
            }
            while(updatingUsersDataValues)
            {
                Thread.Sleep(10);
            }

            updatingUsersDataValues = true;

            string[] _lines = MFile.ReadAllLines(usersConfigDir);

            List<string> lines = new List<string>(_lines);

            string nameLine = "-name:" + user.userName;

            

            for(int i = 0;i<lines.Count;i++)
            {
                if(lines[i] == nameLine)
                {
                    Debug.LogWarning("Found nameline");
                    bool foundNewUser = false;
                    for(int j = i;j<lines.Count;j++)
                    {
                        if(lines[j].StartsWith('+'))
                        {
                            foundNewUser = true;
                            lines.RemoveRange(i, j - i - 1);
                            lines[i - 1] = CreateUserDataInFileFormat(user);
                            Debug.LogWarning("Lines[i-1]: " + lines[i - 1]);

                            break;
                        }
                    }
                    if(!foundNewUser)
                    {
                        lines.RemoveRange(i, lines.Count - i);
                        lines[i - 1] = CreateUserDataInFileFormat(user);
                        Debug.LogWarning("Lines[i-1]: " + lines[i - 1]);
                    }
                    break;
                }
            }

            lines = MUtil.RemoveEmptyLines(lines);

            MFile.WriteAllLines(usersConfigDir, lines.ToArray());

            updatingUsersDataValues = false;
        }

        private string CreateUserDataInFileFormat(User user)
        {
            string userData = "+user" + "\n";
            userData += "-name:" + user.userName + "\n";
            userData += "-password:" + user.password + "\n";
            userData += "-coins:" + user.coins.GetPrice(Price.Currency.coins).ToString() + "\n";
            userData += "-permission:" + user.permissionLevel.id.ToString() + "\n";
            userData += "-games:" + "\n";
            for(int i = 0;i<user.games.Count;i++)
            {
                userData += user.games[i].id.ToString() + "\n";
            }
            userData += '\n';


            return userData;
        }

        private void AddUserToFile(User user)
        {
            if(!File.Exists(usersConfigDir))
            {
                Debug.LogError("Users config dir doesn't exist!");
                return;
            }

            string userData = CreateUserDataInFileFormat(user);

            
            

            MFile.AppendAllText(usersConfigDir, userData);
        }

        /// <summary>
        /// Registers new user
        /// </summary>
        /// <param name="client"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public RegisterStatus RegisterUser(NetworkEngine.Client client, out User user)
        {
            user = null;

            UserCredentials userCredentials = GetClientCredentials(client);

            if(userCredentials.login == "NR" || userCredentials.password == "")
            {
                return RegisterStatus.clientDisconnected;
            }

            User foundUser = FindUser(userCredentials);
            if(foundUser == null)
            {
                user = AddUser(userCredentials, client);
               
                Debug.Log("Registered user " + userCredentials.login + " with password: " + userCredentials.password);
                
            }
            else
            {
                return RegisterStatus.userAlreadyRegistered;
            }

            //user = FindUser(userCredentials);

            AddNewUserGames(user);
            AddUserToFile(user);

            return RegisterStatus.successfull;
        }

        /// <summary>
        /// Send data to client
        /// </summary>
        /// <param name="client">Client to send data</param>
        /// <param name="data">Data to send</param>
        /// <param name="messageCode">5 digit long message identification code</param>
        /// <returns></returns>
        public bool Send(NetworkEngine.Client client, string data, string messageCode)
        {
            if(client.socket != null)
            {
                client.Send(data, messageCode);
                return true;
            }
            Debug.LogError("Socket is null");

            return false;
        }

        /// <summary>
        /// Finds user by credentials
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        User FindUser(UserCredentials userCredentials)
        {
            int usersCount = 0;
            lock(usersListLock)
            {
                usersCount = users.Count;
            }

            for(int i = 0;i<usersCount; i++)
            {
                if (GetUser(i).userName == userCredentials.login) return GetUser(i);//users[i].userName == userCredentials.login) return users[i];
            }

            return null;
        }

        /// <summary>
        /// Logs user
        /// </summary>
        /// <param name="client"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public LoginStatus LogUser(NetworkEngine.Client client, out User user)
        {
            user = null;

            while (true)
            {
                //client.ForceReceive();
                //string command = client.ReadData(5);
                string command = client.Receive_LowLevel(-2, false, false);
                if (command == "REGIS")
                {
                    RegisterStatus registerStauts = RegisterUser(client, out user);
                    switch(registerStauts)
                    {
                        case RegisterStatus.successfull:
                            client.Send("RS:OK", "CMMND");
                            break;
                        case RegisterStatus.userAlreadyRegistered:
                            client.Send("RS:AR", "CMMND");
                            break;
                    }
                }
                else if (command == "LOGIN")
                {
                    break;
                }
                else
                {
                    Debug.LogError("Bad command " + command);
                    if(!client.socket.Connected)
                    {
                        client.Dispose();
                        return LoginStatus.clientDisconnected;
                    }
                    return LoginStatus.badCommand;
                }
            }

            

            UserCredentials userCredentials = GetClientCredentials(client);
            string username = userCredentials.login;
            string password = userCredentials.password;

            user = FindUser(userCredentials);


            client.notes = username;

            user = FindUser(client);

            if(password.Length == 0 || userCredentials.login == "NR")
            {
                Debug.LogError("Client disconnected");
                return LoginStatus.clientDisconnected;
            }

            if(user == null)
            {
                return LoginStatus.notRegistered;
            }

            if(user.password == password)
            {
                return LoginStatus.successfull;
            }
            else
            {
                Debug.LogError(password + " is not the correct password");
                return LoginStatus.badPassword;
            }
        }

        /// <summary>
        /// Thread for managing new user adding
        /// </summary>
        /// <param name="client"></param>
        private void AddClient_Thread(NetworkEngine.Client client)
        {
            Debug.Log("New connection");

            User user = null;
            LoginStatus status = LoginStatus.notRegistered;
            while (status != LoginStatus.successfull)
            {
                status = LogUser(client, out user);
                if (status == LoginStatus.clientDisconnected) return;
                if (status == LoginStatus.notRegistered)
                {
                    client.Send("LS:NR", "CMMND");

                    //Thread.Sleep(2000);
                    //return;
                }
                if(status == LoginStatus.badPassword)
                {
                    client.Send("LS:BP", "CMMND");
                }
            }

            Send(client, "LS:OK", "CMMND");

            
            
            client.dataReceivedFunction = ClientDataReceived;

            client.thread.Start();
            Debug.Log("Client logged", ConsoleColor.Blue);
        }

        public bool LoadPermissionsFromFile(string filePath)
        {
            groups = new List<Group>();

            if(!File.Exists(filePath))
            {
                Debug.LogError("Cannot load permission from " + filePath + ", file doesn't exist");
                return false;
            }


            string[] lines = MFile.ReadAllLines(filePath);

            for(int i = 0;i<lines.Length;i++)
            {
                if(lines[i].StartsWith("default:"))
                {
                    lines[i] = lines[i].Remove(0, "default:".Length);
                    int defId = 0;
                    if(!int.TryParse(lines[i], out defId))
                    {
                        Debug.LogError("Cannot parse " + lines[i] + " to default level ID ( int )");
                        return false;
                    }
                    defaultPermissionLevlID = defId;

                    continue;
                }

                Group level = new Group(-1, "", false);

                string workingStr = "";
                //Name
                lines[i] = MUtil.GetStringToSpecialCharAndDelete(lines[i], ';', out workingStr);

                level.name = workingStr;

                lines[i] = MUtil.GetStringToSpecialCharAndDelete(lines[i], ';', out workingStr);

                int _id = 0;
                //Id
                if(!int.TryParse(workingStr, out _id))
                {
                    Debug.LogError("Cannot parse " + lines[i] + " to id ( int )");
                    return false;
                }

                while(lines[i].Length != 0)
                {
                    lines[i] = MUtil.GetStringToSpecialCharAndDelete(lines[i], ';', out workingStr);
                    if(workingStr == "admin")
                    {
                        level.isAdmin = true;
                    }
                }

                level.id = _id;

                groups.Add(level);

               
            }

            return true;
        }

        private static bool workingOnVouchers = false;
        public static void AddVoucher(Voucher voucher)
        {
            while(workingOnVouchers)
            {
                Thread.Sleep(10);
            }

            workingOnVouchers = true;

            vouchers.Add(voucher);

            workingOnVouchers = false;
        }

        public static void RemoveVoucher(Voucher voucher)
        {
            while(workingOnVouchers)
            {
                Thread.Sleep(10);
            }

            workingOnVouchers = true;

            vouchers.Remove(voucher);

            workingOnVouchers = false;
        }

        public bool LoadVouchersFromFile(string filePath)
        {
            vouchers = new List<Voucher>();

            if (!File.Exists(filePath))
            {
                Debug.LogError("Voucher file path " + filePath + " doesn't exist");
                return false;
            }

            string[] lines = MFile.ReadAllLines(filePath);

            lines = MUtil.RemoveEmptyLines(lines);

            for(int i = 0;i<lines.Length;i++)
            {
                if(lines[i].Length == 0)
                {
                    continue;
                }

                if(lines[i][0] == '#')
                {
                    continue;
                }

                string code = "";
                int colonPos = -1;
                for(int j = 0;j<lines[i].Length;j++)
                {
                    if(lines[i][j] == ':')
                    {
                        colonPos = j;
                        break;
                    }

                    code += lines[i][j];
                }
                if(colonPos == -1)
                {
                    Debug.LogError("Cannot parse line, no data after code in line " + i);
                    return false;
                }

                lines[i] = lines[i].Remove(0, colonPos + 1);
                bool coins = false;

                if(lines[i][0] == 'c')
                {
                    coins = true;
                    lines[i] = lines[i].Remove(0, 1);
                }

                bool semicolonFound = false;
                string nmbr = "";
                for(int j = 0;j<lines[i].Length;j++)
                {
                    if(lines[i][j] == ';')
                    {
                        lines[i] = lines[i].Remove(0, j+1);
                        semicolonFound = true;
                        break;
                    }

                    nmbr += lines[i][j];
                }

                if(!semicolonFound)
                {
                    lines[i] = "";
                }

                long number = 0;
                if (!long.TryParse(nmbr, out number))
                {
                    Debug.LogError("Error at line " + i + ", cannot parse \"" + lines[i] + "\" to number ( long )");
                    return false;
                }

                long coinsNumber = 0;
                long gameID = -1;
                if(coins)
                {

                    coinsNumber = number;
                }
                else
                {
                    gameID = number;

                    //Find game by name
                    /*Game game = FindGame(nmbr);
                    if(game == null)
                    {
                        Debug.LogError("Cannot find game with name " + nmbr);
                        return false;
                    }

                    gameID = game.id;*/
                }

                int uses = 0;
                if(lines[i].Length == 0)
                {
                    uses = -1;
                }
                else
                {
                    if(!int.TryParse(lines[i], out uses))
                    {
                        Debug.LogError("Error at line " + i + ", cannot parse \"" + lines[i] + "\" to uses ( int )");
                        return false;
                    }
                }
                AddVoucher(new Voucher(code, coinsNumber, gameID, uses));
            }

            return true;
        }

        /// <summary>
        /// Starts AddClient thread
        /// </summary>
        /// <param name="client"></param>
        public void AddClient(NetworkEngine.Client client)
        {
            Thread addClientThread = new Thread(() => AddClient_Thread(client));
            addClientThread.Start();
        }

        private void CalculateGamesSizes()
        {
            for(int i = 0;i<games.Count;i++)
            {
                string __path = DownloadsManager.downloadFilesDirectory + games[i].filename;
                if (File.Exists(__path))
                {
                    FileInfo info = new FileInfo(__path);
                    games[i].downloadSize = info.Length;


                    ZipArchive archive = ZipFile.Open(__path, ZipArchiveMode.Read);

                    long sizeOnDisk = 0;
                    for(int j = 0;j<archive.Entries.Count;j++)
                    {
                        sizeOnDisk += archive.Entries[j].Length;
                    }

                    games[i].diskSize = sizeOnDisk;
                }
            }
        }

        public bool LoadUsersFromFile(string usersConfigFilePath)
        {
            users = new List<User>();
            if (!File.Exists(usersConfigFilePath))
            {
                NetworkEngine.WriteError("Users config file on path \"" + usersConfigFilePath + "\" doesn't exist, do You want to create new? ( Y - yes, N - no )");
                //return false;
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y)
                {
                    FileStream stream = File.Create(usersConfigFilePath);
                    stream.Close();
                    Debug.LogWarning("\nCreated new file");
                }
                else
                {
                    Debug.LogError("\nNot creating new file");
                    return false;
                }
            }

            string[] lines = MFile.ReadAllLines(usersConfigFilePath);

            lines = MUtil.RemoveEmptyLines(lines);

            int lastCat = -1;

            User lastUser = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                switch (lines[i][0])
                {
                    case '+':
                        if (lines[i].Remove(0, 1) != "user")
                        {
                            Debug.LogError("Error in line " + i + ", unknown command " + lines[i].Remove(0, 1));
                            return false;
                        }
                        if (lastUser != null)
                        {
                            if (lastUser.userName == "")
                            {
                                Debug.LogError("Cannot load userName in line " + i);
                                return false;
                            }
                            if (lastUser.password == "")
                            {
                                Debug.LogError("Cannot load user " + lastUser.userName + " password in line " + i);
                                return false;
                            }
                            AddUser(new UserCredentials(lastUser.userName, lastUser.password), lastUser.socket, lastUser.games, (long)lastUser.coins.GetPrice(Price.Currency.coins), lastUser.permissionLevel);

                        }
                        lastUser = new User(-2, "", "", new List<Game>(), "");
                        lastCat = -1;
                        break;
                    case '-':
                        string category = "";
                        string data = "";
                        bool result = FileParser.ParseOption(lines[i], out category, out data);

                        if (result)
                        {
                            switch (category)
                            {
                                case "name":
                                    lastUser.userName = data;
                                    break;

                                case "password":
                                    lastUser.password = data;
                                    break;

                                case "coins":
                                    Int64 newCoinsValue = 0;
                                    if (Int64.TryParse(data, out newCoinsValue))
                                    {
                                        lastUser.coins = newCoinsValue;
                                        //lastUser.coins.SetPrice(newCoin)
                                    }
                                    else
                                    {
                                        Debug.LogError("Cannot parse \"" + data + "\" to coins ( long )");
                                        return false;
                                    }
                                    break;

                                case "games":
                                    lastCat = 1;
                                    break;

                                case "permission":
                                case "group":
                                case "permissionLevel":
                                    {
                                        int level = -1;
                                        if(!int.TryParse(data, out level))
                                        {
                                            Debug.LogError("Cannot parse \"" + data + "\" to level ( int )");
                                            return false;
                                        }

                                        Group gr = FindGroup(level);
                                        if(gr == null)
                                        {
                                            Debug.LogError("Cannot find group with id " + level);
                                            return false;
                                        }

                                        lastUser.permissionLevel = gr;

                                        break;
                                    }

                                default:
                                    NetworkEngine.WriteError("Category " + category + " not found, error in file in line " + (i + 1).ToString());
                                    return false;
                                    break;
                            }
                            lastCat = -1;
                        }
                        else
                        {
                            switch (category)
                            {
                                case "games":
                                    lastCat = 1;
                                    break;
                            }
                        }
                        break;
                    case '#':
                        continue;
                    case ' ':
                        continue;
                    default:
                        switch (lastCat)
                        {
                            //Games
                            case 1:
                                long id = -5;
                                if (!long.TryParse(lines[i], out id))
                                {
                                    Debug.LogError("Cannot parse \"" + lines[i] + "\" to gameID ( long ) in line " + i);
                                    return false;
                                }
                                Game game = FindGame(id);
                                if (game == null)
                                {
                                    Debug.LogError("Cannot find game with id " + id + " in line " + i);
                                    return false;
                                }

                                lastUser.games.Add(game);

                                break;
                            default:
                                NetworkEngine.WriteError("Data without category ( " + lines[i] + " ) on line " + (i + 1).ToString());
                                return false;
                                break;
                            case -1:
                                NetworkEngine.WriteError("Data without category ( " + lines[i] + " ) on line " + (i + 1).ToString());
                                return false;
                                break;
                        }
                        break;
                }
            }

            if (lastUser != null)
            {
                if (lastUser.userName == "")
                {
                    Debug.LogError("Cannot load userName in line " + lines.Length);
                    return false;
                }
                if (lastUser.password == "")
                {
                    Debug.LogError("Cannot load user " + lastUser.userName + " password in line " + lines.Length);
                    return false;
                }
                AddUser(new UserCredentials(lastUser.userName, lastUser.password), lastUser.socket, lastUser.games, (long)lastUser.coins.GetPrice(Price.Currency.coins), lastUser.permissionLevel);
                //AddUser(lastUser, lastUser.socket);
            }

            return true;
        }

        /// <summary>
        /// Loads games from file
        /// </summary>
        /// <param name="configFilePath">File to load from</param>
        /// <returns></returns>
        public bool LoadGamesFromFile(string configFilePath)
        {
            games = new List<Game>();

            if (!File.Exists(configFilePath))
            {
                NetworkEngine.WriteError("Games config file on path \"" + configFilePath + "\" doesn't exist");
                return false;
            }

            string[] lines = MFile.ReadAllLines(configFilePath);

            lines = MUtil.RemoveEmptyLines(lines);

            int lastCat = -1;

            for(int i = 0;i<lines.Length;i++)
            {
                if (lines[i].Length == 0) continue;
                switch(lines[i][0])
                {
                    case '+':
                        games.Add(new Game("", -games.Count - 1));
                        lastCat = -1;
                        break;
                    case '-':
                        string category = "";
                        string data = "";
                        bool result = FileParser.ParseOption(lines[i], out category, out data);

                        if(result)
                        {
                            lastCat = -1;
                            switch (category.ToLower())
                            {
                                case "name":
                                case "app":
                                    games[games.Count - 1].name = data;
                                    break;

                                case "price":
                                    try
                                    {
                                        games[games.Count - 1].price.SetPrice(Price.Currency.coins, decimal.Parse(data, NumberStyles.Any, CultureInfo.InvariantCulture));
                                    } catch(FormatException e)
                                    {
                                        NetworkEngine.WriteError("Error at line " + (i+1).ToString() + ": " + e.Message );
                                        games.RemoveAt(games.Count - 1);
                                        return false;
                                    }
                                    break;

                                case "groupprice":
                                    {
                                        string wData = "";
                                        //GroupID
                                        data = MUtil.GetStringToSpecialCharAndDelete(data, ';', out wData);



                                        break;
                                    }

                                case "path":
                                    games[games.Count - 1].path = data;
                                    break;

                                case "file":
                                    games[games.Count - 1].filename = data;
                                    break;

                                case "exec":
                                    games[games.Count - 1].execName = data;
                                    break;

                                case "icon":
                                    games[games.Count - 1].iconPath = data;
                                    break;

                                case "id":
                                    long _id = 0;
                                    if(long.TryParse(data, out _id))
                                    {
                                        Game game = FindGame(_id);
                                        if(game != null)
                                        {
                                            Debug.LogError("ID " + _id + " is already taken!");
                                            return false;
                                        }

                                        games[games.Count - 1].id = _id;
                                    }
                                    else
                                    {
                                        Debug.LogError("Cannot parse " + data + " to game ID ( long )");
                                        return false;
                                    }
                                    
                                    break;

                                case "version":
                                    int v = 0;
                                    if(!int.TryParse(data, out v))
                                    {
                                        Debug.LogError("Cannot parse " + data + " to version ( int )");
                                        return false;
                                    }
                                    games[games.Count - 1].version = v;
                                    break;

                                default:
                                    NetworkEngine.WriteError("Category " + category + " not found, error in file on line " + (i+1).ToString());
                                    return false;
                                    break;
                            }
                            
                        }
                        break;
                    case '#':
                        continue;
                    case ' ':
                        continue;
                    default:
                        switch(lastCat)
                        {
                            default:
                                NetworkEngine.WriteError("Data without category ( " + lines[i] + " ) on line " + (i+1).ToString());
                                return false;
                                break;
                            case -1:
                                NetworkEngine.WriteError("Data without category ( " + lines[i] + " ) on line " + (i+1).ToString());
                                return false;
                                break;
                        }
                        break;
                }
            }

            CalculateGamesSizes();

            return true;
        }

        /// <summary>
        /// Writes games info in console
        /// </summary>
        void DisplayGames()
        {
            Console.WriteLine("Games: ");
            for(int i = 0;i<games.Count;i++)
            {
                Console.WriteLine(games[i].name + "[" + games[i].id + "]: " + games[i].price.GetPriceStr(Price.Currency.coins) + " ( " + games[i].price.GetPriceStr(Price.Currency.zloty) + "zł ). Path: " + games[i].path);
            }
        }

        /// <summary>
        /// Deletes spaces
        /// </summary>
        /// <param name="line">String to remove spaces from</param>
        /// <returns>String without spaces</returns>
        private string DeleteSpaces(string line)
        {
            for(int i = 0;i<line.Length;i++)
            {
                if(line[i] == ' ')
                {
                    line = line.Remove(i, 1);
                    i--;
                }
            }

            return line;
        }

        /// <summary>
        /// Parses one line from config
        /// </summary>
        /// <param name="line">Line to parse</param>
        private void ParseConfigLine(string line)
        {
            line = DeleteSpaces(line);

            if(line.ToLower().StartsWith("port="))
            {
                line = line.Remove(0, "port=".Length);
                int _port = 0;
                if(int.TryParse(line, out _port))
                {
                    port = _port;
                    Debug.Log("New port: " + port);
                }
                else
                {
                    Debug.LogError("Error parsing " + line + " to port ( int )");
                }
            }
            else if (line.ToLower().StartsWith("downloadport="))
            {
                line = line.Remove(0, "downloadport=".Length);

                int _downloadPort = 0;

                if (int.TryParse(line, out _downloadPort))
                {
                    downloadEnginePort = _downloadPort;

                    Debug.Log("New download engine port: " + downloadEnginePort);
                }
                else
                {
                    Debug.LogError("Invalid port format, cannot parse " + line + " to download port ( int )");
                }
            }
            else if (line.ToLower().StartsWith("iconsdownloadport=") || line.ToLower().StartsWith("downloadiconsport="))
            {
                line = line.Remove(0, "iconsdownloadport=".Length);

                int _downloadPort = 0;

                if (int.TryParse(line, out _downloadPort))
                {
                    downloadIconsPort = _downloadPort;

                    Debug.Log("New icons download port: " + downloadIconsPort);
                }
                else
                {
                    Debug.LogError("Invalid port format, cannot parse " + line + " to icons download port ( int )");
                }
            }
            else if(line.ToLower().StartsWith("maxpacketsize="))
            {
                line = line.Remove(0, "maxpacketsize=".Length);

                int _maxPacket = -1;

                try
                {
                    _maxPacket = int.Parse(line);

                    DownloadEngine.packetSize = _maxPacket;

                    Debug.Log("Max packet size: " + DownloadEngine.packetSize);
                }
                catch(Exception)
                {
                    Debug.ConversionError(line, "_maxPacket", _maxPacket);
                    return;
                }
            }
            else
            {
                Debug.LogError("Config option: " + line + " not found");
                return;
            }
        }

        

        const string configDir = "config.ini";
        /// <summary>
        /// Loads config from configDir
        /// </summary>
        private void LoadConfig()
        {
            if(!File.Exists(configDir))
            {
                Debug.LogError("File " + configDir + " doesn't exist");
                return;
            }

            string[] lines = File.ReadAllLines(configDir);

            for(int i = 0;i<lines.Length;i++)
            {
                ParseConfigLine(lines[i]);
            }
                
        }



        public int port = 15332;
        public int downloadEnginePort = 5592;
        public int downloadIconsPort = 5593;

        const string usersConfigDir = "users.conf";
        const string voucherConfigDir = "vouchers.dat";


        public bool LoadAESKeysFromFile(string filePath)
        {
            if(!File.Exists(filePath))
            {
                Debug.LogError("File " + filePath + " doesn't exist");
                return false;
            }

            string file = File.ReadAllText(filePath);
            if(file.Length == 0)
            {
                Debug.LogError("Empty file");
                return false;
            }

            //Checking if encrypted
            if (file.StartsWith("encrypt"))
            {
                while (true)
                {


                    Debug.LogWarning("File is not encrypted, do You want to encrypt? Y - yes, N - no");
                    ConsoleKeyInfo info = Console.ReadKey();
                    if (info.Key == ConsoleKey.Y)
                    {
                        Debug.Log("\nEncrypting file " + file + "...");

                        string[] l = MUtil.StringToStringArray(file);
                        file = "";
                        for(int i = 1;i<l.Length - 1;i++)
                        {
                            file += l[i] + "\n";
                        }
                        file += l[l.Length - 1];

                        byte[] encrypted = MCrypt.EncryptString(file, MCrypt.lockFileKey, MCrypt.lockFileIV);

                        File.WriteAllBytes(filePath, encrypted);

                        return LoadAESKeysFromFile(filePath);
                        
                    }
                    if (info.Key == ConsoleKey.N)
                    {
                        Debug.LogWarning("\nBad idea... So i'm closing");


                        return false;
                    }
                }

                
            }



            byte[] encryptedFile = File.ReadAllBytes(filePath);

            string decryptedFile = MCrypt.DecryptByteArray(encryptedFile, MCrypt.lockFileKey, MCrypt.lockFileIV);


            //0 - key, 1 - IV
            string[] lines = MUtil.StringToStringArray(decryptedFile);

            if(lines.Length != 2)
            {
                Debug.LogError("Bad data count in file, got " + lines.Length + " instead of 3");
                return false;
            }

            byte[] iv = MCrypt.ParseLineToAESKey(lines[0]);
            byte[] key = MCrypt.ParseLineToAESKey(lines[1]);
            

            if(key == null)
            {
                Debug.LogError("Cannot convert line to key");
                return false;
            }

            if (iv == null)
            {
                Debug.LogError("Cannot convert line to IV");
                return false;
            }



            MCrypt.filesKey = key;
            MCrypt.filesIV = iv;


            return true;
        }

        public void EncryptFilesIfNeeded(string[] files)
        {
            for(int i = 0;i<files.Length;i++)
            {
                if(!File.Exists(files[i]))
                {
                    Debug.LogWarning("File " + files[i] + " doesn't exist");
                    continue;
                }

                string[] lines = File.ReadAllLines(files[i]);
                if(lines.Length == 0)
                {
                    continue;
                }
                if(lines[0] != "encrypt")
                {
                    //Debug.LogWarning("Not encrypting " + files[i]);
                    continue;
                }


                string toEncrypt = "";
                for(int j = 1;j<lines.Length - 1;j++)
                {
                    toEncrypt += lines[j] + '\n';
                }
                toEncrypt += lines[lines.Length - 1];


                //byte[] encrypted = MCrypt.EncryptString(toEncrypt);

                //File.WriteAllBytes(files[i], encrypted);
                MFile.WriteAllText(files[i], toEncrypt);
            }
        }

        public void RemoveEmptyLinesFromFiles(string[] files)
        {
            for(int i = 0;i<files.Length;i++)
            {
                if(!File.Exists(files[i]))
                {
                    Debug.LogWarning("Files " + files[i] + " doesn't exist");
                    continue;
                }

                string[] content = MFile.ReadAllLines(files[i]);

                content = MUtil.RemoveEmptyLines(content);

                MFile.WriteAllLines(files[i], content);
            }
        }

        /// <summary>
        /// Loads config, Loads games and starts New client thread
        /// </summary>
        public StoreServer()
        {
            bool result = LoadAESKeysFromFile("lock");
            if(!result)
            {
                Debug.LogError("Cannot continue without AES keys");
                Thread.Sleep(5000);
                Environment.Exit(-10);
                return;
            }

            string[] files = {"games.conf", usersConfigDir, voucherConfigDir, "permissions.conf" };
            EncryptFilesIfNeeded(files);
            RemoveEmptyLinesFromFiles(files);

            //users = new List<User>();
            LoadConfig();

            LoadPermissionsFromFile("permissions.conf");

            LoadGamesFromFile("games.conf");

            LoadUsersFromFile(usersConfigDir);

            LoadVouchersFromFile(voucherConfigDir);

            


            Debug.Log("Starting server on port ", ConsoleColor.Yellow, false);
            Debug.Log(port, ConsoleColor.Green, true);
            socket = new NetworkEngine(port);
            socket.addUserFunction = new NetworkEngine.NewClientFunction(AddClient);

            DownloadsManager downloadsManager = new DownloadsManager(downloadEnginePort);
            DownloadsManager iconsDownloadManager = new DownloadsManager(downloadIconsPort);


            
        }
    }
}
