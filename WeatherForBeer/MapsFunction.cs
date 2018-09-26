using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace WeatherForBeer
{
    public class MapsFunction
    {
        [FunctionName("Maps")]
        public async static Task RunAsync([QueueTrigger("locations-openweather-out", Connection = "AzureWebJobsStorage")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

            BlobTriggerMessage message = (BlobTriggerMessage)JsonConvert.DeserializeObject(myQueueItem, typeof(BlobTriggerMessage));
            WeatherRootObject weatherRootObject = message.Weather;
            Coord coordinates = weatherRootObject.Coord;
            log.LogInformation("Objects are casted");
            string key = Environment.GetEnvironmentVariable("AzureMapsKey");
            string url = String.Format("https://atlas.microsoft.com/map/static/png?subscription-key={0}&api-version=1.0&center={1},{2}", key,
                coordinates.Lon, coordinates.Lat);
            try
            {

                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                CloudBlockBlob cloudBlockBlob = await GetCloudBlockBlob(message, storageAccount);

                HttpClient client = new HttpClient();
                log.LogInformation("Awaiting HTTP Response from Maps API");
                HttpResponseMessage response = await client.GetAsync(url);
                log.LogInformation("Retrieving HTTP Response");
                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation("Response is binnen");

                    try
                    {
                        Stream responseContent = await response.Content.ReadAsStreamAsync();
                        double tempInCelsius = weatherRootObject.Main.Temp;
                        double windSpeed = weatherRootObject.Wind.Speed;
                        string tekst2 = string.Format("Temp: {0} °C Wind:{1} km/u", tempInCelsius, windSpeed);
                        string tekst1 = GetBeerCaption(tempInCelsius);
                        var renderedImage = ImageHelper.AddTextToImage(responseContent, (tekst1, (10, 20)), (tekst2, (10, 50)));

                        log.LogInformation("Uploading response to blob");
                        await cloudBlockBlob.UploadFromStreamAsync(renderedImage);
                        log.LogInformation("Uploaded response to blob");
                    }
                    catch{}
                }
                else
                {
                    log.LogInformation(response.StatusCode.ToString());
                }



            }
            catch
            {

            } 
        }

        private static async Task<CloudBlockBlob> GetCloudBlockBlob(BlobTriggerMessage message, CloudStorageAccount storageAccount)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
            await blobContainer.CreateIfNotExistsAsync();
            string fileName = String.Format("{0}.png", message.Guid);
            CloudBlockBlob cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
            cloudBlockBlob.Properties.ContentType = "image/png";
            return cloudBlockBlob;
        }

        private static string GetBeerCaption(double tempInCelsius)
        {
            string beerCaption;
            if (tempInCelsius > 20.00)
            {
                beerCaption = "HAHAHA BIER!";
            }
            else
            {
                beerCaption = "Neem maar iets anders";
            }

            return beerCaption;
        }   

    }
}
