
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
using System.Globalization;
using NISOCountries.Ripe;
using NISOCountries.Core;
using System.Net.Http;

namespace BeerWeather
{
    public class Beer
    {

        [FunctionName("Beer")]
        public async static Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequest req, ILogger log)
        {

            string cityName = req.Query["cityName"];
            string countryCode = req.Query["countryCode"];
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            cityName = cityName ?? data?.cityName;
            countryCode = countryCode ?? data?.countryCode;
            string result = "";

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();

            if (cityName != null & countryCode.Length == 2)
            {
                var countries = new RipeISOCountryReader().GetDefault();
                var lookup = new ISOCountryLookup<RipeCountry>(countries);
                RipeCountry isCode = null;
                lookup.TryGetByAlpha2(countryCode.ToUpper(), out isCode);
                if (isCode == null)
                {
                    return new BadRequestObjectResult("The country doesn't exist!");
                }
                else
                {
                    try
                    {
                        countryCode = countryCode.ToLower();

                        log.LogInformation("Receiving StorageAccount");

                        var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));

                        string blobContainerReference = "beerweather-blobs";
                        CloudBlobContainer blobContainer = await CreateBlobContainer(storageAccount, blobContainerReference);

                        string guid = Guid.NewGuid().ToString();
                        string blobUrl = await RetrieveCloudBlockBlob(guid, log);

                        log.LogInformation("Created cloud blob: {0}.png", guid);
                        string openWeatherIn = "locations-openweather-in";

                        CloudQueueMessage cloudQueueMessage = CreateApiMessage(cityName, countryCode, blobUrl, blobContainerReference, guid);
                        CloudQueueClient client = storageAccount.CreateCloudQueueClient();
                        var cloudQueue = client.GetQueueReference(openWeatherIn);
                        await cloudQueue.CreateIfNotExistsAsync();

                        await cloudQueue.AddMessageAsync(cloudQueueMessage);

                        log.LogInformation("Posted object in queue locations-openweather-in");

                        result = String.Format("Your beerreport can be found at {0}", blobUrl);


                    }
                    catch
                    {
                        result = "Please pass a name on the query string or in the request body";
                    }
                }

              
            }


            return cityName != null & countryCode.Length == 2 & result != null
                 ? (ActionResult)new OkObjectResult(result)
                 : new BadRequestObjectResult(result);
        }


        private static async Task<CloudBlobContainer> CreateBlobContainer(CloudStorageAccount storageAccount, string reference)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(reference);
            await blobContainer.CreateIfNotExistsAsync();

            // Set the permissions so the blobs are public. 
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Off
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



        private async static Task<string> RetrieveCloudBlockBlob(string guid, ILogger log)
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
            await blobContainer.CreateIfNotExistsAsync();

            var sas = blobContainer.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(15)
            });


            string fileName = String.Format("{0}.png", guid);
            CloudBlockBlob cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
            cloudBlockBlob.Properties.ContentType = "image/png";
            string imageUrl = string.Format("{0}/{1}{2}", blobContainer.StorageUri.PrimaryUri.AbsoluteUri , fileName , sas);
            return imageUrl;
        }

      
    }


}
