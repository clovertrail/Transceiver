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
        private static string[] hubIds;
        private static ServerHandler _server;

        static void Main(string[] args)
        {
            
            var app = new CommandLineApplication();
            app.FullName = "Azure SignalR Serverless Sample";
            app.HelpOption("--help");
            var settingsFile = app.Option("-s|--settingFile", "Set setting json file", CommandOptionType.SingleValue, true);
            var hubIdsFile = app.Option("-b|--hubIdsFile", "Set hubIds file", CommandOptionType.SingleValue, true);

            var settingsPath = Path.Combine(Environment.CurrentDirectory, settingsFile.Value());
            var settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(settingsPath));

            var hubIdsPath = Path.Combine(Environment.CurrentDirectory, hubIdsFile.Value());
            hubIds = File.ReadAllLines(hubIdsPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            app.Command("client", cmd =>
            {
                cmd.Description = "Start a client to listen to the service";
                cmd.HelpOption("--help");
                cmd.OnExecute(async () =>
                {
                    var counter = new Counter();
                    var client = new ClientHandler(settings["SignalRServiceConnectionString"], hubIds, counter);
                    counter.StartPrint();
                    await client.StartAsync();
                    Console.WriteLine("Client started...");
                    Console.ReadLine();
                    await client.DisposeAsync();
                    return 0;
                });
            });
            app.Command("server", cmd =>
            {
                cmd.Description = "Start a server to broadcast message through RestAPI";
                cmd.HelpOption("--help");
                var count = cmd.Argument("<threadCount>", "Set thread count");
                cmd.OnExecute(() =>
                {
                    _server = new ServerHandler(settings["SignalRServiceConnectionString"], hubIds);
                    var threadCount = int.Parse(count.Value);
                    var txThreads = Enumerable.Range(0, threadCount)
                        .Select(_ => new Thread(TransmitEvents))
                        .ToList();
                    foreach (var txThread in txThreads)
                        txThread.Start();

                    Thread.Sleep(TimeSpan.FromMinutes(5));
                    cancellationToken.Cancel();
                    return 0;
                });
            });
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
