using System;
using System.Collections.Generic;
using System.Text;

using System.Net.Sockets;
using System.Net;

using System.Threading;

using System.IO;

namespace MStoreServer
{
    
    public class NetworkEngine
    {
        public class Client : IDisposable
        {
            public bool active = true;

            public string notes = "";

            public Thread thread;
            private bool suspendReceivingThread = false;

            public TcpClient socket;
            public string receiveData = "";

            public delegate void DataFunction(string data, Client client);
            public DataFunction dataReceivedFunction = null;

            NetworkStream stream;

            public bool disposed
            {
                get; private set;
            }
                

            /// <summary>
            /// Disposes client
            /// </summary>
            public void Dispose()
            {
                if (disposed) return;

                socket.Dispose();

                disposed = true;
            }

            /// <summary>
            /// Low level string data send ( without any checks or sth )
            /// </summary>
            /// <param name="data">String to send</param>
            /// <returns></returns>
            protected bool Send_LowLevel(string data)
            {
                byte[] sendBytes = Encoding.UTF8.GetBytes(data);

                return Send_LowLevel(sendBytes);
            }

            /// <summary>
            /// Low level byte array data send ( without any checks or sth )
            /// </summary>
            /// <param name="data">Byte array of data</param>
            /// <returns></returns>
            protected bool Send_LowLevel(byte[] data)
            {
                if (!active || disposed) return false;
                
                try
                {
                    

                    //byte[] sendBytes = System.Text.Encoding.ASCII.GetBytes(data);
                    //Encoding.
                    try
                    {
                        stream.Write(data, 0, data.Length);
                    }
                    catch (System.IO.IOException e)
                    {
                        NetworkEngine.WriteError(e.Message);
                        socket.Close();
                        active = false;
                        return false;
                    }
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogError(e.Message + " at " + e.Source + " in Send_LowLevel()");
                    socket.Close();
                    active = false;
                    return false;
                }
                return true;
            }

            const byte maxPacketSize = 250;
            /// <summary>
            /// Packet send ( messageCode, size and data )
            /// </summary>
            /// <param name="data">Byte array to send</param>
            /// <param name="messageCode">Message code string ( 5 chars )</param>
            /// <param name="requireConfirmation">If true, it's requeired for user to send confirmation packet ( reallllllyyyy sloooow )</param>
            public void Send(byte[] data, string messageCode, bool requireConfirmation = false)
            {
                if (messageCode.Length != 5 && messageCode.Length != 0)
                {
                    Debug.LogError("Message Code is not 5 letters long, canceling send");
                    return;
                }

                if (messageCode.Length != 0)
                {
                    
                    if (!Send_LowLevel(messageCode))
                    {
                        //Debug.LogWarning("Message code send low level returned false");
                        return;
                    }
                }

                if(data.Length == 0)
                {
                    //data = new byte[1];
                    //data[0] = 0;
                    return;
                }
                byte[] array;

                if (data.Length < maxPacketSize + 1)
                {
                    array = new byte[1];
                    array[0] = (byte)data.Length;
                }
                else
                {
                    int addon = 1;
                    if (data.Length % maxPacketSize == 0) addon = 0; // Yeah, you need that because array would be too big
                    array = new byte[data.Length / maxPacketSize  + addon];

                    int len = data.Length;

                    int it = 0;
                    while(len > maxPacketSize)
                    {
                        array[it] = 0;
                        len -= maxPacketSize;

                        it++;
                    }
                    // Command to expect more packets
                    if (len == maxPacketSize)
                    {
                        len = 255;
                    }

                    array[it] = (byte)len;
                }
                    
                    

                //Size
                if (!Send_LowLevel(array))
                {
                    return;
                }

                byte[] sendData;

                sendData = new byte[data.Length];
                for(int i = 0;i<data.Length;i++)
                {
                    sendData[i] = data[i];
                }

                if (!Send_LowLevel(sendData))
                {
                    return;
                }

                if (data.Length > maxPacketSize)
                {
                    byte[] newData = new byte[data.Length - maxPacketSize];
                    for(int i = maxPacketSize; i<data.Length;i++)
                    {
                        newData[i - maxPacketSize] = data[i];
                    }

                    data = newData;
                }
                else
                {
                    data = new byte[0];
                }

                if (requireConfirmation)
                {
                    //Wait for packet receive confirmation
                    Receive_LowLevel(1, true, false);
                }

            }

            /// <summary>
            /// Send packet to user ( message code, size and data )
            /// </summary>
            /// <param name="data">Data to send</param>
            /// <param name="messageCode">5 chars message code</param>
            public void Send(string data, string messageCode)
            {
                if(messageCode.Length != 5)
                {
                    Debug.LogError("Message Code is not 5 letters long, canceling send");
                    return;
                }

                if (!Send_LowLevel(messageCode))
                {
                    Debug.LogWarning("Send low level returned false");
                    return;
                }
                if(data.Length == 0)
                {
                    data = "\0";
                }
                while (data.Length != 0)
                {
                    

                    string size = "";


                    //size += (char)(data.Length % 256);
                    if(data.Length < maxPacketSize + 1)
                    {
                        size += (char)(data.Length);
                    }
                    else
                    {
                        size += (char)maxPacketSize;
                    }
                    if(!Send_LowLevel(size))
                    {
                        Debug.LogWarning("Send low level returned false");
                        return;
                    }
                    string sendData = data;
                    if (data.Length >= maxPacketSize + 1)
                    {
                        sendData = data.Remove(maxPacketSize + 1);
                    }

                    if (!Send_LowLevel(sendData))
                    {
                        Debug.LogWarning("Send low level returned false");
                        return;
                    }

                    if (data.Length > maxPacketSize)
                    {
                        data = data.Remove(0, maxPacketSize);
                    }
                    else
                    {
                        data = data.Remove(0);
                    }
                    
                }
            }

            string __ReceiveData = "";
            /// <summary>
            /// Receives data ( with size )
            /// </summary>
            /// <param name="length">Lenght of data to receive, -2 to get size from stream</param>
            /// <param name="forceReceive">If true, skip waiting time when packet is fragmented ( High CPU usage )</param>
            /// <param name="looping">If true, the function is looping</param>
            /// <param name="timeout">Receive timeout</param>
            /// <returns></returns>
            public string Receive_LowLevel(int length, bool forceReceive, bool looping, int timeout = -1)
            {
                if (!active || disposed) return "\0";
                try
                {
                    while (socket == null)
                    {
                        Thread.Sleep(150);
                    }
                    Mutex mtx = new Mutex();
                    int receiveTryCount = 0;
                    while (receiveTryCount < timeout || timeout < 0)
                    {
                        try
                        {
                            NetworkStream stream = socket.GetStream();
                            while (!stream.DataAvailable && __ReceiveData.Length == 0)
                            {
                                Thread.Sleep(10);
                                if (!active) return "\0";
                                while (suspendReceivingThread && !forceReceive)
                                {
                                    Thread.Sleep(20);
                                }
                                receiveTryCount++;
                                if (receiveTryCount >= timeout && timeout > 0)
                                {
                                    break;
                                }
                            }
                            //string rData = "";

                            if (length == -2)
                            {
                                

                                if(__ReceiveData.Length != 0)
                                {
                                    length = (byte)__ReceiveData[0];

                                    __ReceiveData = __ReceiveData.Remove(0, 1);
                                }
                                else
                                {

                                    byte[] sizeBuffer = new byte[1];
                                    while(stream.Read(sizeBuffer, 0, 1) <= 0)
                                    {
                                        Thread.Sleep(50);
                                    }
                                    length = sizeBuffer[0];

                                }

                                while (!stream.DataAvailable && __ReceiveData.Length == 0)
                                {
                                    Thread.Sleep(10);
                                    if (!active) return "\0";
                                    while (suspendReceivingThread && !forceReceive)
                                    {
                                        Thread.Sleep(20);
                                    }
                                    receiveTryCount++;
                                    if (receiveTryCount >= timeout && timeout > 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            /*if(rData != null && rData.Length != 0)
                            {
                                __ReceiveData += rData;
                            }*/

                            /*if(rData != null && rData.Length != 0)
                            {
                                __ReceiveData += rData;

                                string returnData = "";
                                for(int i = 0;i<rData.Length && length != 0;i++)
                                {
                                    returnData += rData[i];
                                    length--;
                                }

                                __ReceiveData = __ReceiveData.Remove(0, returnData.Length);

                                if (length == 0) return returnData;
                            }
                            if(__ReceiveData != null && __ReceiveData.Length != 0)
                            {

                            }*/

                            byte[] buffer = new byte[256];
                            mtx.WaitOne();
                            //__ReceiveData = "";
                            Int32 bytesReceived = 0;
                            while ((stream.DataAvailable && (bytesReceived < length || length == -1)))
                            {
                                if (!active) return "\0";
                                if (length == -1) length = 255;

                                try
                                {
                                    Int32 bytesCount = stream.Read(buffer, 0, length);
                                    __ReceiveData += System.Text.Encoding.ASCII.GetString(buffer, 0, bytesCount);
                                    bytesReceived += bytesCount;
                                }
                                catch(ArgumentOutOfRangeException e)
                                {
                                    Debug.LogError(e.Message + " at " + e.Source + " in Receive_LowLevel.while (stream.DataAvailable && (bytesReceived < length || length == -1))");
                                }
                                catch(Exception e)
                                {
                                    Debug.LogError(e.Message + " at " + e.Source + " in Receive_LowLevel.while (stream.DataAvailable && (bytesReceived < length || length == -1))");
                                    return "\0";
                                }
                            }
                        }
                        catch (ObjectDisposedException e)
                        {
                            Debug.LogWarning(e.Message + " at " + e.Source + " in Receive_LowLevel()");
                            active = false;
                            return "\0";
                        }
                        catch(Exception e)
                        {
                            Debug.LogWarning(e.Message + " at " + e.Source + " in Receive_LowLevel()");
                            active = false;
                            return "\0";
                        }

                        //Console.WriteLine("Readed data: " + __ReceiveData);
                        mtx.ReleaseMutex();

                        if (dataReceivedFunction != null) dataReceivedFunction.Invoke(__ReceiveData, this);

                        if (!looping) break;

                        if (length <= __ReceiveData.Length && length > 0)
                        {
                            __ReceiveData = __ReceiveData.Remove(0, length);
                        }

                        length = -2;
                    }
                }
                catch(ThreadInterruptedException e)
                {
                    Debug.LogWarning(e.Message + " at " + e.Source);
                }
                
                string returnData = "";
                if (length == -1)
                {
                    returnData = __ReceiveData;

                    __ReceiveData = "";
                    return returnData;
                }

                
                for(int i = 0;i<length && __ReceiveData.Length != 0;i++)
                {
                    returnData += __ReceiveData[i];
                    __ReceiveData = __ReceiveData.Remove(0, 1);
                    i--;
                    length--;
                }

                return returnData;
            }

            /// <summary>
            /// Receives data and clears old data
            /// </summary>
            /// <param name="clearReceive">If true, clears old receive buffer</param>
            /// <param name="forceReceive">If true, skip waiting time when packet is fragmented ( High CPU usage )</param>
            /// <param name="looping">If true, the function is looping</param>
            /// <param name="timeout">Receive timeout</param>
            protected void Receive(bool forceReceive, bool looping, int timeout = -1, bool clearReceive = true)
            {
                
                if(clearReceive)
                {
                    receiveData = "";
                }
                receiveData = Receive_LowLevel(-2, forceReceive, looping, timeout);
            }

            protected void Receive()
            {
                Receive(false, true);
            }

            /// <summary>
            /// Stops receiving thread and receives data
            /// </summary>
            /// <param name="timeout">Receive timeout ( -1 = infinite )</param>
            public void ForceReceive(int timeout = -1)
            {
                DataFunction functionBackup = dataReceivedFunction;
                dataReceivedFunction = null;

                suspendReceivingThread = true;

                Receive(true, false, timeout);
                dataReceivedFunction = functionBackup;

                suspendReceivingThread = false;

                
            }

            /// <summary>
            /// Reads data from string buffer and clears it
            /// </summary>
            /// <param name="bytesToRead">-1 to infinite, any other to read only specified amount of chars</param>
            /// <returns></returns>
            public string ReadData(int bytesToRead = -1)
            {
                if(!socket.Connected)
                {
                    active = false;
                }

                //string data = receiveData;
                //receiveData = "";
                string data = "";
                if(bytesToRead >= receiveData.Length || bytesToRead == -1)
                {
                    data = receiveData;

                    receiveData = "";
                }
                else
                {
                    data = receiveData.Remove(bytesToRead);
                    receiveData = receiveData.Remove(0, bytesToRead);
                }

                return data;
            }

            /// <summary>
            /// Reads data until reaches endingChar and returns string WITHOUT it. If char is not present, returns full string
            /// </summary>
            /// <param name="endingChar"></param>
            /// <returns></returns>
            public string ReadData(char endingChar)
            {
                if (!socket.Connected)
                {
                    active = false;
                }

                //string data = receiveData;
                //receiveData = "";
                string data = "";

                int index = -1;
                for(int i = 0;i<receiveData.Length;i++)
                {
                    if(receiveData[i] == endingChar)
                    {
                        index = i;
                        break;
                    }

                    
                }
                if (index != -1)
                {
                    data = receiveData.Remove(index);
                    receiveData = receiveData.Remove(0, index);
                }
                else
                {
                    data = receiveData;
                    receiveData = "";
                }

                return data;
            }

            public Client(TcpClient client, bool createReceivingThread = true, bool startReceivingThread = true)
            {
                socket = client;
                stream = socket.GetStream();

                if (createReceivingThread)
                {
                    thread = new Thread(() => Receive_LowLevel(-2, false, true));
                    if (startReceivingThread)
                    {
                        thread.Start();
                    }
                }

                

                Send("WP", "CMMND"); // Welcome packet
            }

            ~Client()
            {
                thread.Interrupt();
                socket.Close();          
            }
        }

        /*public class FileTransferClient
        {
            public bool sendingFile { get; private set; }

            Thread fileTransferThread;

            public string filePath { get; private set; }

            public IPAddress address { get; private set; }
            public int port { get; private set; }

            public IPEndPoint endPoint { get; private set; }


            Socket socket;

            private void _SendFile(string filePath)
            {
                socket.SendFile(filePath);

                sendingFile = false;
            }

            public void SendFile(string _filePath)
            {
                

                if(sendingFile)
                {
                    Debug.LogWarning("Already sending file, cannot start new file send");
                    return;
                }

                if(!File.Exists(_filePath))
                {
                    Debug.LogError(_filePath + " doesn't exist");
                    return;
                }

                filePath = _filePath;

                sendingFile = true;

                fileTransferThread = new Thread(() => _SendFile(filePath));
                fileTransferThread.Start();
            }

            public FileTransferClient(string _address, int _port)
            {
                //address = _address;
                IPAddress iPAddress;
                if(IPAddress.TryParse(_address, out iPAddress))
                {
                    address = iPAddress;
                }
                else
                {
                    Debug.LogError("Cannot parse \"" + _address + "\" to IPAddress");
                    return;
                }
                port = _port;

                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                endPoint = new IPEndPoint(address, port);

                

                try
                {
                    socket.Connect(endPoint);

                    
                }
                catch(Exception e)
                {
                    Debug.LogError("Error at FileTransferClient(): " + e.Message);
                    //return;
                    try
                    {
                        

                        socket.Bind(new IPEndPoint(IPAddress.Any, port));

                        

                        //socket = socket.Accept();
                    }
                    catch(Exception er)
                    {
                        Debug.LogError("Error accepting connection: " + er.Message);
                        return;
                    }
                }
            }
        }*/

        private TcpListener socket = null;
        private IPAddress localIP = null;
        private int port = -1;

        public List<Client> clients;

        private Thread clientAcceptThread;
        private Thread manageClientsThread;

        public delegate void NewClientFunction(Client newClient);
        public NewClientFunction addUserFunction;

        public static void WriteError(object error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error.ToString());
            Console.ForegroundColor = ConsoleColor.White;
        }



        

        private void AddClient(Client newClient)
        {
            clients.Add(newClient);

            if(addUserFunction != null)
            {
                addUserFunction.Invoke(newClient);
            }
        }

        private void DeleteClient(Client client)
        {
            Console.WriteLine("Client deleted");
            client.thread.Interrupt();
            client = null;
            StoreServer.DeleteUser(ref client);
        }

        public NetworkEngine(int _port, string _address = "127.0.0.1")
        {
            port = _port;
            bool result = IPAddress.TryParse(_address, out localIP);
            if(result == false)
            {
                WriteError("Cannot parse IP address");

                port = -1;
                localIP = null;
                return;
            }

            clients = new List<Client>();

            StartListen();
        }

        private void StartListen()
        {
            Console.WriteLine("Creating socket...");

            try
            {
                //socket = new TcpListener(localIP, port);
                socket = new TcpListener(IPAddress.Any, port);
            } catch(ArgumentException e)
            {
                WriteError(e);
                return;
            }

            Console.WriteLine("Starting listening...");

            clientAcceptThread = new Thread(ListenEngine);
            clientAcceptThread.Start();

            manageClientsThread = new Thread(ManageClients);
            manageClientsThread.Start();
        }

        private void ManageClients()
        {
            
            while(true)
            {
                for(int i = 0;i<clients.Count;i++)
                {
                    if (clients[i].socket.Connected && clients[i].active) continue;
                    //DeleteClient(clients[i]);
                    clients[i].thread.Interrupt();
                    clients.RemoveAt(i);
                    i--;
                }

                Thread.Sleep(500);
            }
        }

        private void ListenEngine()
        {
            socket.Start();

            while(true)
            {
                Console.WriteLine("Waiting for connection...");

                Client client = new Client(socket.AcceptTcpClient(), true, false);
                Debug.Log("Connection incoming");
                if(client != null)
                {
                    AddClient(client);
                }
            }
        }
    }
}
