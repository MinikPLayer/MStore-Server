using System;

namespace MStoreServer
{
    class Program
    {
        
        static void Main(string[] args)
        {
            

            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("Starting server...");


            //Debug.Log("Disabled StoreServer for debuggin purposes");
            StoreServer server = new StoreServer();

            //Debug.Log("Starting test upload");
            //TestDownloadEngine test = new TestDownloadEngine(5592, "test.bmp");

        }
    }
}
