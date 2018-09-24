using System;
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
    public static class Maps
    {
        [FunctionName("Maps")]
        public static async Task RunAsync([QueueTrigger("locations-openweather-out", Connection = "AzureWebJobsStorage")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

            BlobTriggerMessage message = (BlobTriggerMessage)JsonConvert.DeserializeObject(myQueueItem, typeof(BlobTriggerMessage));
            WeatherRootObject weatherRootObject = message.Weather;
            Coord coordinates = weatherRootObject.Coord;
            log.LogInformation("Objects are casted");
            string key = Environment.GetEnvironmentVariable("AzureMapsKey");
            string url = String.Format("https://atlas.microsoft.com/map/static/png?subscription-key={0}&api-version=1.0&center={1},{2}", key,
                coordinates.Lon, coordinates.Lat);
            try {

                CloudBlockBlob cloudBlockBlob = null;
                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                string refBlob = message.BlobRef.ToString();
                log.LogInformation("The blob reference is {0}", refBlob);
                CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
                await blobContainer.CreateIfNotExistsAsync();

                log.LogInformation(message.Guid);
                string fileName = String.Format("{0}.png", message.Guid);
                log.LogInformation(fileName);
                cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
                cloudBlockBlob.Properties.ContentType = "image/png";
                
            
                

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
                        string tekst2 = tempInCelsius.ToString();
                        string tekst1 = GetBeerCaption(tempInCelsius);
                        var renderedImage = ImageHelper.AddTextToImage(responseContent, (tekst1, (10, 20)), (tekst2, (10, 50)));



                        log.LogInformation("Uploading response to blob");
                        await cloudBlockBlob.UploadFromStreamAsync(renderedImage);
                        log.LogInformation("Uploaded response to blob");
                    }
                    catch
                    {

                    }
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

        private static string GetBeerCaption(double tempInCelsius)
        {
            string tekst1;
            if (tempInCelsius > 20.00)
            {
                tekst1 = "Neem een biertje!";
            }
            else
            {
                tekst1 = "Warme chocolademelk met rum!";
            }

            return tekst1;
        }

        private static async Task<CloudBlockBlob> RetrieveCloudBlockBlob(BlobTriggerMessage l, ILogger log)
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
            await blobContainer.CreateIfNotExistsAsync();

            // Set the permissions so the blobs are public. 
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Blob
            };
            await blobContainer.SetPermissionsAsync(permissions); 

            string fileName = String.Format("{0}.png",l.Guid);
            CloudBlockBlob cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
            cloudBlockBlob.Properties.ContentType = "image/png";
            return cloudBlockBlob;
        }
    }
}
