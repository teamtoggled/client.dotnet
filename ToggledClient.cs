using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;

namespace Toggled.Client
{
    public class ToggledSignalRMessage
    {
        public string FeatureToggleName {get; set;}
        public bool NewValue {get; set;}        

        // TODO: It would be nice to have a UpdatedDateTimeUtc to ensure that we don't overwrite
        // if a message arrives out of sequence.
    }

    public class ToggledClient : IToggledClient
    {
        private readonly HubConnection _connection;
        private readonly string _connectionString;
        private readonly string _hubName;
        private readonly string _clientNameOrMachineName;

        public async Task DisposeAsync()
        {
            await _connection.DisposeAsync();
        }

        private string GetClientUrl(string endpoint, string hubName)
        {
            return $"{endpoint}/client/?hub={hubName}";
        }

        private Dictionary<string, bool> FeatureValueDictInternal {get; set;}

        public ToggledClient(string connectionString, string hubName, string clientNameOrMachineName)
        {
            _connectionString = connectionString;
            _hubName = hubName;
            _clientNameOrMachineName = clientNameOrMachineName;
            FeatureValueDictInternal = new Dictionary<string, bool>();
            
            var serviceUtils = new ServiceUtils(_connectionString);

            var url = GetClientUrl(serviceUtils.Endpoint, _hubName);

            _connection = new HubConnectionBuilder()
                .WithUrl(url, option =>
                {
                    option.AccessTokenProvider = () =>
                    {
                        return Task.FromResult(serviceUtils.GenerateAccessToken(url, _clientNameOrMachineName));
                    };
                }).Build();

            _connection.Closed += async (error) =>
            {
                Console.WriteLine("The connection to the Toggled SignalR service has been lost. Trying to reconnect...");
                await Task.Delay(new Random().Next(0,5) * 1000);
                await _connection.StartAsync();
            };

            _connection.On<string, string>("SendMessage",
                (string server, string message) =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString()}] Received message from server {server}: {message}");

                    var signalREvent = JsonConvert.DeserializeObject<ToggledSignalRMessage>(message);

                    if(FeatureValueDictInternal.ContainsKey(signalREvent.FeatureToggleName))
                    {
                        Console.WriteLine("Updating feature switch value.");
                        FeatureValueDictInternal[signalREvent.FeatureToggleName] = signalREvent.NewValue;
                    }
                    else
                    {
                        Console.WriteLine("Adding new feature switch to dictionary.");
                        FeatureValueDictInternal.Add(signalREvent.FeatureToggleName, signalREvent.NewValue);
                    }
                    
                });

            StartListeningForEvents().GetAwaiter().GetResult();
        }

        private async Task StartListeningForEvents()
        {
            Console.WriteLine("Anchors away");
            await _connection.StartAsync();
        }

        public bool GetFeatureValue(string featureName)
        {
            if(FeatureValueDictInternal.ContainsKey(featureName))
                return FeatureValueDictInternal[featureName];

            throw new Exception($"There was no feature switch in the dictionary called {featureName}.");
        }
    }
}
