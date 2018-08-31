using System.IO;
using Amqp;

namespace ArtemisLib
{
    public class FileProcessor
    {
        private readonly ConnectionManager _manager;

        public FileProcessor()
        {
            _manager = new ConnectionManager("ABCD", OnMessage, "POC.ELNIncomingQueue")
            { IsUnicast = true };
        }

        public void WatchForFiles()
        {
            _manager.SetupConnection();
        }

        public void OnMessage(IReceiverLink link, Message message)
        {
            ConnectionManager.WriteLine($"Called, and using the message: {message.Body}");

            ConnectionManager.WriteLine($"Accepting message number ..please wait");

            var content = message.Body;

            var bytes = (byte[]) content;

            File.WriteAllBytes($@"C:\Temp\ELNOutput\{message.ApplicationProperties["CamelFileName"]}", bytes);
            link.Accept(message);
        }

    }
}
