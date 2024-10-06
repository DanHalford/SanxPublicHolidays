using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using PublicHolidays.Helpers;
using PublicHolidays.Models;

namespace PublicHolidays.Functions
{
    public class ClearHolidays
    {
        private readonly ILogger _logger;

        public ClearHolidays(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ClearHolidays>();
        }

        [Function("ClearHolidays")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "ClearHolidays/{userPrincipalName}")] HttpRequestData req, string userPrincipalName)
        {
            _logger.LogInformation("ClearHolidays function triggered");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            List<string> categories = new List<string>();
            List<HolidayPack> holidayPacks = await HolidayPackHelper.GetHolidayPacks();
            foreach (var holidayPack in holidayPacks)
            {
                if (!categories.Contains(holidayPack.category))
                {
                    categories.Add(holidayPack.category);
                }
            }
            string filter = string.Join(" or ", categories.Select(c => $"categories/any(c:c eq '{c}')"));

            GraphServiceClient graphClient = GraphHelper.GetGraphServiceClient();
            if (graphClient == null)
            {
                _logger.LogError("Failed to create GraphServiceClient");
                throw new Exception("Failed to create GraphServiceClient");
            }

            _logger.LogInformation($"Successfully created GraphServiceClient");

            List<Event> events = null;
            try
            {
                events = await CalendarEventsHelper.GetAllEvents(graphClient, userPrincipalName, filter);
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get events");
                _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            if (events == null || events.Count == 0)
            {
                _logger.LogInformation("No events found.");
            }
            else
            {
                foreach (var calendarEvent in events)
                {
                    await graphClient.Users[userPrincipalName].Calendar.Events[calendarEvent.Id].DeleteAsync();
                    _logger.LogInformation($"Deleted event {calendarEvent.Subject} on {calendarEvent.Start.DateTime:yyyy-MM-dd}");
                }
                _logger.LogInformation($"{events.Count} events removed.");
            }

            return response;
        }
    }
}
