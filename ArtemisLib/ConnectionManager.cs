using System;
using System.Configuration;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using log4net;
using Polly;

namespace ArtemisLib
{
    class ConnectionManager
    {
        private static string DEFAULT_SUBSCRIPTION_NAME = "test-subscription";
        private Session MqSession { get; set; }

        private Connection Connect { get; set; }

        private bool _delayed = false;

        private readonly string _address;

        public bool IsUnicast { get; set; }

        public void SetupConnection(bool delayed = false)
        {
            _delayed = delayed;
            Task.Run(async () => { await GetSession(); });
        }

        private void Receiver_Closed(IAmqpObject sender, Error error)
        {
            WriteLine("Receiver closed, renewing session");

            Task.Run(async () => { await GetSession(); });
        }

        private static int _formatter;
        private async Task GetSession()
        {
            var delayed = Policy.Handle<AmqpException>().Or<SocketException>().Or<TimeoutException>().WaitAndRetryAsync(4, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timespan, retries, context) =>
                {
                    WriteLine($"Received {exception.GetType()} {exception.Message}");
                    WriteLine($"Attempting a connection. Trial after a delay of {timespan.Seconds} seconds");
                });

            var fallBack = Policy.Handle<AmqpException>().Or<SocketException>().Or<TimeoutException>().WaitAndRetryForeverAsync(
                retryAttempt => TimeSpan.FromSeconds(10),
                (exception, timespan) =>
                {
                    WriteLine($"Received {exception.GetType()} {exception.Message}");
                    WriteLine($"Fallback retries. Attempting a connection. Trial after a delay of {timespan.Seconds} seconds");
                });

            var policy = Policy.WrapAsync(fallBack, delayed);


            await policy.ExecuteAsync(async () => await RenewSession());

        }

        private async Task RenewSession()
        {
            string amqpUrl = ConfigurationManager.AppSettings["subscriberBrokerUri"];

            //Trace.TraceLevel = TraceLevel.Frame;
            //Trace.TraceListener += (level, format, args) =>
            //{
            //    WriteLine($"{level}, {format}, args");
            //    foreach (var o in args)
            //    {
            //        WriteLine(o.ToString());
            //    }
            //};
            Dispose();
            var conn = new ConnectionFactory();
            conn.AMQP.MaxFrameSize = 64 * 1024;
            //conn.SASL.Profile = SaslProfile.External; 

            //Connect = conn.CreateAsync(new Address(amqpUrl), new Open() { ContainerId = "client-001" });
            Connect = conn.CreateAsync(new Address(amqpUrl)).Result;
            MqSession = new Session(Connect);
            var receiver = new ReceiverLink(MqSession, DEFAULT_SUBSCRIPTION_NAME, GetSource(_address), null);

            receiver.Start(1, _onMessage);
            receiver.Closed += Receiver_Closed;
            WriteLine("Established a connection with the broker");
        }

        private Source GetSource(string address)
        {
            var source = new Source
            {
                Address = address,
                ExpiryPolicy = new Symbol("never"),
                Capabilities = IsUnicast ? null : new[] { new Symbol("topic"), new Symbol("shared") },
                // Outcomes = new[] {new Symbol("maxconsumers:-1") },
                Dynamic = false,
                Durable = 2,
                DefaultOutcome = new Modified
                {
                    DeliveryFailed = true,
                    UndeliverableHere = false
                }
            };

            return source;
        }

        private void Dispose()
        {
            if (MqSession != null && !MqSession.IsClosed) MqSession?.Close();
            if (MqSession != null && !MqSession.Connection.IsClosed) MqSession.Connection.Close();
        }

        private string DefaultContainerId { get; }
        private readonly MessageCallback _onMessage;

        public ConnectionManager(string defaultContainerId, MessageCallback onMessage, string address)
        {
            this.DefaultContainerId = defaultContainerId;
            this._onMessage = onMessage;
            _address = address;
        }

        public static void WriteLine(string message)
        {
            if (_formatter++ > 10) _formatter = 0;
            LogManager.GetLogger(typeof(ConnectionManager)).Info(message);
            var space = new string(' ', _formatter);
            Console.WriteLine(space + message);
            Debug.WriteLine(message);
        }

    }
}
