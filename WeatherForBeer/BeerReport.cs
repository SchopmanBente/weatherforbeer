
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;

namespace WeatherForBeer
{
    public static class BeerReport
    {
        

        [FunctionName("BeerReport")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["url"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;


            if (!string.IsNullOrEmpty(name))
            {
                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer blobContainer = blobClient.GetContainerReference("beerweather-blobs");
                await blobContainer.CreateIfNotExistsAsync();

                // Set the permissions so the blobs are public. 
                BlobContainerPermissions permissions = new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                };
                await blobContainer.SetPermissionsAsync(permissions);
                CloudBlockBlob blockBlob = blobContainer.GetBlockBlobReference(name);
                blockBlob.Properties.ContentType = "image/png";
                var response = await blockBlob.OpenReadAsync();
                return new OkObjectResult(response);


            }
            else
            {
                return new BadRequestObjectResult("Please pass a name on the query string or in the request body");
            }
           
        }
    }
}
