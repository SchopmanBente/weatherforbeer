using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace WeatherForBeer
{
    public static class OpenWeather
    {
        [FunctionName("getWeatherInfo")]
        public static async Task Run([QueueTrigger("locations-openweather-in", Connection = "AzureWebJobsStorage")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

            try
            {
                string openweatherapikey = Environment.GetEnvironmentVariable("OpenWeatherKey");

                TriggerMessage l = (TriggerMessage)JsonConvert.DeserializeObject(myQueueItem, typeof(TriggerMessage));

                try
                {
                    string url = String.Format("https://api.openweathermap.org/data/2.5/weather?q={0},{1}&appid=98038ed7c159977f2e7b0b6aad6cbda5&units=metric",
                        l.CityName, l.CountryCode);
                    log.LogInformation("Creating HTTP Client");

                    HttpClient client = new HttpClient();
                    log.LogInformation("Awaiting HTTP Response from OpenWeather API");
                    HttpResponseMessage response = await client.GetAsync(url);
                    log.LogInformation("Retrieving HTTP Response");
                    if (response.IsSuccessStatusCode)
                    {
                        log.LogInformation("Response is binnen");
                        string content = await response.Content.ReadAsStringAsync();
                        CloudQueueMessage cloudQueueMessage = CreateCloudQueueMessage(l, content);

                        var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                        await PostMessageToQueue(cloudQueueMessage, storageAccount);
                    }
                    else
                    {
                        log.LogInformation("An epic fail occured");
                    }
                }
                catch(Exception e)
                {
                    log.LogInformation(e.Data.ToString());
                }
            }
            catch
            {

            }

        }

        private static async Task PostMessageToQueue(CloudQueueMessage cloudQueueMessage, CloudStorageAccount storageAccount)
        {
            var cloudClient = storageAccount.CreateCloudQueueClient();
            var queue = cloudClient.GetQueueReference("locations-openweather-out");
            await queue.CreateIfNotExistsAsync();

            await queue.AddMessageAsync(cloudQueueMessage);
        }

        private static CloudQueueMessage CreateCloudQueueMessage(TriggerMessage l, string content)
        {
            BlobTriggerMessage message = CreateBlobTriggerMessage(l, content);
            var messageAsJson = JsonConvert.SerializeObject(message);
            var cloudQueueMessage = new CloudQueueMessage(messageAsJson);
            return cloudQueueMessage;
        }

        private static BlobTriggerMessage CreateBlobTriggerMessage(TriggerMessage l, string content)
        {
            WeatherRootObject weather = (WeatherRootObject)JsonConvert.DeserializeObject(content, typeof(WeatherRootObject));
            BlobTriggerMessage message = new BlobTriggerMessage(weather)
            {
                CityName = l.CityName,
                CountryCode = l.CountryCode,
                Blob = l.Blob,
                Guid = l.Guid,
            };
            return message;
        }
    }
}
