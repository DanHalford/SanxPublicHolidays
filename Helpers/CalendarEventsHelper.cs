using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace PublicHolidays.Helpers
{
    internal class CalendarEventsHelper
    {
        internal static async Task<List<Event>> GetAllEvents(GraphServiceClient graphClient, string? Id, string? filter)
        {
            List<Event> allEvents = new List<Event>();

            // Fetch the first page of events
            var pagedEvents = await graphClient.Users[Id].Calendar.Events
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = filter;
                });

            // Add the first page of events to the list
            if (pagedEvents.Value != null)
            {
                allEvents.AddRange(pagedEvents.Value);
            }

            // While there's a next page, fetch the next set of results
            while (pagedEvents.OdataNextLink != null)
            {
                // Get the next page of results
                pagedEvents = await graphClient.Users[Id].Calendar.Events.WithUrl(pagedEvents.OdataNextLink).GetAsync();

                // Add the next page of events to the list
                if (pagedEvents.Value != null)
                {
                    allEvents.AddRange(pagedEvents.Value);
                }
            }

            return allEvents;
        }
    }
}
