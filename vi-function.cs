using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using System.IO;
using System.Threading;
using System.Text;
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System.Configuration;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json.Linq;


namespace CustomCognitive
{
    public static class Function1
    {

        [FunctionName("CustomCognitive")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {

            log.Info("C# HTTP trigger function processed a request.");
         
            var mem = await req.Content.ReadAsStreamAsync();

            mem.Position = 0;
            mem.Seek(0, SeekOrigin.Begin);

            MemoryStream mem2 = new MemoryStream();
            mem.CopyTo(mem2);
            mem2.Position = 0;
            mem2.Seek(0, SeekOrigin.Begin);

            mem.Position = 0;
            mem.Seek(0, SeekOrigin.Begin);
            //llamada web app
            HttpClient client = new HttpClient();
            HttpContent content = new StreamContent(mem);

            var response = await client.PostAsync("<your azure webapp hosting the model", content);
            var responseString = await response.Content.ReadAsStringAsync();
            
            double helmetThreshold = 0.5;
            double personThreshold = 0.5;

            dynamic json = JObject.Parse(responseString);
            var predictions = json.predictions;
            message m = new message();
            foreach (var p in predictions)
            {
                if ((p.tagName == "helmet") && (p.probability > helmetThreshold))
                {
                    m.helmets++;
                }
                if ((p.tagName == "person") && (p.probability > personThreshold))
                {
                    m.persons++;
                }

            }

             SendingMessages(m);
              //enviamos la imagen al blob storage
              sendImage(mem2, m.date);
              
            return mem == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "BAD REQUEST")
                : req.CreateResponse(HttpStatusCode.OK, "OK");
        }



        static void SendingMessages(message m)
        {
            string connectionString = "<your eventhub connection string>";
            string eventHubName = "<your eventhub name>";
            var connectionStringBuilder = new EventHubsConnectionStringBuilder(connectionString)
            {
                EntityPath = eventHubName
            };

            var eventHubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());

            eventHubClient.SendAsync(new EventData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(m))));

         
        }
        public static void sendImage(Stream img, string filename)
        {

            // Retrieve storage account from connection string.
            var connectionstring = "<your storage account endpoint>";

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionstring);
           
            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference("<your container name>");

            // Retrieve reference to a blob named "myblob".

            string path = "<your directory>/" + DateTime.Today.Year + "/" + DateTime.Today.Month + "/" + DateTime.Today.Day + "/img" + filename + ".jpg";
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(path);
            blockBlob.UploadFromStreamAsync(img);

        }


    }

        public class message
        {
            public int helmets;
            public int persons;
            public bool isWarning;
            public bool isSafe;
            public string status;
            public string date;
            public string location;

            public message()
            {
                helmets = 0;
                persons = 0;
                isWarning = false;
                isSafe = true;
                status = "ok";
                date = DateTime.Now.ToString("o");
                location = "<retrieve location and insert here>";

            }


        }

    }

