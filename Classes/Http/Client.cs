using System;
using System.Net;
using System.Net.Http;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Http
{
    public class HttpClient
    {
        public static CookieContainer CookieContainer = new();
        private static System.Net.Http.HttpClient httpClient;

        public static System.Net.Http.HttpClient getInstance() {
            if (httpClient == null){
                httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = new TimeSpan(0, 2, 15);
            }
            return httpClient;
        }
    }
}