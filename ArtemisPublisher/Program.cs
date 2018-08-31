using System;
using System.Threading;
using ArtemisLib;

namespace ArtemisClient
{
    class Program
    {

        public static string DefaultContainerId = "client-1";
        static void Main(string[] args)
        {
            Console.WriteLine("Enter the target address to publish to: ");
            string addressName = Console.ReadLine();

            while (true)
            {
                DisplayOptions();
                var option = int.Parse(Console.ReadLine());
                switch (option)
                {
                    case 1:
                        new Client(DefaultContainerId).SendMessage(addressName).Wait();
                        break;
                    case 2:
                        int i = 0;
                        var client = new Client(DefaultContainerId);
                        while (true)
                        {
                            client.SendMessage(addressName).Wait();
                            Console.Write($"Iteration: {i++}, ");
                        }
                }

            }
        }

        public static void DisplayOptions()
        {
            Console.WriteLine("The following options are available:");
            Console.WriteLine("1. Send one message to the bus");
            Console.WriteLine("2. Send a continuous set of messages");
            Console.WriteLine("Press ^C to stop");
        }
    }
}
