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

        private NetworkEngine socket;

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
        }

        public class Game
        {
            public string name = "\0";
            public Int64 id = -1;

            public string path = "\0";
            public string filename = "\0";

            public string execName = "\0";

            public Size diskSize = new Size(-1);
            public Size downloadSize = new Size(-1);

            public Price price;


            public Game(string _name = "\0", Int64 _id = -1, string _path = "\0", Price _price = default(Price), string _filename = "\0")
            {
                name = _name;
                id = _id;
                path = _path;
                price = _price;

                filename = _filename;
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


            public User(Int64 _id, string _userName, string _password, List<Game> _games, string _token, NetworkEngine.Client _socket = null)
            {
                games = new List<Game>();
                id = _id;
                userName = _userName;
                password = _password;
                games = _games;
                token = _token;
                socket = _socket;
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
            //info += user.games.Count.ToString() + '\n';

            return info;
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
                    Debug.Log("User info requested");
                    User user = FindUser(client);
                    if(user == null)
                    {
                        Send(client, "User not found", "ERROR");
                        break;
                    }
                    Send(client, ParseUserInfo(user), "URNFO");
                    break;

                //Request library
                case "RQLBR":
                    Debug.Log("Library requested");
                    Send(client, CreateUserLibraryString(FindUser(client)), "RQLBR");
                    break;

                //Game info
                case "GMNFO":
                    if(data.Length == 0)
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
            client.ForceReceive();



            string username = client.ReadData('\n');
            Debug.Log("Username: " + username);

            //client.Send("N");
            if (!Send(client, "N", "CMMND"))
            {
                return new UserCredentials("NR", "-");//LoginStatus.unknownError;
            }

            client.ForceReceive();
            string password = client.ReadData('\n');
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
        public Price.Currency GetUserCurrency(User user)
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
        private void AddUser(UserCredentials userCredentials, NetworkEngine.Client client)
        {
            //Mutex mtx = new Mutex();
            lock (usersListLock)
            {
                users.Add(new User(users.Count, userCredentials.login, userCredentials.password, new List<Game>(), GenerateToken(), client));
            }
        }

        /// <summary>
        /// [Temporary] Adds some free games to new user's account
        /// </summary>
        /// <param name="user"></param>
        public void AddNewUserGames(User user)
        {
            user.games.Add(games[0]);
            user.games.Add(games[1]);
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
                AddUser(userCredentials, client);
                Debug.Log("Registered user " + userCredentials.login + " with password: " + userCredentials.password);
                
            }
            else
            {
                return RegisterStatus.userAlreadyRegistered;
            }

            user = FindUser(userCredentials);

            AddNewUserGames(user);

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
                client.ForceReceive();
                string command = client.ReadData(5);
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
            Debug.Log("Client successfully added", ConsoleColor.Blue);
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
                                default:
                                    NetworkEngine.WriteError("Category " + category + " not found, error in file on line " + (i+1).ToString());
                                    return false;
                                    break;
                            }
                            lastCat = -1;
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

        int port = 15332;
        int downloadEnginePort = 5592;

        /// <summary>
        /// Loads config, Loads games and starts New client thread
        /// </summary>
        public StoreServer()
        {
            users = new List<User>();
            LoadConfig();
            LoadGamesFromFile("games.conf");

            DisplayGames();

            Debug.Log("Starting server on port " + port + "...");
            socket = new NetworkEngine(port);
            socket.addUserFunction = new NetworkEngine.NewClientFunction(AddClient);

            DownloadsManager downloadsManager = new DownloadsManager(downloadEnginePort);
        }
    }
}
