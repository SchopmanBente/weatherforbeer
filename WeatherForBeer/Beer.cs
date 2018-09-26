
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
        public async static Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequest req, ILogger log)
        {

            string city = req.Query["city"];
            string country = req.Query["country"];
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            string result = "Please pass a name on the query string or in the request body";
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            city = city ?? data?.city;
            country = country ?? data?.country;
            if (string.IsNullOrWhiteSpace(country) || string.IsNullOrEmpty(country) ||  string.IsNullOrWhiteSpace(city) ||
                string.IsNullOrEmpty(city) || country.Length != 2 || !CheckIfCountryIsValid(country) || !Regex.IsMatch(city, @"^[a-z]+$"))
            {
                return new BadRequestObjectResult("The input is not valid");
            }
            else
            {
                try
                {
                    country = country.ToLower();
                    city = city.ToLower();

                    log.LogInformation("Receiving StorageAccount");

                    var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));

                    string blobContainerReference = "beerweather-blobs";
                    CloudBlobContainer blobContainer = await CreateBlobContainer(storageAccount, blobContainerReference);

                    string guid = Guid.NewGuid().ToString();
                    string blobUrl = await RetrieveCloudBlockBlob(guid, log);

                    log.LogInformation("Created cloud blob: {0}.png", guid);

                    CloudQueueMessage cloudQueueMessage = CreateApiMessage(city, country, blobUrl, blobContainerReference, guid);
                    CloudQueueClient client = storageAccount.CreateCloudQueueClient();
                    await AddMessageToQueue(cloudQueueMessage, client);

                    log.LogInformation("Posted object in queue locations-openweather-in");

                    result = String.Format("Your beerreport is being generated for {0},{1} and can be found at <a>{2}</a>. This report is accessible" +
                        "for 10 minutes", city, country, blobUrl);
                    return new OkObjectResult(result);


                }
                catch
                {
                    return new BadRequestObjectResult(result);
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
            RipeCountry isCode = null;
            lookup.TryGetByAlpha2(countryCode.ToUpper(), out isCode);
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

            // Set the permissions so the blobs are public. 
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Off
            };
            await blobContainer.SetPermissionsAsync(permissions);
            return blobContainer;
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



