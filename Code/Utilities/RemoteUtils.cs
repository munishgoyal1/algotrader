
using System;
using System.Net;
using System.Net.NetworkInformation;
using HttpLibrary;
using StockTrader.Platform.Logging;
using System.Text;

namespace StockTrader.Utilities
{
    public enum RemoteCommand
    {
        SOSEXIT,
        EXITALL,

        PAUSEALL,
        RESUMEALL,
        STOPALL,
        PAUSENEWPOSALL,
        SQUAREOFFALL,
        SQUAREOFFPAUSEALL,
        SQUAREOFFATPROFITALL,
        SQUAREOFFATPROFITPAUSEALL,
        SQUAREOFFATPROFITRESETALL,
        RESETFULLALL,
        RESETCOREALL,
        RESETPOSALL,
        RESETDIRALL,

        GETSTATEALL,

        PAUSE,
        RESUME,
        STOP,
        PAUSENEWPOS,
        SQUAREOFF,
        SQUAREOFFPAUSE,
        SQUAREOFFATPROFIT,
        SQUAREOFFATPROFITPAUSE,
        SQUAREOFFATPROFITRESET,
        RESETFULL,
        RESETCORE,
        RESETPOS,
        RESETDIR
    }

    //string s = Enum.GetName(typeof(Shipper), x);

    public enum ProgramRemoteControl
    {
        RUN,
        PAUSE,
        STOP,
        HIBERNATE,
        HIBERNATEATEND
    }

    public static class RemoteUtils
    {

        public static bool CheckPingResponse()
        {
            string hostname = "google.com";
            //Ping netMon = new Ping();
            //IPAddress address = Dns.GetHostEntry(hostname).AddressList[0];
            //PingReply pr = netMon.Send(hostname);
            //if (pr.Status != IPStatus.Success)
            //    return false;
            //else return true;

            Ping pingSender = new Ping ();
            PingOptions options = new PingOptions ();

            // Use the default Ttl value which is 128,
            // but change the fragmentation behavior.
            options.DontFragment = true;

            // Create a buffer of 32 bytes of data to be transmitted.
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = Encoding.ASCII.GetBytes (data);
            int timeout = 120;
            PingReply reply = pingSender.Send (hostname, timeout, buffer, options);
            if (reply.Status == IPStatus.Success)
                return true;

            return false;
        }

        /// <summary>
        /// Indicates whether any network connection is available
        /// Filter connections below a specified speed, as well as virtual network cards.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if a network connection is available; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNetworkAvailable()
        {
            return IsNetworkAvailable(10000);
        }

        /// <summary>
        /// Indicates whether any network connection is available.
        /// Filter connections below a specified speed, as well as virtual network cards.
        /// </summary>
        /// <param name="minimumSpeed">The minimum speed required. Passing 0 will not filter connection using speed.</param>
        /// <returns>
        ///     <c>true</c> if a network connection is available; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNetworkAvailable(long minimumSpeed)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;
            var networks = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in networks)
            {
                // discard because of standard reasons
                if ((ni.OperationalStatus != OperationalStatus.Up) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel))
                    continue;

                // this allow to filter modems, serial, etc.
                // I use 10000000 as a minimum speed for most cases
                if (ni.Speed < minimumSpeed)
                    continue;

                // discard virtual cards (virtual box, virtual pc, etc.)
                if ((ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (ni.Name.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                return true;
            }
            return false;
        }

        // Not used currently
        public static bool IsSendSystemInHibernation()
        {
            ProgramRemoteControl remoteControlValue = RemoteUtils.GetProgramRemoteControlValue();

            // If Hibernate control specified return true
            return remoteControlValue.Equals(ProgramRemoteControl.HIBERNATE) ||
                   remoteControlValue.Equals(ProgramRemoteControl.HIBERNATEATEND);
        }

        public static ProgramRemoteControl GetProgramRemoteControlValue()
        {
            string pageData = HttpHelper.GetWebPageResponse("http://sites.google.com/site/munishgoyal/",
                null,
                null,
                new CookieContainer());

            ProgramRemoteControl controlValue = ProgramRemoteControl.RUN;
            if (!string.IsNullOrEmpty(pageData))
            {
                if (pageData.Contains("StopKalaPudinaStockTraderPause"))
                {
                    controlValue = ProgramRemoteControl.PAUSE;
                }
                else if (pageData.Contains("StopKalaPudinaStockTraderStop"))
                {
                    controlValue = ProgramRemoteControl.STOP;
                }
                else if (pageData.Contains("StopKalaPudinaStockTraderHibernate"))
                {
                    controlValue = ProgramRemoteControl.HIBERNATE;
                }
                else if (pageData.Contains("StopKalaPudinaStockTraderAtEndHibernate"))
                {
                    controlValue = ProgramRemoteControl.HIBERNATEATEND;
                }
            }
            string traceString = string.Format("GetProgramRemoteControlValue: {0}, {1}", controlValue.ToString(),
                string.IsNullOrEmpty(pageData) ? "Failed Fetch" : "Succesful Contact");
            FileTracing.TraceOut(traceString);

            return controlValue;
        }

        

    }
}
