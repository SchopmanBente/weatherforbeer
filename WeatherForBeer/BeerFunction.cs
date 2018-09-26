
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
using System.Text.RegularExpressions;

namespace BeerWeather
{
    public class Beer
    {


        [FunctionName("beer")]
        public async static Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequest req, ILogger log)
        {

            string cityName = req.Query["city"];
            string countryCode = req.Query["country"];
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            cityName = cityName ?? data?.city;
            countryCode = countryCode ?? data?.country;
            if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(cityName) ||
                countryCode.Length != 2 || !CheckIfCountryIsValid(countryCode) || !Regex.IsMatch(cityName, @"^[a-z]+$"))
            {
                return new BadRequestObjectResult("The input is not valid");
            }
            else
            {
                try
                {
                    countryCode = countryCode.ToLower();
                    cityName = cityName.ToLower();

                    log.LogInformation("Receiving StorageAccount");

                    var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));

                    string blobContainerReference = "beerweather-blobs";
                    CloudBlobContainer blobContainer = await CreateBlobContainer(storageAccount, blobContainerReference);

                    string guid = Guid.NewGuid().ToString();
                    string blobUrl = await CreateCloudBlockBlob(guid, log);

                    log.LogInformation("Created cloud blob: {0}.png", guid);

                    CloudQueueMessage cloudQueueMessage = CreateApiMessage(cityName, countryCode, blobUrl,guid);
                    CloudQueueClient client = storageAccount.CreateCloudQueueClient();
                    await AddMessageToQueue(cloudQueueMessage, client);

                    log.LogInformation("Posted object in queue locations-openweather-in");

                    string result = String.Format("Your beerreport is being generated for {0},{1} and can be found at {2}. This report is accessible " +
                        "for 10 minutes", cityName, countryCode, blobUrl);
                    return new OkObjectResult(result);


                }
                catch
                {
                    return new BadRequestObjectResult("Something strange happened");
                }
            }

        }

        private static async Task AddMessageToQueue(CloudQueueMessage cloudQueueMessage, CloudQueueClient client)
        {
            string openWeatherIn = "locations-openweather-in";
            var cloudQueue = client.GetQueueReference(openWeatherIn);
            await cloudQueue.CreateIfNotExistsAsync();

            await cloudQueue.AddMessageAsync(cloudQueueMessage);
        }

        private static bool CheckIfCountryIsValid(string countryCode)
        {
            bool isValid = false;
            var countries = new RipeISOCountryReader().GetDefault();
            var lookup = new ISOCountryLookup<RipeCountry>(countries);
            lookup.TryGetByAlpha2(countryCode.ToUpper(), out RipeCountry isCode);
            if (isCode != null)
            {
                isValid = true;
            }
            return isValid;
        }

        private static async Task<CloudBlobContainer> CreateBlobContainer(CloudStorageAccount storageAccount, string reference)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(reference);
            await blobContainer.CreateIfNotExistsAsync();

            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Off
            };
            await blobContainer.SetPermissionsAsync(permissions);
            return blobContainer;
        }

        private static CloudQueueMessage CreateApiMessage(string cityName, string countryCode, string blobUrl,
            string guid)
        {
            TriggerMessage l = new TriggerMessage
            {
                CityName = cityName,
                CountryCode = countryCode,
                Blob = blobUrl,
                Guid = guid
            };
            var messageAsJson = JsonConvert.SerializeObject(l);
            var cloudQueueMessage = new CloudQueueMessage(messageAsJson);
            return cloudQueueMessage;
        }
        private async static Task<string> CreateCloudBlockBlob(string guid, ILogger log)
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
            await blobContainer.CreateIfNotExistsAsync();

            var sas = blobContainer.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(10)
            });


            string fileName = String.Format("{0}.png", guid);
            CloudBlockBlob cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
            cloudBlockBlob.Properties.ContentType = "image/png";
            string imageUrl = string.Format("{0}/{1}{2}", blobContainer.StorageUri.PrimaryUri.AbsoluteUri, fileName, sas);
            return imageUrl;
        }

    }
}



