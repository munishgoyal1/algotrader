using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Web.UI.WebControls;

namespace MailChatSms
{
    /// <summary>
    /// Summary description for SmtpMailSender
    /// </summary>
    public class Smtp : IDisposable
    {
        /*
        private string _host;
        private int _port;
        private bool _credentialsRequired;
        private string _userName;
        private string _password;
         * */
        private SmtpClient _smtpClient;

        // Track whether Dispose has been called.
        private bool disposed = false;

        ///// <summary>
        ///// Constructor for mail sender
        ///// </summary>
        //public SmtpMailSender()
        //{
        //    _smtpClient = new SmtpClient();
        //}

        ///// <summary>
        ///// Constructor for mail sender
        ///// </summary>
        ///// <param name="host">host to be used to send mail</param>
        ///// <param name="port">port to be used to send mail</param>
        //public SmtpMailSender(string host, int port)
        //{
        //    /*
        //    _host = host;
        //    _port = port;
        //    _credentialsRequired = false;
        //    _userName = string.Empty;
        //    _password = string.Empty;
        //     */
        //    _smtpClient = new SmtpClient();
        //    _smtpClient.Host = host;
        //    _smtpClient.Port = port;
        //}

        /// <summary>
        /// Constructor for mail sender
        /// </summary>
        /// <param name="host">host to be used to send mail</param>
        /// <param name="port">port to be used to send mail</param>
        /// <param name="userName">user name for smtp server authentication</param>
        /// <param name="password">password for smtp server authentication</param>
        public Smtp(string host, int port, string userName, string password, bool useSSL)
        {
            _smtpClient = new SmtpClient();
            _smtpClient.Host = host;
            _smtpClient.Port = port;
            _smtpClient.Credentials = new System.Net.NetworkCredential(userName, password);
            _smtpClient.EnableSsl = useSSL;
        }

        /// <summary>
        /// Method to send mail
        /// </summary>
        /// <param name="from">From email Address</param>
        /// <param name="to">To email Address</param>
        /// <param name="subject">Subject of mail</param>
        /// <param name="body">Body of mail</param>
        /// <returns>Success or Failure</returns>
        public short SendMail(string from, string to, string subject, string body, bool isBodyHtml, short maxRetries)
        {
            short smtpStatus = -1;

            try
            {
                MailMessage message = new MailMessage(from, to, subject, body);

                //configure message
                message.IsBodyHtml = isBodyHtml;

                smtpStatus = SendMailMessage(message, maxRetries);

            }
            catch (Exception)
            {
                //Exception caught in Retry
                if (smtpStatus == 250)
                    smtpStatus = -1;
            }

            return smtpStatus;
        }

        /// <summary>
        /// Method to send mail
        /// </summary>
        /// <param name="from">From email Address</param>
        /// <param name="to">To email Address</param>
        /// <param name="subject">Subject of mail</param>
        /// <param name="body">Body of mail</param>
        /// <param name="attachments">List of attachements</param>
        /// <returns>Success or Failure</returns>
        public short SendMailWithAttachments(string from, string to, string subject, string body, bool isBodyHtml, string[] attachments, short maxRetries)
        {
            short smtpStatus = 250;

            try
            {

                MailMessage message = new MailMessage(from, to, subject, body);

                // Add attachments to message
                foreach (string attachment in attachments)
                {
                    Attachment attachmentData = new Attachment(attachment, MediaTypeNames.Application.Octet);
                    message.Attachments.Add(attachmentData);
                }

                //configure message
                message.IsBodyHtml = isBodyHtml;

                smtpStatus = SendMailMessage(message, maxRetries);

            }
            catch (Exception)
            {
                //Exception caught in Retry
                if (smtpStatus == 250)
                    smtpStatus = -1;
            }

            return smtpStatus;
        }

        /// <summary>
        /// Method to send mail
        /// </summary>
        /// <param name="from">From email Address</param>
        /// <param name="to">To email Address</param>
        /// <param name="subject">Subject of mail</param>
        /// <param name="body">Body of mail</param>
        /// <param name="attachments">List of attachements</param>
        /// <returns>Success or Failure</returns>
        public short SendMailWithAttachments(string from, string to, string subject, string body, ListDictionary replacements, bool isBodyHtml, string[] attachments, short maxRetries)
        {
            short smtpStatus = 250;

            try
            {
                MailDefinition md = new MailDefinition();

                MailMessage message = md.CreateMailMessage(to, replacements, body, new System.Web.UI.Control());

                message.Subject = subject;
                message.From = new MailAddress(from);


                // Add attachments to message
                foreach (string attachment in attachments)
                {
                    Attachment attachmentData = new Attachment(attachment, MediaTypeNames.Application.Octet);
                    message.Attachments.Add(attachmentData);
                }

                //configure message
                message.IsBodyHtml = isBodyHtml;

                smtpStatus = SendMailMessage(message, maxRetries);
           
            }
            catch (Exception)
            {
                //Exception caught in Retry
                if (smtpStatus == 250)
                    smtpStatus = -1;
            }

            return smtpStatus;
        }

        /// <summary>
        /// Method to send mail
        /// </summary>
        /// <param name="from">From email Address</param>
        /// <param name="to">To email Address</param>
        /// <param name="to">Reply-To email Addresses</param>
        /// <param name="subject">Subject of mail</param>
        /// <param name="body">Body of mail</param>
        /// <param name="attachments">List of attachements</param>
        /// <returns>Success or Failure</returns>
        public short SendMailWithAttachments(string from, string to, string[] replyToList, string subject, string body, string[] attachments,
                                            ListDictionary replacements, 
                                            bool isBodyHtml, 
                                            short maxRetries)
        {
            short smtpStatus = 250;
            MailMessage message = null;
            try
            {
                MailDefinition md = new MailDefinition();

                message = md.CreateMailMessage(to, replacements, body, new System.Web.UI.Control());

                // Set the reply-to list of addresses
                MailAddressCollection replyToAddresses = message.ReplyToList;
                foreach (string addr in replyToList)
                {
                    MailAddress replyToAddr = new MailAddress(addr);
                    replyToAddresses.Add(replyToAddr);
                }


                message.Subject = subject;
                message.From = new MailAddress(from);


                // Add attachments to message
                foreach (string attachment in attachments)
                {
                    Attachment attachmentData = new Attachment(attachment, MediaTypeNames.Application.Octet);
                    attachmentData.ContentDisposition.FileName = Path.GetFileName(attachment);
                    message.Attachments.Add(attachmentData);
                }

                //configure message
                message.IsBodyHtml = isBodyHtml;

                smtpStatus = SendMailMessage(message, maxRetries);

            }
            catch (Exception)
            {
                //Exception caught in Retry
                if (smtpStatus == 250)
                    smtpStatus = -1;
            }
            finally
            {
                if (message != null)
                {
                    if(message.Attachments != null)
                        message.Attachments.Dispose();

                    message.Dispose();
                }
            }

            return smtpStatus;
        }


        /// <summary>
        /// Method to send mail
        /// </summary>
        /// <param name="from">From email Address</param>
        /// <param name="to">To email Address</param>
        /// <param name="subject">Subject of mail</param>
        /// <param name="body">Body of mail</param>
        /// <param name="attachments">List of attachements</param>
        /// <returns>Success or Failure</returns>
        public short SendMailMessage(MailMessage message, short maxRetries)
        {
            int numberOfRetries = 0;
            short smtpStatus = -1;

           Reexecute:
            try
            {
                numberOfRetries++;
                _smtpClient.Send(message);
                smtpStatus = 0;
            }

            catch (SmtpFailedRecipientsException ex)
            {
                //if number of reties to cross maximum allowed number of retries, abort task and send failure
                if (numberOfRetries >= maxRetries)
                {
                    smtpStatus = (short)ex.StatusCode;
                }
                else
                {
                    for (int i = 0; i < ex.InnerExceptions.Length; i++)
                    {
                        smtpStatus = (short)ex.InnerExceptions[i].StatusCode;

                        if (smtpStatus == (short)SmtpStatusCode.MailboxBusy || smtpStatus == (short)SmtpStatusCode.MailboxUnavailable)
                        {
                            // Delivery failed - retrying in 5 second
                            smtpStatus = 250;

                            System.Threading.Thread.Sleep(5000);

                            goto Reexecute;
                        }
                    }
                }
            }
            catch (SmtpFailedRecipientException ex)
            {
                if (numberOfRetries >= maxRetries)
                {
                    smtpStatus = (short)ex.StatusCode;
                }

                else if (smtpStatus == (short)SmtpStatusCode.MailboxBusy || smtpStatus == (short)SmtpStatusCode.MailboxUnavailable)
                {
                    // Delivery failed - retrying in 5 second
                    smtpStatus = 250;

                    System.Threading.Thread.Sleep(5000);

                    goto Reexecute;
                }
            }

            return smtpStatus;
        }

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    _smtpClient.Dispose();
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here.
                // If disposing is false, 
                // only the following code is executed.
            }
            disposed = true;
        }
 
        /// <summary>
        /// Destructor
        /// </summary>
        ~Smtp()
        {
            Dispose(true);
        }
    }
}