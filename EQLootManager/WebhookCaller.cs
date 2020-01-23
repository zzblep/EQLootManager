using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Timers;

namespace EQLootManager
{
    public class WebhookCaller
    {
        private static readonly HttpClient client = new HttpClient();
        private static List<string> SendQueue = new List<string>();
        private static Timer queueTimer;
        private static string WebhookURL = "";

        public WebhookCaller(string URL)
        {
            WebhookURL = URL;
            queueTimer = new Timer();
            queueTimer.Interval = 1000;
            queueTimer.AutoReset = true;
            queueTimer.Elapsed += ProcessQueue;
            queueTimer.Start();
        }
        public void SendHook(string Payload)
        {
            SendQueue.Add(Payload);
        }

        private async void ProcessQueue(Object source, ElapsedEventArgs e)
        {
            if (SendQueue.Count == 0)
                return;

            string Payload = SendQueue[0];
            SendQueue.RemoveAt(0);
            var values = new Dictionary<string, string>
            {
                //{ "content", Payload.Replace("\"", "\\\"") }
                { "content", Payload }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync(WebhookURL, content);
            var responseString = await response.Content.ReadAsStringAsync();
        }





    }
}
