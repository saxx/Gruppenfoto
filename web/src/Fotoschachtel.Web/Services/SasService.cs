﻿using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace Fotoschachtel.Services
{
    public class SasService
    {
        private readonly Settings _settings;

        public SasService([NotNull] IOptions<Settings> settings)
        {
            _settings = settings.Value;
        }


        public async Task<SasToken> GetSasForContainer([CanBeNull] string @event, [CanBeNull] string containerName)
        {
            if (string.IsNullOrWhiteSpace(@event))
            {
                throw new Exception("No event specified.");
            }
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new Exception("No container name specified.");
            }
            if (string.IsNullOrWhiteSpace(_settings.AzureStorageContainer) || string.IsNullOrWhiteSpace(_settings.AzureStorageKey))
            {
                throw new Exception("Azure storage configuration missing");
            }
            
            var storageAccount = new CloudStorageAccount(new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(_settings.AzureStorageContainer, _settings.AzureStorageKey), true);
            var storageClient = storageAccount.CreateCloudBlobClient();

            var container = storageClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            var serviceProperties = await storageClient.GetServicePropertiesAsync();
            serviceProperties.Cors.CorsRules.Clear();
            serviceProperties.Cors.CorsRules.Add(new CorsRule
            {
                AllowedMethods = CorsHttpMethods.Get | CorsHttpMethods.Put | CorsHttpMethods.Post,
                AllowedOrigins = { "*" },
                AllowedHeaders = { "x-ms-*", "content-type", "accept", "content-length" },
                MaxAgeInSeconds = 60 * 60
            });
            await storageClient.SetServicePropertiesAsync(serviceProperties);

            var sasExpiration = DateTime.UtcNow.AddHours(12);
            var sas = container.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write,
                SharedAccessExpiryTime = sasExpiration,
            });

            return new SasToken
            {
                EventId = @event,
                SasQueryString = sas,
                ContainerUrl = container.Uri.ToString(),
                SasExpiration = sasExpiration
            };
        }


        [NotNull]
        public static string GetContainerName([NotNull] string eventId)
        {
            eventId = eventId.ToLowerInvariant();
            eventId = eventId.Replace(" ", "-");
            eventId = eventId.Replace("_", "-");
            eventId = eventId.Replace("---", "-").Replace("--", "-");
            eventId = eventId.Trim('-');
            eventId = eventId.PadRight(3, '0');
            return eventId;
        }


        public class SasToken
        {
            public string EventId { get; set; }
            public string ContainerUrl { get; set; }
            public string SasQueryString { get; set; }
            public DateTime SasExpiration { get; set; }

            public string SasListUrl => ContainerUrl + SasQueryString + "&comp=list&restype=container&prefix=thumbnails-small";
        }
    }
}
