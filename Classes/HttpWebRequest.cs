using System.Text;
using System;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    class HttpWebRequest
    {
        /// <summary>
        /// Creates a http webrequest with the POST method
        /// </summary>
        /// <param name="CookieContainer">The cookiecontainer to be used</param>
        /// <param name="PostData">Postdata to post to</param>
        /// <param name="Url">Webrequest target url</param>
        /// <param name="UserAgent">Useragent to be used</param>
        /// <param name="Referer">Referer to be used</param>
        /// <returns>Returns the website sourcecode</returns>
        public static string WebRequestPost(System.Net.CookieContainer CookieContainer, string PostData, string Url, string UserAgent, string Referer, Encoding responseEncoding, string proxy = "")
        {
            string websiteResponse = "";

            try
            {
                System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(Url);

                if (proxy.Length > 2 && proxy.Contains(":"))
                {
                    string proxyIP = proxy.Split(':')[0].Trim();
                    int proxyPort = Convert.ToInt32(proxy.Split(':')[1].Trim());
                    webRequest.Proxy = new System.Net.WebProxy(proxyIP, proxyPort);
                }

                webRequest.CookieContainer = CookieContainer;
                webRequest.Method = "POST";
                webRequest.UserAgent = UserAgent;
                webRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                webRequest.KeepAlive = true;
                webRequest.AllowAutoRedirect = true;
                webRequest.Timeout = 22500;
                webRequest.Referer = Referer;
                webRequest.ContentType = "application/x-www-form-urlencoded";

                ASCIIEncoding encoding = new();
                byte[] dataBytes = encoding.GetBytes(PostData);
                webRequest.ContentLength = dataBytes.Length;

                System.IO.Stream stream = webRequest.GetRequestStream();
                stream.Write(dataBytes, 0, dataBytes.Length);
                stream.Close();

                System.Net.HttpWebResponse webResponse = (System.Net.HttpWebResponse)webRequest.GetResponse();

                System.IO.StreamReader streamReader = new(webResponse.GetResponseStream(), responseEncoding);

                websiteResponse = streamReader.ReadToEnd();

                streamReader.Close();
                streamReader.Dispose();
                webResponse.Close();

                return websiteResponse;
            }
            catch (Exception ex)
            {
                Log.WriteToLog("Error at WebRequest: " + ex.ToString());
                return websiteResponse;
            }
        }

        /// <summary>
        /// Creates a http webrequest with the GET method
        /// </summary>
        /// <param name="CookieContainer">The cookiecontainer to be used</param>
        /// <param name="Url">Webrequest target url</param>
        /// <param name="UserAgent">Useragent to be used</param>
        /// <param name="Referer">Referer to be used</param>
        /// <returns>Returns the website sourcecode</returns>
        public static string WebRequestGet(System.Net.CookieContainer CookieContainer, string Url, string UserAgent, string Referer, string proxy = "")
        {
            string websiteResponse = "";

            try
            {
                System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(Url);

                if (proxy.Length > 2 && proxy.Contains(":"))
                {
                    string proxyIP = proxy.Split(':')[0].Trim();
                    int proxyPort = Convert.ToInt32(proxy.Split(':')[1].Trim());
                    webRequest.Proxy = new System.Net.WebProxy(proxyIP, proxyPort);
                }

                webRequest.CookieContainer = CookieContainer;
                webRequest.Method = "GET";
                webRequest.UserAgent = UserAgent;
                webRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                webRequest.KeepAlive = true;
                webRequest.AllowAutoRedirect = true;
                webRequest.Timeout = 25000;
                webRequest.Referer = Referer;
                webRequest.ContentType = "application/x-www-form-urlencoded";

                System.Net.HttpWebResponse webResponse = (System.Net.HttpWebResponse)webRequest.GetResponse();

                System.IO.StreamReader streamReader = new(webResponse.GetResponseStream(), Encoding.UTF8);
                websiteResponse = streamReader.ReadToEnd();

                streamReader.Close();
                streamReader.Dispose();
                webResponse.Close();

                return websiteResponse;
            }
            catch (Exception ex)
            {
                Log.WriteToLog("Error at WebRequest: " + ex.ToString());
                return websiteResponse;
            }
        }
    }
}
