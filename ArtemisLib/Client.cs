using System;
using System.Configuration;
using System.Net.Sockets;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Polly;

namespace ArtemisLib
{
    public class Client
    {
        private Session MqSession { get; set; }

        private string DefaultContainerId { get; }

        public Client(string defaultContainerId)
        {
            DefaultContainerId = defaultContainerId;
            GetSession().Wait();
        }

        public async Task RenewSession()
        {
            string amqpUrl = ConfigurationManager.AppSettings["publisherBrokerUri"];

            //Connection connection = new Connection(new Address(amqpURL));
            //var factory = new ConnectionFactory();
            var connection = await Connection.Factory.CreateAsync(new Address(amqpUrl), 
                new Open() { ContainerId = DefaultContainerId });
            MqSession = new Session(connection);
        }

        public async Task SendMessage(string addressName)
        {
            string message = $"Hello AMQP! at time {DateTime.Now}";
            SenderLink sender = new SenderLink(MqSession, "Client", addressName);
            Message message1 = new Message(message);
            await sender.SendAsync(message1);
            await sender.CloseAsync();
            Console.WriteLine("Sent a message: " + message);
        }

        private async Task GetSession()
        {
            var delayed = Policy.Handle<AmqpException>().Or<SocketException>().Or<TimeoutException>().WaitAndRetryAsync(4, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timespan, retries, context) =>
                {
                    Console.WriteLine($"Received {exception.GetType()} {exception.Message}");
                    Console.WriteLine($"Attempting a connection. Trial after a delay of {timespan.Seconds} seconds");
                });

            var fallBack = Policy.Handle<AmqpException>().Or<SocketException>().Or<TimeoutException>().WaitAndRetryForeverAsync(
                retryAttempt => TimeSpan.FromSeconds(10),
                (exception, timespan) =>
                {
                    Console.WriteLine($"Received {exception.GetType()} {exception.Message}");
                    Console.WriteLine($"Fallback retries. Attempting a connection. Trial after a delay of {timespan.Seconds} seconds");
                });

            var policy = Policy.WrapAsync(fallBack, delayed);


            await policy.ExecuteAsync(async () => await RenewSession());

        }

    }
}
