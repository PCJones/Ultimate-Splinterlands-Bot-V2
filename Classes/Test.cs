using HiveAPI.CS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    class Test
    {
            
        public static void TestX()
        {
            return;


            var exitEvent = new ManualResetEvent(false);
            var url = new Uri("wss://ws2.splinterlands.com/");

            using (var client = new WebsocketClient(url))
            {
                client.ReconnectTimeout = TimeSpan.FromSeconds(30);
                client.ReconnectionHappened.Subscribe(info =>
                    Log.WriteToLog($"Reconnection happened, type: {info.Type}"));

                client.MessageReceived.Subscribe(msg => Log.WriteToLog($"Message received: {msg}"));
                client.Start();

                Task.Run(() => client.Send(File.ReadAllText("test.txt")));
                Task.Run(() => client.Send("{\"type\":\"ping\"}"));

                exitEvent.WaitOne();
            }
        }
    }
}
