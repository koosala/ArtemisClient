using ArtemisLib;
using log4net;

namespace WebClient
{
    public class PreWarmCache : System.Web.Hosting.IProcessHostPreloadClient
    {
        public Subscriber Subscriber = new Subscriber();

        public void Preload(string[] parameters)
        {
            LogManager.GetLogger(GetType()).Info("Renewing the connection by the warming cache");
            Subscriber.SetupConnection("WebClient2", "WebAddress");
        }

    }
}