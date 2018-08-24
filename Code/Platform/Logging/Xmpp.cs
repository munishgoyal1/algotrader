using System;
using System.IO;
using agsXMPP;
using agsXMPP.protocol.client;
using agsXMPP.Xml.Dom;
using StockTrader.Platform.Logging;

namespace MailChatSms
{
    public class Xmpp
    {
        string _user;
        string _password;

        string _acceptMsgFrom;

        XmppClientConnection _objXmpp;
        OnXmppMessageReceive _handler;
        public bool IsActive;
        public delegate void OnXmppMessageReceive(string messageText);

        public Xmpp(string user, string password, OnXmppMessageReceive handler, string acceptMsgFrom = "")
        {
            _user = user;
            _password = password;
            _handler = handler;

            _acceptMsgFrom = acceptMsgFrom;

            _objXmpp = new XmppClientConnection();
            Jid jid = new Jid(_user);
            _objXmpp.Password = _password;
            _objXmpp.Username = jid.User;
            _objXmpp.Server = jid.Server;
            _objXmpp.AutoResolveConnectServer = true;

            _objXmpp.OnMessage += messageReceived;
            _objXmpp.OnLogin += loggedIn;
            _objXmpp.OnAuthError += loginFailed;
            _objXmpp.OnError += OnError;
            _objXmpp.OnRegisterError += OnStreamError;
            _objXmpp.OnStreamError += OnStreamError;
            _objXmpp.OnSocketError += OnError;
        }

        public void messageReceived(object sender, agsXMPP.protocol.client.Message msg)
        {
            if (!string.IsNullOrEmpty(msg.Body))
            {
                string[] chatMessage = msg.From.ToString().Split('/');

                if (_handler != null && (string.IsNullOrEmpty(_acceptMsgFrom) || string.Compare(chatMessage[0], _acceptMsgFrom, true) == 0))
                    _handler(msg.Body);

                Jid jid = new Jid(chatMessage[0]);
                Message autoReply = new Message(jid, MessageType.chat, "Recieved");
                _objXmpp.Send(autoReply);

                string msgDesc = DateTime.Now.ToLongDateString() + ":" + DateTime.Now.ToLongTimeString() + "~ " 
                    + chatMessage[0] + " says: " + msg.Body;
                File.AppendAllLines(SystemUtils.GetXmppMessagesFile("Recieved"), new string[] { msgDesc });
            }
        }
        public void OnError(object o, Exception ex)
        {
            IsActive = false;
            _objXmpp.Close();
            Start();
        }
        public void OnStreamError(object o, Element e)
        {
            IsActive = false;
            _objXmpp.Close();
            Start();
        }
        public void loginFailed(object o, agsXMPP.Xml.Dom.Element el)
        {
            IsActive = false;
            Logger.LogErrorMessage("Xmpp Login failed. Please check your details.");
        }
        public void loggedIn(object o)
        {
            IsActive = true;
            FileTracing.TraceOut("Xmpp Logged in and Active.");
        }

        public bool Send(string to, string msgText)
        {
            bool success = false;
            try
            {
                Message msg = new Message(to, MessageType.chat, msgText);
                _objXmpp.Send(msg);
                success = true;
                string msgDesc = DateTime.Now.ToLongDateString() + ":" + DateTime.Now.ToLongTimeString() + "  " + msgText;
                File.AppendAllLines(SystemUtils.GetXmppMessagesFile("Sent"), new string[] { msgDesc });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return success;
        }
        public bool Start()
        {
            bool success = false;

            try
            {
                _objXmpp.Open();
                success = true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return success;
        }
    }
}
