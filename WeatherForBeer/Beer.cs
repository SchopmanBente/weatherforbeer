
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Threading.Tasks;
using System;
using WeatherForBeer;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BeerWeather
{
    public static class Beer
    {

        [FunctionName("beer")]
        public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequest req, ILogger log)
        {

            string cityName = req.Query["cityName"];
            string countryCode = req.Query["countryCode"];
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            cityName = cityName ?? data?.cityName;
            countryCode = countryCode ?? data?.countryCode;
            string result = "";

            if (cityName != null & countryCode.Length == 2)
            {
                try
                {
                    countryCode = countryCode.ToLower();

                    log.LogInformation("Receiving StorageAccount");

                    var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));
                    //var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                    //var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=beerweather;AccountKey=Hw25zEHPg/d6+Fseju/1ts6LqVEK3PdJJ0zzrWZYna5Nvv4I9Zu9BZMOMkapzkR7GyK/ovXsR0yqK9hk4tUTBA==;EndpointSuffix=core.windows.net");

                    log.LogInformation("Received storageAccount {0}", storageAccount.Credentials.AccountName.ToString());

                    string blobContainerReference = "beerweather-blobs";
                    CloudBlobContainer blobContainer = await CreateBlobContainer(storageAccount, blobContainerReference);

                    string guid = Guid.NewGuid().ToString();
                    string blobUrl = CreateCloudBlockBlob(cityName, countryCode, guid, blobContainer, storageAccount);

                    log.LogInformation("Created cloud blob: {0}", blobUrl);
                    string openWeatherIn = "locations-openweather-in";

                    CloudQueueMessage cloudQueueMessage = CreateApiMessage(cityName, countryCode, blobUrl, blobContainerReference, guid);
                    CloudQueueClient client = storageAccount.CreateCloudQueueClient();
                    var cloudQueue = client.GetQueueReference(openWeatherIn);
                    await cloudQueue.CreateIfNotExistsAsync();

                    await cloudQueue.AddMessageAsync(cloudQueueMessage);

                    log.LogInformation("Posted object in queue locations-openweather-in");

                    result = String.Format("Your beerreport can be found at {0}", blobUrl);


                }
                catch (Exception e)
                {
                    result = "Please pass a name on the query string or in the request body";
                }
            }


            return cityName != null & countryCode.Length == 2 & result != null
                 ? (ActionResult)new OkObjectResult(result)
                 : new BadRequestObjectResult(result);
        }

        private static string CreateCloudBlockBlob(string cityName, string countryCode, string guidString, CloudBlobContainer blobContainer, CloudStorageAccount storageAccount)
        {

            string fileName = String.Format("{0}.png", guidString);
            CloudBlockBlob cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
            cloudBlockBlob.Properties.ContentType = "image/png";


            string blobUrl = cloudBlockBlob.StorageUri.PrimaryUri.ToString();
            return blobUrl;
        }

        private static async Task<CloudBlobContainer> CreateBlobContainer(CloudStorageAccount storageAccount, string reference)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(reference);
            await blobContainer.CreateIfNotExistsAsync();

            // Set the permissions so the blobs are public. 
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Blob
            };
            await blobContainer.SetPermissionsAsync(permissions);
            return blobContainer;
        }

        private static async Task PostMessageToQueue(CloudStorageAccount storageAccount, string queue, CloudQueueMessage cloudQueueMessage)
        {
            CloudQueueClient client = storageAccount.CreateCloudQueueClient();
            var cloudQueue = client.GetQueueReference(queue);
            await cloudQueue.CreateIfNotExistsAsync();

            await cloudQueue.AddMessageAsync(cloudQueueMessage);

        }

        private static CloudQueueMessage CreateApiMessage(string cityName, string countryCode, string blobUrl, string blobContainerReference,
            string guid)
        {
            TriggerMessage l = new TriggerMessage
            {
                CityName = cityName,
                CountryCode = countryCode,
                Blob = blobUrl,
                BlobRef = blobContainerReference,
                Guid = guid
            };
            var messageAsJson = JsonConvert.SerializeObject(l);
            var cloudQueueMessage = new CloudQueueMessage(messageAsJson);
            return cloudQueueMessage;
        }
    }


}
