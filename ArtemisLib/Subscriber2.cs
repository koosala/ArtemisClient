using System;
using System.Diagnostics;
using Amqp;
using Amqp.Framing;
using Amqp.Types;

namespace ArtemisLib
{
    public class Subscriber2
    {
        private Session MqSession { get; set; }

        public void SetupConnection()
        {
            string amqpURL = "multicast://admin:admin@localhost:5672";

            Connection connection = new Connection(new Address(amqpURL));
            MqSession = new Session(connection);
            Source source = CreateBasicSource();

            source.Address = "MyTopicAddress";
            //source.DistributionMode = new Symbol();
            source.ExpiryPolicy = new Symbol("never");
            source.Durable = 2;
            source.DistributionMode = new Symbol("copy");

            ReceiverLink receiver = new ReceiverLink(MqSession, "test-subscription", source, null);

            receiver.Start(10, OnMessage);
            //var msg = receiver.Receive();
            //Console.WriteLine("Received: " + msg.Body);
            //MqSession.Close();
        }

        public void OnMessage(IReceiverLink link, Message message)
        {
            //link.Accept(message);
            Console.WriteLine($"Received message {message.Body}");
            Debug.WriteLine($"Received message {message.Body}");
        }

        private static Source CreateBasicSource()
        {
            Source source = new Source();

            // These are the outcomes this link will accept.
            Symbol[] outcomes = new Symbol[] {new Symbol("amqp:accepted:list"),
                new Symbol("amqp:rejected:list"),
                new Symbol("amqp:released:list"),
                new Symbol("amqp:modified:list") };

            // Default Outcome for deliveries not settled on this link
            Modified defaultOutcome = new Modified
            {
                DeliveryFailed = true,
                UndeliverableHere = false
            };

            // Configure Source.
            source.DefaultOutcome = defaultOutcome;
            source.Outcomes = outcomes;

            return source;
        }
    }
}
