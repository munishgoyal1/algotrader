
using MailChatSms;
using System;

namespace StockTrader.Platform.Logging
{
    [Flags]
    public enum MessagingChannel
    {
        EMAIL = 1,
        CHAT = 2,
    }

    public static class MessagingUtils
    {
        static string _gUser = "simpletrader11@gmail.com";
        static string _gPassword = "!Q2w3e4r";
        static string _mailTo = "munishgoyal1@gmail.com";
        static string _chatTo = "munishgoyal1@gmail.com"; 

        static Smtp GmailSender;
        static Xmpp Gtalk;
        static Imap GmailReader;

        public static void Init(Imap.OnImapMessageReceive _handlerImap, Xmpp.OnXmppMessageReceive _handlerXmpp, string acceptMsgFrom = "")
        {
           // Gtalk = new Xmpp(_gUser, _gPassword, _handlerXmpp, acceptMsgFrom);
            //GmailReader = new Imap(_gUser, _gPassword, _handlerImap, acceptMsgFrom);
            GmailSender = new Smtp("smtp.gmail.com", 587, _gUser, _gPassword, true);
            //Gtalk.Start();
        }

        public static void Init()
        {
            //Gtalk = new Xmpp(_gUser, _gPassword, null, "");
            //GmailReader = new Imap(_gUser, _gPassword, _handlerImap, acceptMsgFrom);
            GmailSender = new Smtp("smtp.gmail.com", 587, _gUser, _gPassword, true);
            //Gtalk.Start();
        }

        public static void ReadMailMessages()
        {
            //BUGBUG TODO EAGetEmail has expired
            //GmailReader.ReadEmails();
        }

        public static void SendAlertMessage(string subject, string body, string smsBody = null,
            MessagingChannel channel = MessagingChannel.EMAIL | MessagingChannel.CHAT)
        {
            try
            {
                body = string.Format("{0} at {1}", body, DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
                
                if (channel.HasFlag(MessagingChannel.EMAIL))
                    GmailSender.SendMail(_gUser, _mailTo, subject, body, false, 3);

                //if (channel.HasFlag(MessagingChannel.CHAT))
                //    Gtalk.Send(_chatTo, subject + "-----" + body);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }
    }
}

//int extraParts = ((totalParts + 1) * (subject.Length + 7)) / SINGLE_SMS_MAX_LENGTH;

//int totalLen = totalParts * SINGLE_SMS_MAX_LENGTH - remainingBody.Length;
//totalLen = 

//extraParts += (remainingBody.Length + (totalParts * (subject.Length + 7))) / SINGLE_SMS_MAX_LENGTH;
//totalParts = extraParts += extraParts + 1;

//while (extraParts-- > 0)
//int len = SINGLE_SMS_MAX_LENGTH - subject.Length - 7;
//