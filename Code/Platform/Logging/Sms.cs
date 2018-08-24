using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using HtmlParsingLibrary;
using HttpLibrary;
using StockTrader.Platform.Logging;

namespace MailChatSms
{
    [Serializable()]
    public class W2SContact
    {
        public string Name;
        public string MobileNumber;
        public string Group;

        public string ToString()
        {
            return Name + " " + MobileNumber;
        }
    }

    [Serializable()]
    public class W2SAddressBook
    {
        public List<W2SContact> Contacts = new List<W2SContact>();
        public Dictionary<string, List<W2SContact>> ContactGroups = new Dictionary<string, List<W2SContact>>();
    }


    public class Way2SmsAPI
	{
        //string URL_W2S_LOGIN = "http://site6.way2sms.com/auth.cl";
        string URL_W2S_LOGIN = "http://site4.way2sms.com/Login1.action";
        //string URL_W2S_LOGIN_REFERER = "http://site6.way2sms.com/content/index.html";
	    string URL_W2S_LOGIN_REFERER = "http://site4.way2sms.com/content/index.html";
        //username	99999999999
        //password	0000


        public W2SAddressBook AddressBook = new W2SAddressBook();

        //http://site4.way2sms.com/Main.action?id=
        //http://site4.way2sms.com/jsp/DashBoard.jsp
        //http://site4.way2sms.com/QuickContacts  POST folder          DashBoard  

        // After login, resulting url is of the form as below
        //http://site6.way2sms.com/./jsp/Main.jsp?id=CFB24FF4EF2CCCF70AA8F661AF396EDF.w810
        //http://site6.way2sms.com/jsp/Main.jsp?id=10256EDCF0631F9C0FAA5A21EEB3FC4A.w803

        // Get Action Value Url
        string URL_W2S_GETACTION = "http://site4.way2sms.com/jsp/InstantSMS.jsp";
        // Action parse string
        // <input type="hidden" name="Action" id="Action" value="sdf5445fdg" />


        // Now to send sms
        //string URL_W2S_QUICKSMS = "http://site6.way2sms.com/FirstServletsms";
        string URL_W2S_QUICKSMS = "http://site4.way2sms.com/quicksms.action";
        //string URL_W2S_QUICKSMS_REFERER = "http://site6.way2sms.com/jsp/InstantSMS.jsp";
	    string URL_W2S_QUICKSMS_REFERER = "http://site4.way2sms.com/jsp/InstantSMS.jsp";
        //HiddenAction    instantsms                                                 
        //catnamedis      Birthday                                                   
        //Action          sdf5445fdg                                                 
        //chkall          on                                                         
        //MobNo           9199999999999                                                 
        //textArea        helllllooo fejkeww,f fwf,we fw,g ,ggf w,f,.f'';;'er;q'q;w  
        //guid            username                                                   
        //gpwd            *******                                                    
        //yuid            username                                                   
        //ypwd            *******                                                    


        // Future sms
        //http://site6.way2sms.com/ScheduleSMS
        //schnAction      schinsert          
        //mobno           919999999999       
        //smsg            its future sms...  
        //sdate           2011/06/29         
        //stime           11:45~PM       
    
        //http://site6.way2sms.com/jsp/sheduleconfirm.jsp
        //schmobile       99999999999         
        //schtext         its future sms...  
        //schhour         11                 
        //schminute       45                 
        //schsession      PM                 


	    private string URL_W2S_USERCONTACTS_GET = "http://site2.way2sms.com/jsp/UserContacts.jsp";
        private string URL_W2S_USERCONTACTS_POST = "http://site2.way2sms.com/FBMain";
        // faction         checkFB  
        // wfb             main     

	    private string URL_W2S_DASHBOARD = "http://site4.way2sms.com/jsp/DashBoard.jsp";
	    private string URL_W2S_QUICKCONTACTS = "http://site4.way2sms.com/QuickContacts";
        // folder          DashBoard  

	    private string URL_W2S_USERCONTACTS_REFERRER = "http://site2.way2sms.com/Main.action?id=";//"DC523D85FCF9F8096BF835DE581FC23C.w809";
	    private string URL_W2S_ID = "";

        string _userName;
        string _password;
        string _action;

        public int NumSmsSent;

        bool IsLoggedIn;
        CookieContainer cookies = new CookieContainer();
        private string _addrbookFile;

        public bool GetAddressBook()
        {
            int retryCnt = 0;

            while (!IsLoggedIn && retryCnt++ <= 3)
            {
                Login();
            }

            if (!IsLoggedIn)
                return false;


            if (!File.Exists(_addrbookFile))
            {
                StringBuilder sb = new StringBuilder("faction=checkFB");
                sb.Append("&wfb=main");

                string postdata = sb.ToString();

                string response = HttpHelper.GetWebPageResponse(URL_W2S_USERCONTACTS_POST, postdata,
                                                                URL_W2S_USERCONTACTS_REFERRER, cookies);
                postdata = null;
                response = HttpHelper.GetWebPageResponse(URL_W2S_USERCONTACTS_GET, postdata,
                                                         URL_W2S_USERCONTACTS_REFERRER, cookies);
                response = HttpHelper.GetWebPageResponse(URL_W2S_DASHBOARD, postdata, URL_W2S_USERCONTACTS_REFERRER,
                                                         cookies);
                postdata = "folder=DashBoard";
                response = HttpHelper.GetWebPageResponse(URL_W2S_QUICKCONTACTS, postdata, URL_W2S_USERCONTACTS_REFERRER,
                                                         cookies);
                //response = HttpHelper.GetWebPageResponse(URL_W2S_USERCONTACTS_GET, postdata, URL_W2S_USERCONTACTS_REFERRER, cookies);

                if (string.IsNullOrEmpty(response))
                    return false;

                // Parse contacts
                //<input type='hidden' id='Quckvalue' name='Quckvalue' value='      '>
                //<input type='hidden' id='Qucktitle' name='Qucktitle' value=',     '>

                string prefix = "<input type='hidden' id='Quckvalue' name='Quckvalue' value='*";
                string postfix = "'>";
                string names = StringParser.GetStringBetween(response, 0, prefix, postfix, null);

                var nameArr = names.Split('*');

                prefix = "<input type='hidden' id='Qucktitle' name='Qucktitle' value=',";
                postfix = "'>";
                string numbers = StringParser.GetStringBetween(response, 0, prefix, postfix, null);

                var numArr = numbers.Split(',');

                Debug.Assert(nameArr.Length == numArr.Length);
                int i = 0;
                foreach(var name in nameArr)
                {
                    AddressBook.Contacts.Add(new W2SContact{Name = name, MobileNumber = numArr[i++]});
                }

                // Now write back the gd
                using (FileStream ws = File.Open(_addrbookFile, FileMode.Create))
                {
                    BinaryFormatter bformatter = new BinaryFormatter();
                    bformatter.Serialize(ws, AddressBook.Contacts);
                }

                return true;
            }


            using (FileStream stream = File.Open(_addrbookFile, FileMode.Open))
            {
                BinaryFormatter bformatter = new BinaryFormatter();

                AddressBook.Contacts = (List<W2SContact>)bformatter.Deserialize(stream);
            }

            return true;
        }
        public Way2SmsAPI(string username, string pwd)
        {
            _userName = username;
            _password = pwd;

            //_addrbookFile = string.Format(AppConfig.AddressBookFile, _userName);
        }
        public void Login()
        {
            string postdata = "username=" + _userName + 
                "&password=" + _password;

            string response = HttpHelper.GetWebPageResponse(URL_W2S_LOGIN, postdata, URL_W2S_LOGIN_REFERER, cookies);

            // do better checking (for successful login)  later 
            if (!string.IsNullOrEmpty(response))
            {
                FileTracing.TraceInformation("Way2Sms login successful");
                IsLoggedIn = true;
            }
            else
            {
                FileTracing.TraceInformation("Way2Sms login failed");
            }

            // Get action value
            response = HttpHelper.GetWebPageResponse(URL_W2S_GETACTION, null, null, cookies);

            string prefix = "<input type=\"hidden\" name=\"Action\" id=\"Action\" value=\"";
            string postfix = "\"";
            string action = StringParser.GetStringBetween(response, 0, prefix, postfix, null);

            _action = action;

            var cc = cookies.GetCookies(new Uri(URL_W2S_GETACTION));
            URL_W2S_ID = cc["JSESSIONID"].Value.Split('~')[1];
            URL_W2S_USERCONTACTS_REFERRER += URL_W2S_ID;

        }

        public bool SendSms(string mobileNum, string msg, bool resetLogin = true)
        {
            int retryCnt = 0;
            bool isSuccess = false;

            while (!IsLoggedIn && retryCnt++ <= 3)
            {
                Login();
            }

            if (!IsLoggedIn)
                return isSuccess;

            IsLoggedIn = !resetLogin; // Do not know the timeout logic yet. So login each time to send the message
            StringBuilder sb = new StringBuilder("HiddenAction=instantsms");
            sb.Append("&catnamedis=StockTrader");
            sb.Append("&Action=" + _action);
            sb.Append("&chkall=on");
            sb.Append("&MobNo=" + mobileNum);
            sb.Append("&textArea=" + msg);

            string postdata = sb.ToString();
            //sb.Append("&");
            //HiddenAction    instantsms                                                 
            //catnamedis      Birthday                                                   
            //Action          sdf5445fdg                                                 
            //chkall          on                                                         
            //MobNo           9999999999                                                 
            //textArea        helllllooo fejkeww,f fwf,we fw,g ,ggf w,f,.f'';;'er;q'q;w  
            //guid            username                                                   
            //gpwd            *******                                                    
            //yuid            username                                                   
            //ypwd            *******   
            string response = HttpHelper.GetWebPageResponse(URL_W2S_QUICKSMS, postdata, URL_W2S_QUICKSMS_REFERER, cookies);

            if (!string.IsNullOrEmpty(response))
            {
                isSuccess = true;
                NumSmsSent++;
            }

            return isSuccess;
        }
	}
}
