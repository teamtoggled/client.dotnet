using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Toggled.Client
{
    public interface IToggledClient 
    {
        bool GetFeatureValue();
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

        private bool _featureValue;

        public ToggledClient(string connectionString, string hubName, string clientNameOrMachineName)
        {
            _connectionString = connectionString;
            _hubName = hubName;
            _clientNameOrMachineName = clientNameOrMachineName;
            
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
                Console.WriteLine("Connection is borked. Trying to reconnect...");
                await Task.Delay(new Random().Next(0,5) * 1000);
                await _connection.StartAsync();
            };

            _connection.On<string, string>("SendMessage",
                (string server, string message) =>
                {
                    _featureValue = !_featureValue;
                    Console.WriteLine($"[{DateTime.Now.ToString()}] Received message from server {server}: {message}");
                });

            StartListeningForEvents().GetAwaiter().GetResult();
        }

        //~ToggledClient()
        //{
         //   DisposeAsync().GetAwaiter().GetResult();
        //}

        private async Task StartListeningForEvents()
        {
            Console.WriteLine("Anchors away");
            await _connection.StartAsync();
        }

        public bool GetFeatureValue() => _featureValue;
    }
}
