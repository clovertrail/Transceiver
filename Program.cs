﻿using Microsoft.Azure.EventHubs;
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
        private static EventHubClient eventHubClient;
        private static string[] hubIds;
        private static HttpClient _client;
        private static ServerHandler _server;

        static void Main(string[] args)
        {
            var settingsPath = Path.Combine(Environment.CurrentDirectory, "local.settings.json");
            var settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(settingsPath));
            var hubIdsPath = Path.Combine(Environment.CurrentDirectory, "HubIds.csv");
            hubIds = File.ReadAllLines(hubIdsPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            var app = new CommandLineApplication();
            app.FullName = "Azure SignalR Serverless Sample";
            app.HelpOption("--help");
            var hubOption = app.Option("-m|--mode", "Set mode: client or server", CommandOptionType.SingleValue, true);
            if (hubOption.Value().Equals("client"))
            {
                Task.Run(async () =>
                {
                    var counter = new Counter();
                    var client = new ClientHandler(settings["SignalRServiceConnectionString"], hubIds, counter);
                    counter.StartPrint();
                    await client.StartAsync();
                    Console.WriteLine("Client started...");
                    Console.ReadLine();
                    await client.DisposeAsync();
                }).Wait();
            }
            else if (hubOption.Value().Equals("server"))
            {
                _server = new ServerHandler(settings["SignalRServiceConnectionString"], hubIds);
                var threadCount = 1;
                var txThreads = Enumerable.Range(0, threadCount)
                    .Select(_ => new Thread(TransmitEvents))
                    .ToList();
                foreach (var txThread in txThreads)
                    txThread.Start();

                Thread.Sleep(TimeSpan.FromMinutes(5));
                cancellationToken.Cancel();
            }
            app.Execute(args);
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
