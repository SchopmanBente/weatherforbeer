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
                        double windSpeed = weatherRootObject.Wind.Speed;
                        string tekst2 = string.Format("Temp: {0} °C Wind:{1} km/u",tempInCelsius, windSpeed);
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

        private async Task<CloudBlockBlob> RetrieveCloudBlockBlob(BlobTriggerMessage l, ILogger log)
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
            await blobContainer.CreateIfNotExistsAsync();

            string sharedAccessPolicyName = "TestPolicy";
            //await CreateSharedAccessPolicy(blobContainer, sharedAccessPolicyName);


            string fileName = String.Format("{0}.png", l.Guid);
            CloudBlockBlob cloudBlockBlob = blobContainer.GetBlockBlobReference(fileName);
            cloudBlockBlob.Properties.ContentType = "image/png";
            CreateSharedAccesSignatureForCloudBlockBlob(cloudBlockBlob, sharedAccessPolicyName);
            return cloudBlockBlob;
        }

        public string CreateSharedAccesSignatureForCloudBlockBlob(CloudBlockBlob cloudBlockBlob, string policyName)
        {
            string sasToken = cloudBlockBlob.GetSharedAccessSignature(null, policyName);
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}", cloudBlockBlob.Uri, sasToken);
        }

        /*
        private async Task CreateSharedAccessPolicy(CloudBlobContainer blobContainer, string policyName)
        {
           
            SharedAccessBlobPolicy storedPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read 
                  
            };

            //let's start with a new collection of permissions (this wipes out any old ones)
            BlobContainerPermissions permissions = new BlobContainerPermissions();
            permissions.SharedAccessPolicies.Clear();
            permissions.SharedAccessPolicies.Add(policyName, storedPolicy);
            await blobContainer.SetPermissionsAsync(permissions);
        } */

    }
}
