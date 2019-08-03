using System;
using System.Collections.Generic;
using System.Text;

using System.Net;
using System.Net.Sockets;

using System.Threading;

using System.IO;

namespace MStoreServer
{
    public class DownloadEngine
    {
        NetworkEngine.Client client;

        Thread workingThread;

        /// <summary>
        /// Tells if something is uploading / downloading
        /// </summary>
        public bool working { get; private set; } = false;
        public int port;

        public string token = "";

        public StoreServer.User user;

        private void _SendFile(string filePath)
        {
            //string fileContent = File.ReadAllText(filePath);

            //client.Send(fileContent, "DLOAD");


            //byte[] fileContent = File.ReadAllBytes(filePath);

            /*string fileContentStr = "";

            for(int i = 0;i<fileContent.Length;i++)
            {
                fileContentStr += (char)fileContent[i];
            }*/

            //Convert.






            byte[] fileContent = new byte[255];
            for(int i = 0;i<255;i++)
            {
                fileContent[i] = 124;
            }

            using (FileStream fs = File.OpenRead(filePath))
            {
                var binaryReader = new BinaryReader(fs);
                //fileContent = binaryReader.ReadBytes((int)fs.Length);
                int count = 0;
                while (fs.Position < fs.Length - 255)
                {

                    //byte[] content = binaryReader.ReadBytes(255);
                    // DISABLED FOR DEBUGGING PURPOSES
                    fileContent = binaryReader.ReadBytes(255);



                    client.Send(fileContent, "", false);
                    //Debug.Log("Sent " + fileContent.Length + " bytes");
                    count++;
                    if(count%500 == 0)
                    {
                        Debug.Log("Sent " + (fs.Position * 100f / (float)fs.Length) + "%");
                    }

                    //Thread.Sleep(50);
                }

                // DISABLED FOR DEBUGGING PURPOSES
                fileContent = binaryReader.ReadBytes((int)(fs.Length - fs.Position));
                Debug.Log("Last packet size: " + fileContent.Length);

                client.Send(fileContent, "", false);

                Debug.Log("Sent " + fs.Position + " bytes");
            }

            





            /*Debug.Log("File content: ");

            //string content = "";

            for(int i = 0;i<fileContent.Length;i++)
            {
                //content += (char)fileContent[i];
                Console.WriteLine(fileContent[i].ToString() + " - " + (char)fileContent[i]);
            }*/

            //Debug.Log(content);

            //Debug.Log("Content size: " + fileContent.Length);

            

            working = false;
        }

        public enum UploadStatus
        {
            success,
            alreadyWorking,
            fileDoesntExist,
            unknownError,
        }

        public UploadStatus SendFile(string filePath, bool threaded = true, bool autoStartThread = true)
        {
            if(working)
            {
                Debug.LogWarning("Already uploading / downloading, aborting...");
                return UploadStatus.alreadyWorking;
            }

            if(!File.Exists(filePath))
            {
                Debug.LogError("File " + filePath + " doesn't exist");
                return UploadStatus.fileDoesntExist;
            }

            if(threaded)
            {
                workingThread = new Thread(() => _SendFile(filePath));
                if (autoStartThread)
                {
                    workingThread.Start();
                }

                return UploadStatus.success;
            }
            else
            {
                _SendFile(filePath);

                return UploadStatus.success;
            }


            return UploadStatus.unknownError;
            
        }

        public void DataReceived(string data, NetworkEngine.Client client)
        {
            string response = "UK";
            string responseCode = "UNKNW";

            Console.WriteLine("DownloadEngine: Data received: " + data);
            string command = data;
            if (data.Length > 5)
            {
                command = data.Remove(5);
            }

            data = data.Remove(0, 5);

            switch (command)
            {
                case "OK":
                    return;
                case "DWNLD":
                    if(user == null)
                    {
                        response = "NA";
                        responseCode = "ERROR";
                        break;
                    }
                    if(data.Length == 0)
                    {
                        response = "NF";
                        responseCode = "ERROR";
                        break;
                    }
                    Int64 id = -1;
                    if(!Int64.TryParse(data, out id))
                    {
                        Debug.LogError("Cannot parse \"" + data + "\" to Int64");

                        response = "NF";
                        responseCode = "ERROR";
                        break;
                    }

                    StoreServer.Game game = StoreServer.FindGame(id);

                    if(game == null)
                    {
                        response = "NF";
                        responseCode = "ERROR";
                        break;
                    }

                    if(!StoreServer.CheckIfUserHaveGame(user, game))
                    {
                        response = "NA";
                        responseCode = "ERROR";
                        break;
                    }

                    

                    UploadStatus status = SendFile(DownloadsManager.downloadFilesDirectory + game.filename, true, false);
                    switch (status)
                    {
                        case UploadStatus.success:
                            Debug.Log("File sent successfully");
                            client.Send("OK", "DWNLD");
                            workingThread.Start();

                            return;
                        case UploadStatus.alreadyWorking:
                            Debug.Log("Thread is busy");
                            break;
                        case UploadStatus.fileDoesntExist:
                            response = "NF";
                            responseCode = "ERROR";
                            break;
                        case UploadStatus.unknownError:
                            
                            break;
                        default:
                            break;
                    }

                    break;
                default:
                    response = "NF";
                    responseCode = "ERROR";
                    break;
            }
            Debug.LogWarning("Sending response ");
            client.Send(response, responseCode);

        }

        public DownloadEngine(NetworkEngine.Client _client, int _port = -1, string _token = "")
        {
            client = _client;

            port = _port;

            token = _token;
 

            user = StoreServer.FindUserByToken(token);

            client.dataReceivedFunction = DataReceived;
        }

       


    }

    public class DownloadsManager
    {
        public static string downloadFilesDirectory = "./downloadFiles/";

        public int port = -1;
        Thread listenThread;

        public List<DownloadEngine> downloadClients = new List<DownloadEngine>();


        private enum UserAcceptStates
        {
            unknown,
            accepted,
            badToken,
            notConnected,
        }

        private void AddUser(NetworkEngine.Client client, string token)
        {
            client.Send("OK", "DAUTH");
            DownloadEngine newDownloadClient = new DownloadEngine(client, port, token);
        }

        private void UserAddError(NetworkEngine.Client client, UserAcceptStates status)
        {
            switch (status)
            {
                case UserAcceptStates.unknown:
                    client.Send("UN", "ERROR");
                    break;
                case UserAcceptStates.badToken:
                    client.Send("BT", "ERROR");
                    break;
                default:
                    break;
            }
        }

        private void TokenDataReceived(string data, NetworkEngine.Client client)
        {
            if(data.StartsWith("TOKEN") && data.Length > "TOKEN".Length)
            {
                string token = data.Remove(0, "TOKEN".Length);
                UserAcceptStates status = AcceptUser(token);
                switch (status)
                {
                    case UserAcceptStates.unknown:
                        Debug.LogError("Unknown error adding user with token \"" + token + "\"");
                        UserAddError(client, UserAcceptStates.unknown);
                        break;
                    case UserAcceptStates.accepted:
                        AddUser(client, token);
                        break;
                    case UserAcceptStates.badToken:
                        Debug.LogError("Bad token: \"" + token + "\"");
                        UserAddError(client, UserAcceptStates.badToken);
                        break;
                    case UserAcceptStates.notConnected:
                        Debug.LogError("Client not connected");
                        UserAddError(client, UserAcceptStates.notConnected);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                Debug.LogWarning("Message is not token");
            }
        }

        private UserAcceptStates AcceptUser(string token)
        {
            StoreServer.User user = StoreServer.FindUserByToken(token);
            if(user != null)
            {
                return UserAcceptStates.accepted;
            }
            else
            {
                return UserAcceptStates.badToken;
            }


            return UserAcceptStates.unknown;
        }

        private void ListenForConnections()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, port);

            listener.Start();

            while (true)
            {
                NetworkEngine.Client client = new NetworkEngine.Client(listener.AcceptTcpClient(), true);
                client.dataReceivedFunction = TokenDataReceived;
                //UserAcceptStates state = AcceptUser(client);
            }
        }

        public DownloadsManager(int listenPort)
        {
            port = listenPort;

            listenThread = new Thread(ListenForConnections);
            listenThread.Start();
        }
    }

    public class TestDownloadEngine
    {
        public TestDownloadEngine(int port, string filePath)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, port);

            listener.Start();

            NetworkEngine.Client client = new NetworkEngine.Client(listener.AcceptTcpClient(), false);

            Debug.Log("Connection incoming");

            if(client != null)
            {
                DownloadEngine downloadEngine = new DownloadEngine(client);


                Debug.Log("Connetion established, trying to send file");
                DownloadEngine.UploadStatus status = downloadEngine.SendFile(filePath);
                switch (status)
                {
                    case DownloadEngine.UploadStatus.success:
                        Debug.Log("Success");
                        break;
                    case DownloadEngine.UploadStatus.alreadyWorking:
                        Debug.LogError("Already working");
                        break;
                    case DownloadEngine.UploadStatus.fileDoesntExist:
                        Debug.LogError("File doesn't exist");
                        break;
                    case DownloadEngine.UploadStatus.unknownError:
                        Debug.LogError("Unknown Error");
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
