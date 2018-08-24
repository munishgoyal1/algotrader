using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace HttpLibrary
{
    public static class HttpHelper
    {
        private static string DecompressGzip(Stream in_InputStream)
        {
            Stream lv_OutputStream = new MemoryStream();
            byte[] lv_Buffer = new byte[4096];
            using (GZipStream lv_gzip = new GZipStream(in_InputStream, CompressionMode.Decompress))
            {
                int i;
                while ((i = lv_gzip.Read(lv_Buffer, 0, lv_Buffer.Length)) != 0)
                {
                    lv_OutputStream.Write(lv_Buffer, 0, i);
                }
            }
            lv_OutputStream.Position = 0;
            using (StreamReader sr = new StreamReader(lv_OutputStream))
            {
                string str = sr.ReadToEnd();
                return str;
            }
        }

        private static string DecompressDeflate(Stream in_InputStream)
        {
            Stream lv_OutputStream = new MemoryStream();
            byte[] lv_Buffer = new byte[4096];
            using (DeflateStream lv_Deflate = new DeflateStream(in_InputStream, CompressionMode.Decompress))
            {
                int i;
                while ((i = lv_Deflate.Read(lv_Buffer, 0, lv_Buffer.Length)) != 0)
                {
                    lv_OutputStream.Write(lv_Buffer, 0, i);
                }
            }
            lv_OutputStream.Position = 0;
            using (StreamReader sr = new StreamReader(lv_OutputStream))
            {
                string str = sr.ReadToEnd();
                return str;
            }
        }

        public static string GetWebPageResponse(string url,
            string postdata,
            string referer,
            CookieContainer cookieContainer)
        {
            return GetWebPageResponse(url,
                postdata,
                referer,
                cookieContainer,
                true);
        }

        public static string GetWebPageResponse(string url,
            string postdata,
            string referer,
            CookieContainer cookieContainer,
            bool allowAutoRedirect)
        {
            if (cookieContainer == null) throw new ArgumentNullException("cookieContainer");
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                request.Headers["Accept-Language"] = "en-US,en;q=0.8";
                request.Headers["Accept-Encoding"] = "gzip, deflate, br";
                request.Headers["Upgrade-Insecure-Requests"] = "1";
                request.Headers["Origin"] = "https://secure.icicidirect.com"; // may not be common for all types of pages
                request.Headers["X-Requested-With"] = "XMLHttpRequest";

                if (!string.IsNullOrEmpty(referer))
                {
                    request.Referer = referer;
                }
                request.AllowAutoRedirect = allowAutoRedirect;
                request.CookieContainer = cookieContainer;
                request.KeepAlive = true;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36";
                if (!string.IsNullOrEmpty(postdata))
                {
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.Method = "POST";
                    //byte[] b = ASCIIEncoding.ASCII.GetBytes(postdata);
                    byte[] b = UTF8Encoding.UTF8.GetBytes(postdata);
                    request.ContentLength = b.Length;
                    using (Stream reqStream = request.GetRequestStream())
                    {
                        reqStream.Write(b, 0, b.Length);
                        reqStream.Flush();
                    }
                }
                else
                {
                    request.Method = "GET";
                }
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        string data = null;
                        if (response.ContentEncoding.IndexOf("gzip") > -1)
                        {
                            data = DecompressGzip(stream);
                        }
                        else if (response.ContentEncoding.IndexOf("deflate") > -1)
                        {
                            data = DecompressDeflate(stream);
                        }
                        else
                        {
                            using (StreamReader sr = new StreamReader(stream))
                            {
                                data = sr.ReadToEnd();
                            }
                        }
                        return data;
                    }
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (WebException)
            {
                return null;
            }
        }
    }
}
