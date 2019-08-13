using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Globalization;

using System.Net.Sockets;

using System.Threading;

using System.IO.Compression;

namespace MStoreServer
{
    public class StoreServer
    {
        private static List<User> users;
        private static List<Game> games;

        private static List<Voucher> vouchers;

        private NetworkEngine socket;

        public struct Voucher
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

            public Size diskSize = new Size(-1);
            public Size downloadSize = new Size(-1);

            public Price price;


            public Game(string _name = "\0", Int64 _id = -1, string _path = "\0", Price _price = default(Price), string _filename = "\0", string _iconPath = "\0")
            {
                name = _name;
                id = _id;
                path = _path;
                price = _price;

                filename = _filename;
                iconPath = _iconPath;
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

            public Int64 id = -1;

            public Price coins = new Price(-1);


            public User(Int64 _id, string _userName, string _password, List<Game> _games, string _token, NetworkEngine.Client _socket = null, Int64 _coins = 0)
            {
                games = _games;
                id = _id;
                userName = _userName;
                password = _password;
                games = _games;
                token = _token;
                socket = _socket;
                coins.SetPrice(Price.Currency.coins, _coins);
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

            Debug.Log("User games count: " + client.games.Count);

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
            Debug.Log("Game price: " + game.price.GetPriceStr(GetUserCurrency(user)));
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

            string[] lines = File.ReadAllLines(voucherConfigDir);
            for(int i = 0;i<lines.Length;i++)
            {
                if(lines[i].StartsWith(voucherCode.code))
                {
                    if(voucherCode.usesLeft < -1)
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
                    Debug.Log("Uses left" + voucherCode.usesLeft);
                    lines[i] += ";" + voucherCode.usesLeft.ToString();
                    break;
                }
            }

            File.WriteAllLines(voucherConfigDir, lines);

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

        public bool UseVoucher(string code, User user, out Voucher voucherUsed)
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
        }

        public bool CheckVoucher(string code)
        {
            for(int i = 0;i<vouchers.Count;i++)
            {
                if(vouchers[i].code == code)
                {
                    return true;
                }
            }

            return false;
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
            switch(command)
            {
                //User info
                case "URNFO":
                    {
                        Debug.Log("User info requested");
                        User user = FindUser(client);
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
                            Debug.Log("Empty game id");
                            Send(client, "Empty game id", "ERROR");
                            break;
                        }
                        Int64 gameID = 0;
                        if (Int64.TryParse(data, out gameID))
                        {
                            Game requestedGame = FindGame(gameID);

                            if (requestedGame == null)
                            {
                                Debug.Log("Game not found");
                                Send(client, "Game not found", "ERROR");
                                break;
                            }
                            Debug.Log("Sending info for the game " + requestedGame.id);
                            Send(client, ParseGameInfo(requestedGame, FindUser(client)), "GMNFO");
                        }
                        else
                        {
                            Debug.Log("Cannot parse game id");
                            Send(client, "Game id is invalid", "ERROR");
                            break;
                        }
                        break;
                    }

                    
                // Store games list
                case "SGLST":
                    {
                        User user = FindUser(client);
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
                        User user = FindUser(client);
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
                        User user = FindUser(client);
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

                        bool result = CheckVoucher(data);

                        if(result == false)
                        {
                            Send(client, "BV", "ERROR");
                            return;
                        }

                        Voucher used;

                        bool useResult = UseVoucher(data, user, out used);
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



            //string username = client.ReadData('\n');
            Debug.Log("Waiting for username...");
            string username = client.Receive_LowLevel(-2, false, false);
            Debug.Log("Username: " + username);

            //client.Send("N");
            Debug.Log("Sending N to client");
            if (!Send(client, "N", "CMMND"))
            {
                return new UserCredentials("NR", "-");//LoginStatus.unknownError;
            }

            //client.ForceReceive();
            //string password = client.ReadData('\n');
            Debug.Log("Waiting for password...");
            string password = client.Receive_LowLevel(-2, false, false);
            Debug.Log("Password: " + password);

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

                Debug.Log("Token: \"" + token + "\"");
            } while (FindUserByToken(token) != null);

            return token;
        }

        /// <summary>
        /// Adds new user to list
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <param name="client"></param>
        /// <returns>User info</returns>
        private User AddUser(UserCredentials userCredentials, NetworkEngine.Client client, List<Game> games = null, long coins = 0)
        {
            User user;
            if (games == null)
            {
                user = new User(users.Count, userCredentials.login, userCredentials.password, new List<Game>(), GenerateToken(), client, coins);
            }
            else
            {
                user = new User(users.Count, userCredentials.login, userCredentials.password, games, GenerateToken(), client, coins);
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

            string[] _lines = File.ReadAllLines(usersConfigDir);

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

            File.WriteAllLines(usersConfigDir, lines);

            updatingUsersDataValues = false;
        }

        private string CreateUserDataInFileFormat(User user)
        {
            string userData = "+user" + "\n";
            userData += "-name:" + user.userName + "\n";
            userData += "-password:" + user.password + "\n";
            userData += "-coins:" + user.coins.ToString(user) + "\n";
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

            File.AppendAllText(usersConfigDir, userData);
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
            Debug.Log("LogUser");
            user = null;

            while (true)
            {
                //client.ForceReceive();
                //string command = client.ReadData(5);
                string command = client.Receive_LowLevel(-2, false, false);
                Debug.Log("Command: " + command);
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
                Debug.Log("Client not registered");
                return LoginStatus.notRegistered;
            }

            if(user.password == password)
            {
                Debug.Log("Login successfull");
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
            Debug.Log("Client successfully added", ConsoleColor.Blue);
        }

        public bool LoadVouchersFromFile(string filePath)
        {
            vouchers = new List<Voucher>();

            if (!File.Exists(filePath))
            {
                Debug.LogError("Voucher file path " + filePath + " doesn't exist");
                return false;
            }

            string[] lines = File.ReadAllLines(filePath);
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
                if(!long.TryParse(nmbr, out number))
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
                }

                int uses = 0;
                if(lines[i].Length == 0)
                {
                    Debug.Log("Infinite uses");
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

                Debug.Log("Adding new voucher with code: " + code + " that adds " + coinsNumber + " coins and game with ID " + gameID + " with " + uses + " uses");
                vouchers.Add(new Voucher(code, coinsNumber, gameID, uses));
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

                    Debug.Log(games[i].name + " download size: " + games[i].downloadSize);

                    ZipArchive archive = ZipFile.Open(__path, ZipArchiveMode.Read);

                    long sizeOnDisk = 0;
                    for(int j = 0;j<archive.Entries.Count;j++)
                    {
                        sizeOnDisk += archive.Entries[j].Length;
                    }

                    games[i].diskSize = sizeOnDisk;
                    Debug.Log(games[i].name + " disk size: " + games[i].diskSize);
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
                if(key.Key == ConsoleKey.Y)
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

            string[] lines = File.ReadAllLines(usersConfigFilePath);

            int lastCat = -1;

            User lastUser = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                switch (lines[i][0])
                {
                    case '+':
                        if(lines[i].Remove(0,1) != "user")
                        {
                            Debug.LogError("Error in line " + i + ", unknown command " + lines[i].Remove(0, 1));
                            return false;
                        }
                        if(lastUser != null)
                        {
                            if(lastUser.userName == "")
                            {
                                Debug.LogError("Cannot load userName in line " + i);
                                return false;
                            }
                            if(lastUser.password == "")
                            {
                                Debug.LogError("Cannot load user " + lastUser.userName + " password in line " + i);
                                return false;
                            }
                            AddUser(new UserCredentials(lastUser.userName, lastUser.password), lastUser.socket, lastUser.games, (long)lastUser.coins.GetPrice(Price.Currency.coins));
                            Debug.Log("Loaded user " + lastUser.userName);
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
                            Debug.Log("Category: " + category);
                            Debug.Log("Data: " + data);

                            //lastCat = -1;
                            switch (category)
                            {
                                case "name":
                                    lastUser.userName = data;
                                    Debug.Log("New user username: " + lastUser.userName);
                                    break;

                                case "password":
                                    lastUser.password = data;
                                    Debug.Log("New user password: " + lastUser.password);
                                    break;

                                case "coins":
                                    Int64 newCoinsValue = 0;
                                    if(Int64.TryParse(data, out newCoinsValue))
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
                                if(!long.TryParse(lines[i], out id))
                                {
                                    Debug.LogError("Cannot parse \"" + lines[i] + "\" to gameID ( long ) in line " + i);
                                    return false;
                                }
                                Game game = FindGame(id);
                                if(game == null)
                                {
                                    Debug.LogError("Cannot find game with id " + id + " in line " + i);
                                }

                                lastUser.games.Add(game);
                                Debug.Log("Added game " + game.name + " to user " + lastUser.userName);

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
                AddUser(new UserCredentials(lastUser.userName, lastUser.password), lastUser.socket, lastUser.games, (long)lastUser.coins.GetPrice(Price.Currency.coins));
                Debug.Log("Loaded user " + lastUser.userName);
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

            string[] lines = File.ReadAllLines(configFilePath);

            int lastCat = -1;

            for(int i = 0;i<lines.Length;i++)
            {
                if (lines[i].Length == 0) continue;
                switch(lines[i][0])
                {
                    case '+':
                        games.Add(new Game("", games.Count));
                        Debug.Log("GAME ADDED");
                        lastCat = -1;
                        break;
                    case '-':
                        string category = "";
                        string data = "";
                        bool result = FileParser.ParseOption(lines[i], out category, out data);

                        if(result)
                        {
                            lastCat = -1;
                            switch (category)
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
                    Debug.Log("Error parsing " + line + " to port ( int )");
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

        /// <summary>
        /// Loads config, Loads games and starts New client thread
        /// </summary>
        public StoreServer()
        {
            //users = new List<User>();
            LoadConfig();
            LoadGamesFromFile("games.conf");

            LoadUsersFromFile(usersConfigDir);

            LoadVouchersFromFile(voucherConfigDir);

            DisplayGames();

            Debug.Log("Starting server on port " + port + "...");
            socket = new NetworkEngine(port);
            socket.addUserFunction = new NetworkEngine.NewClientFunction(AddClient);

            DownloadsManager downloadsManager = new DownloadsManager(downloadEnginePort);
            DownloadsManager iconsDownloadManager = new DownloadsManager(downloadIconsPort);


            
        }
    }
}
