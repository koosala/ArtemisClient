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
    public class Subscriber
    {
        private string _address;
        private static int _x;
        private string _clientName;
        private Session MqSession { get; set; }

        private Connection Connect { get; set; }

        public void SetupConnection(string clientName, string address)
        {
            _clientName = clientName;
            _address = address;
            Task.Run(async () => { await GetSession(); });
        }

        private void Receiver_Closed(IAmqpObject sender, Error error)
        {
            WriteLine("Receiver closed, renewing session");

            Task.Run(async () => { await GetSession(); });
        }

        private int _formatter;
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
            //ConfigureMessageTrace();
            Dispose();
            Connect = await Connection.Factory.CreateAsync(new Address(amqpUrl), new Open() {ContainerId = "client-001"});
            MqSession = new Session(Connect);
            var receiver = new ReceiverLink(MqSession, _clientName, GetSource(_address), null);

            receiver.Start(1, OnMessage);
            receiver.Closed += Receiver_Closed;
            WriteLine("Established a connection with the broker");
        }

        //private void ConfigureMessageTrace()
        //{
        //    Trace.TraceLevel = TraceLevel.Frame;
        //    Trace.TraceListener += (level, format, args) =>
        //    {
        //        WriteLine($"{level}, {format}, args");
        //        foreach (var o in args)
        //        {
        //            WriteLine(o.ToString());
        //        }
        //    };
        //}

        //private void IgnoreCertValidation()
        //{
        //    ServicePointManager.ServerCertificateValidationCallback =
        //        (s, certificate, chain, sslPolicyErrors) => true;
        //}

        private Source GetSource(string address)
        {
            var source = new Source
            {
                Address = address,
                ExpiryPolicy = new Symbol("never"),
                Capabilities = new[] { new Symbol("topic"), new Symbol("shared") },
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


        public void OnMessage(IReceiverLink link, Message message)
        {
            WriteLine($"Received a message with the content: {message.Body}");
            //if (1 == 1) throw new Exception("Something wrong");
                //WriteLine("Testing sleep " + (++x));
                //Thread.Sleep(100 * 1000);

            WriteLine($"Accepted message number {_x++}");
            link.Accept(message);
            //Console.WriteLine($"Received message {message.Body}");
            //Debug.WriteLine($"Received message {message.Body}");

            //WriteLine($"started a long running job at {DateTime.Now}");
            //for (int i = 0; i < 150; i++)
            //{
            //    Console.Write(i + " ");
            //    //user = container.UserService.GetUserProfile();

            //    var fileContent = File.ReadAllText(@"C:\temp\test.txt");
            //}
            //WriteLine($"finished a long running job at {DateTime.Now}");
        }

        private void WriteLine(string message)
        {
            if (_formatter++ > 10) _formatter = 0;
            LogManager.GetLogger(GetType()).Info(message);
            var space = new string(' ', _formatter);
            Console.WriteLine(space +  message);
            Debug.WriteLine(message);
        }
    }
}

