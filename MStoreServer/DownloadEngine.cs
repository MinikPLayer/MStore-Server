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
                    count++;
                    if(count%500 == 0)
                    {
                        Debug.Log("Sent " + (fs.Position * 100f / (float)fs.Length) + "%");
                    }
                }

                // DISABLED FOR DEBUGGING PURPOSES
                fileContent = binaryReader.ReadBytes((int)(fs.Length - fs.Position));

                client.Send(fileContent, "", false);

               
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

        public UploadStatus SendFile(string filePath, bool threaded = true)
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
                workingThread.Start();

                return UploadStatus.success;
            }
            else
            {
                _SendFile(filePath);

                return UploadStatus.success;
            }


            return UploadStatus.unknownError;
            
        }

        public DownloadEngine(NetworkEngine.Client _client, int _port = -1)
        {
            client = _client;

            port = _port;
        }

       


    }

    public class DownloadsManager
    {
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

        }

        private void TokenDataReceived(string data, NetworkEngine.Client client)
        {
            if(data.StartsWith("TOKEN") && data.Length > "TOKEN".Length)
            {
                string token = data.Remove(0, "TOKEN".Length);
                UserAcceptStates status = AcceptUser(client, token);
                switch (status)
                {
                    case UserAcceptStates.unknown:
                        Debug.LogError("Unknown error adding user with token \"" + token + "\"");
                        break;
                    case UserAcceptStates.accepted:
                        AddUser(client, token);
                        break;
                    case UserAcceptStates.badToken:
                        Debug.LogError("Bad token: \"" + token + "\"");
                        break;
                    case UserAcceptStates.notConnected:
                        Debug.LogError("Client not connected");
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

        private UserAcceptStates AcceptUser(NetworkEngine.Client client, string token)
        {



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
