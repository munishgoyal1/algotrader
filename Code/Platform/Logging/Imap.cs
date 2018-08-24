using System;
using EAGetMail;
using StockTrader.Platform.Logging;

namespace MailChatSms
{
    public class Imap
    {
        MailServer oServer;
        MailClient oClient;
        string _storePath;
        Imap4Folder _readMails;
        OnImapMessageReceive _handler;
        string _acceptMsgFrom;

        public delegate void OnImapMessageReceive(string messageText);

        public Imap(string user, string password, OnImapMessageReceive handler, string acceptMsgFrom = "")
        {
            _acceptMsgFrom = acceptMsgFrom;
            _handler = handler;
            oServer = new MailServer("imap.gmail.com", user, password, ServerProtocol.Imap4);
            oClient = new MailClient("TryIt");

            oServer.SSLConnection = true;
            oServer.Port = 993;

            _storePath = SystemUtils.GetMessagesStoreLocation();
            _readMails = new Imap4Folder(".Processed");
        }

        public void ReadEmails()
        {
            try
            {
                //if(!oClient.Connected)
                oClient.Connect(oServer);
                MailInfo[] infos = oClient.GetMailInfos();
                for (int i = 0; i < infos.Length; i++)
                {
                    MailInfo info = infos[i];
                    //Console.WriteLine("Index: {0}; Size: {1}; UIDL: {2}",
                    //    info.Index, info.Size, info.UIDL);

                    // Receive email from IMAP4 server
                    Mail oMail = oClient.GetMail(info);
                    var body = oMail.TextBody.Trim();
                    var subject = oMail.Subject.Trim().Replace("(Trial Version)", "");
                    //Console.WriteLine("From: {0}", oMail.From.ToString());
                    //Console.WriteLine("Subject: {0}\r\n", oMail.Subject);

                    if (_handler != null && (string.IsNullOrEmpty(_acceptMsgFrom) || string.Compare(oMail.From.Address, _acceptMsgFrom, true) == 0))
                        _handler(body);
                    // Generate an email file name based on date time.
                    System.DateTime d = System.DateTime.Now;
                    System.Globalization.CultureInfo cur = new
                        System.Globalization.CultureInfo("en-US");
                    string sdate = d.ToString("ddMMyyyy-HHmmss", cur);
                    string fileName = String.Format("{0}\\{1}_{2}.eml",
                         _storePath, sdate, subject.Replace(':', '-'));
                    // Save email to local disk
                    oMail.SaveAs(fileName, true);

                    oClient.ChangeMailFlags(info, "\\Seen");
                    oClient.Move(info, _readMails);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public void Close()
        {
            // Quit and purge emails marked as deleted from IMAP4 server.
            oClient.Logout();
            oClient.Quit();
        }
    }
}
