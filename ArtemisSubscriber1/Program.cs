using System;
using ArtemisLib;

namespace ArtemisSubscriber1
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World! from subscriber 1");
            Console.WriteLine("Please enter the client name and the address to subscribe to, separated by a comma:");
            var val = Console.ReadLine()?.Split(',');
            var clientName = val?.Length > 0 ? val[0].Trim() : String.Empty;
            var address = val?.Length > 1 ? val[1].Trim() : String.Empty;

            if (!string.IsNullOrEmpty(clientName) && !string.IsNullOrEmpty(address))
                new Subscriber().SetupConnection(clientName, address);
            else
                Console.WriteLine($"Client Name read: {clientName} or address: {address} cannot be empty");
            Console.ReadLine();
        }
    }
}
