using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using ArtemisLib;
using log4net;

namespace WebClient
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private Subscriber subscriber = new Subscriber();
        protected void Application_Start()
        {
            LogManager.GetLogger(GetType()).Info("Starting the connection at application start");

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            subscriber.SetupConnection("WebClient1", "WebAddress");
        }
    }
}
