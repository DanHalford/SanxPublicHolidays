using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using PublicHolidays.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PublicHolidays.Helpers
{
    internal class HolidayPackHelper
    {
        internal static async Task<List<HolidayPack>> GetHolidayPacks()
        {
            string connectionString = Environment.GetEnvironmentVariable("HolidayDataConnectionString");
            string containerName = Environment.GetEnvironmentVariable("HolidayDataContainerName");
            List<HolidayPack> holidayPacks = new List<HolidayPack>();

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (!await containerClient.ExistsAsync())
            {
                throw new Exception($"Container {containerName} does not exist");
            }

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                BlobDownloadInfo download = await blobClient.DownloadAsync();

                using (var reader = new StreamReader(download.Content))
                {
                    string jsonContent = await reader.ReadToEndAsync();
                    HolidayPack holidayPack = JsonConvert.DeserializeObject<HolidayPack>(jsonContent);
                    holidayPacks.Add(holidayPack);
                }
            }
            return holidayPacks;
        }

        internal static List<Holiday> CombineHolidayPacks(List<HolidayPack> holidayPacks)
        {
            var holidayDict = new Dictionary<(string, DateOnly), Holiday>();

            foreach (var pack in holidayPacks)
            {
                foreach (var holiday in pack.holidays)
                {
                    // Create a key based on holiday name and date
                    var key = (holiday.name, holiday.date);

                    // Check if the holiday with the same name and date already exists in the dictionary
                    if (holidayDict.ContainsKey(key))
                    {
                        // If it exists, combine the locations and remove duplicates
                        var existingHoliday = holidayDict[key];
                        var combinedLocations = existingHoliday.location
                            .Concat(holiday.location)       // Combine the two arrays
                            .Distinct()                     // Remove duplicates
                            .OrderBy(loc => loc)            // Sort alphabetically
                            .ToArray();                     // Convert back to an array

                        // Update the existing holiday with the new combined location
                        existingHoliday.location = combinedLocations;
                    }
                    else
                    {
                        // If it doesn't exist, add the holiday to the dictionary
                        holidayDict[key] = new Holiday
                        {
                            name = holiday.name,
                            date = holiday.date,
                            location = holiday.location.OrderBy(loc => loc).ToArray(), // Sort locations
                            category = pack.category,
                            info = holiday.info,
                            remove = holiday.remove
                        };
                    }
                }
            }

            // Return the combined holiday list from the dictionary
            return holidayDict.Values.ToList();
        }
    }
}
