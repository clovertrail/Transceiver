using Microsoft.Azure.EventHubs;
using Microsoft.Azure.SignalR.Samples.Serverless;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Transceiver
{
    class Program
    {
        private const int TRANSMIT_DELAY_MS = 100;

        private static CancellationTokenSource cancellationToken = new CancellationTokenSource();
        private static ServerHandler _server;
        private static Dictionary<string, string> _settings;
        private static string[] _hubIds;

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Specify <client|server> mode");
                return;
            }
            var settingsPath = Path.Combine(Environment.CurrentDirectory, "local.settings.json");
            _settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(settingsPath));

            var hubIdsPath = Path.Combine(Environment.CurrentDirectory, "HubIds.csv");
            _hubIds = File.ReadAllLines(hubIdsPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            if (args[0].Equals("client"))
            {
                Task.Run(async () =>
                {
                    var counter = new Counter();
                    var client = new ClientHandler(_settings["SignalRServiceConnectionString"], _hubIds, counter);
                    counter.StartPrint();
                    await client.StartAsync();
                    Console.WriteLine("Client started...");
                    Console.ReadLine();
                    await client.DisposeAsync();
                }).Wait();
            }
            else
            {
                _server = new ServerHandler(_settings["SignalRServiceConnectionString"], _hubIds);
                var threadCount = 2;
                var txThreads = Enumerable.Range(0, threadCount)
                    .Select(_ => new Thread(TransmitEvents))
                    .ToList();
                foreach (var txThread in txThreads)
                    txThread.Start();

                Thread.Sleep(TimeSpan.FromMinutes(5));
                cancellationToken.Cancel();
            }
        }

        static async void TransmitEvents()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                await _server.BroadcastMessage();
                await Task.Delay(TRANSMIT_DELAY_MS);
            }
        }
    }
}
