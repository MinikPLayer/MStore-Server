using System;
using System.Collections.Generic;
using System.Text;

using System.IO.Pipes;

using System.Threading;

namespace MStoreServer
{
    public class MPipe
    {
        public NamedPipeServerStream pipeServer;

        public Thread serverThread;

        private void ServerThread()
        {
            while(true)
            {
                pipeServer.WaitForConnection();
            }
        }

        public MPipe()
        {
            pipeServer = new NamedPipeServerStream("MPipe", PipeDirection.In);

            serverThread = new Thread(ServerThread);
            serverThread.Start();
        }
    }
}
