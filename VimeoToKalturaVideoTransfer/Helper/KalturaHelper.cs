using Kaltura;
using Kaltura.Enums;
using Kaltura.Request;
using Kaltura.Services;
using Kaltura.Types;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using VimeoToKalturaVideoTransfer.Models;

namespace VimeoToKalturaVideoTransfer.Helper
{
    public class KalturaHelper
    {
        public int PartnerId { get; set; }
        public string SecretKey { get; set; }
        public string UserId { get; set; }
        public SessionType SessionType { get; set; }
        public int Expiry { get; set; }
        public string Priveleges { get; set; }

        public KalturaHelper(int partnerId, string secretKey, string userId, SessionType sessionType, int expiry, string priveleges)
        {
            PartnerId = partnerId;
            SecretKey = secretKey;
            UserId = userId;
            SessionType = sessionType;
            Expiry = expiry;
            Priveleges = priveleges;
        }

        public Client GenerateKalturaClient()
        {
            Configuration config = new Configuration();
            config.ServiceUrl = "https://www.kaltura.com/";
            Client client = new Client(config);
            client.ApiVersion = "18.17.0";
            client.ClientTag = "dotnet:22-11-08";
            return client;
        }

        public void GenerateKalturaSession(Client client)
        {
            client.KS = client.GenerateSession(PartnerId, SecretKey, UserId, SessionType, Expiry, Priveleges);
        }

        public bool UploadVideos(string schoolName)
        {
            Client client = GenerateKalturaClient();

            GenerateKalturaSession(client);

            bool done = false;
            bool isSuccess = false;
            var filePath = Directory.GetCurrentDirectory();
            var fileName = string.Concat("KalturaXMLImport_", schoolName.Replace(" ", ""), ".xml");
            var fullFilePath = string.Concat(filePath, "\\", fileName);

            try
            {
                var fileData = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read);
                BulkUploadXmlJobData bulkUploadData = new BulkUploadXmlJobData();
                bulkUploadData.FileName = fileName;
                BulkUploadEntryData bulkUploadEntryData = new BulkUploadEntryData();

                OnCompletedHandler<BulkUpload> handler = new OnCompletedHandler<BulkUpload>(
                      (BulkUpload result, Exception e) =>
                      {
                          PrintObject(result);
                          done = true;
                      });
                MediaService.BulkUploadAdd(fileData, bulkUploadData, bulkUploadEntryData)
                   .SetCompletion(handler)
                   .Execute(client);

                while (!done)
                {
                    Thread.Sleep(100);
                }

                isSuccess = true;
            }
            catch (Exception e)
            {
                return isSuccess;
            }

            return isSuccess;
        }

        public void StartSession(Client client)
        {
            OnCompletedHandler<string> handler = new OnCompletedHandler<string>(
                  (string result, Exception e) =>
                  {
                      PrintObject(result);
                  });
            SessionService.Start(SecretKey, UserId, SessionType, PartnerId, Expiry, Priveleges)
               .SetCompletion(handler)
               .Execute(client);
        }

        public void PrintObject<T>(T obj)
        {
            var t = typeof(T);
            var props = t.GetProperties();
            StringBuilder sb = new StringBuilder();
            foreach (var item in props)
            {
                try
                {
                    sb.Append(item.Name + ": " + item.GetValue(obj, null) + "\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            sb.AppendLine();
            Console.WriteLine(sb.ToString());
        }
    }
}
