using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using PublicHolidays.Helpers;
using PublicHolidays.Models;
using System.Net;
using Location = Microsoft.Graph.Models.Location;

namespace PublicHolidays.Functions
{
    /// <summary>
    /// Functions for populating holidays for a user or all users
    /// </summary>
    public class PopulateHolidays
    {
        private readonly ILogger _logger;
        private readonly GraphServiceClient _graphClient;
        private List<HolidayPack> _holidayPacks = default!;
        private List<string> _categories = default!;
        private List<Holiday> _holidays = default!;

        public PopulateHolidays(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PopulateHolidays>();
            _graphClient = GraphHelper.GetGraphServiceClient();
        }

        /// <summary>
        /// Populates holidays for a user
        /// </summary>
        /// <param name="req">The HttpRequestData object representing the HTTP request. Two query parameters are accepted: category and location</param>
        /// <param name="userPrincipalName">User's userPrincipalName or Azure object ID</param>
        /// <returns>HTTP 200 OK if successful, HTTP 500 Internal Server Error if not</returns>
        /// <exception cref="Exception"></exception>
        [Function("PopulateHolidaysForUser")]
        public async Task<HttpResponseData> PopulateHolidaysForUser([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "PopulateHolidaysForUser/{userPrincipalName}")] HttpRequestData req, string userPrincipalName)
        {
            if (_graphClient == null)
            {
                _logger.LogError("Failed to create GraphServiceClient");
                throw new Exception("Failed to create GraphServiceClient");
            }
            _logger.LogInformation($"Successfully created GraphServiceClient");


            string? categoryFilter = req.Query["category"];
            string? locationFilter = req.Query["location"];

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await LoadHoldiayPacks();
            string filter = string.Join(" or ", _categories.Select(c => $"categories/any(c:c eq '{c}')"));

            Microsoft.Graph.Models.User? user = null;

            try
            {
                user = await _graphClient.Users[userPrincipalName].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = ["Id", "displayName", "mail", "userPrincipalName", "proxyAddresses", "officeLocation", "city", "state", "country"];
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user data");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            if (user == null)
            {
                _logger.LogError("User not found");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            if (user.ProxyAddresses != null && user.ProxyAddresses.Any(addr => addr.StartsWith("SMTP:", StringComparison.OrdinalIgnoreCase)) && user.OfficeLocation != null)
            {
                await ProcessHolidaysForUser(user, filter, categoryFilter, locationFilter);
            }
            else
            {
                if (user.OfficeLocation == null)
                {
                    _logger.LogInformation($"Skipping {user.DisplayName} as no office location is set");
                }
                else
                {
                    _logger.LogInformation($"Skipping {user.DisplayName} as no SMTP address is set");
                }
            }
            return response;
        }

        /// <summary>
        /// Populates holidays for all users
        /// </summary>
        /// <param name="req">The HttpRequestData object representing the HTTP request. Two query parameters are accepted: category and location</param>
        /// <returns>HTTP 200 OK if successful, HTTP 500 Internal Server Error if not</returns>
        /// <exception cref="Exception"></exception>
        [Function("PopulateHolidaysForAllUsers")]
        public async Task<HttpResponseData> PopulateHolidaysForAllUsers([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            if (_graphClient == null)
            {
                _logger.LogError("Failed to create GraphServiceClient");
                throw new Exception("Failed to create GraphServiceClient");
            }
            _logger.LogInformation($"Successfully created GraphServiceClient");

            string? categoryFilter = req.Query["category"];
            string? locationFilter = req.Query["location"];

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await LoadHoldiayPacks();
            string filter = string.Join(" or ", _categories.Select(c => $"categories/any(c:c eq '{c}')"));

            UserCollectionResponse? users = null;

            try
            {
                users = await _graphClient.Users.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = "accountEnabled eq true";
                    requestConfiguration.QueryParameters.Select = ["Id", "displayName", "mail", "userPrincipalName", "proxyAddresses", "officeLocation", "city", "state", "country"];
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user data");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            if (users == null || users.Value!.Count == 0)
            {
                _logger.LogInformation("No users found");
                return response;
            }
            foreach (var user in users.Value)
            {
                if (user.ProxyAddresses != null && user.ProxyAddresses.Any(addr => addr.StartsWith("SMTP:", StringComparison.OrdinalIgnoreCase)) && user.OfficeLocation != null)
                {
                    await ProcessHolidaysForUser(user, filter, categoryFilter, locationFilter);
                }
                else
                {
                    if (user.OfficeLocation == null)
                    {
                        _logger.LogInformation($"Skipping {user.DisplayName} as no office location is set");
                    }
                    else
                    {
                        _logger.LogInformation($"Skipping {user.DisplayName} as no SMTP address is set");
                    }
                }
            }
            return response;
        }


        internal async Task ProcessHolidaysForUser(Microsoft.Graph.Models.User user, string eventFilter, string? categoryfilter, string? locationFilter)
        {
            _logger.LogInformation($"Processing for {user.DisplayName}");
            _logger.LogInformation($"{user.DisplayName} - {user.OfficeLocation}, {user.Country}");

            var mailboxSettings = await _graphClient.Users[user.Id].MailboxSettings.GetAsync();
            _logger.LogInformation($"Successfully retrieved mailbox settings for user: {user.DisplayName}");
            string userTimeZone = mailboxSettings?.TimeZone ?? "UTC";
            _logger.LogInformation($"{user.DisplayName} - timezone: {userTimeZone}");

            List<Event> events = await CalendarEventsHelper.GetAllEvents(_graphClient, user.Id, eventFilter);
            _logger.LogInformation($"{user.DisplayName} has {events.Count} existing holiday events.");

            List<Holiday> filteredHolidays = _holidays;
            if (categoryfilter != null)
            {
                filteredHolidays = filteredHolidays.Where(h => h.category == categoryfilter).ToList();
            }
            if (locationFilter != null)
            {
                filteredHolidays = filteredHolidays.Where(h => h.location != null && h.location.Contains(locationFilter, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            foreach (var holiday in filteredHolidays)
            {
                if (holiday.remove == false)
                {
                    //Check to see if event already exists:
                    string startTime = holiday.date.ToDateTime(new TimeOnly(0, 0)).ToString("yyyy-MM-ddTHH:mm:ss");
                    DateTimeTimeZone eventStart = new DateTimeTimeZone
                    {
                        DateTime = startTime,
                        TimeZone = userTimeZone
                    };
                    var existingEvent = events
                        .Where(ev => ev.Subject == holiday.name &&
                                     DateOnly.FromDateTime(DateTime.Parse(ev.Start.DateTime)) == holiday.date)
                        .FirstOrDefault();
                    if (existingEvent == null)
                    {
                        bool oof = false;
                        if (holiday.location == null || holiday.outOfOffice == false)
                        {
                            oof = false;
                        }
                        else
                        {
                            oof = holiday.location.Contains(user.OfficeLocation, StringComparer.OrdinalIgnoreCase) || holiday.location.Contains(user.City, StringComparer.OrdinalIgnoreCase) || holiday.location.Contains(user.State, StringComparer.OrdinalIgnoreCase) || holiday.location.Contains(user.Country, StringComparer.OrdinalIgnoreCase);
                        }
                        _logger.LogInformation($"Adding {holiday.name} to {user.DisplayName}'s calendar");
                        Event thisEvent = CreateEvent(holiday.name, holiday.date, holiday.location, userTimeZone, oof, holiday.category, holiday.info);
                        await _graphClient.Users[user.Id].Calendar.Events.PostAsync(thisEvent);
                    }
                    else
                    {
                        _logger.LogInformation($"{existingEvent.Subject} already exists");
                        if (holiday.location == null || holiday.location.Length == 0) { continue; }
                        Array.Sort(holiday.location);
                        if (existingEvent.Location.DisplayName != string.Join(", ", holiday.location))
                        {
                            string[] oldLocations = existingEvent.Location.DisplayName.Split(", ");
                            string[] newLocations = oldLocations
                                .Concat(holiday.location)
                                .Distinct()
                                .OrderBy(l => l)
                                .ToArray();
                            existingEvent.Location = new Location { DisplayName = string.Join(", ", newLocations) };
                            bool oof = false;
                            if (holiday.location == null || holiday.outOfOffice == false)
                            {
                                oof = false;
                            }
                            else
                            {
                                oof = holiday.location.Contains(user.OfficeLocation, StringComparer.OrdinalIgnoreCase) || holiday.location.Contains(user.City, StringComparer.OrdinalIgnoreCase) || holiday.location.Contains(user.State, StringComparer.OrdinalIgnoreCase) || holiday.location.Contains(user.Country, StringComparer.OrdinalIgnoreCase);
                            }

                            existingEvent.ShowAs = oof ? FreeBusyStatus.Oof : FreeBusyStatus.Free;
                            await _graphClient.Users[user.Id].Calendar.Events[existingEvent.Id].PatchAsync(existingEvent);
                            _logger.LogInformation($"{existingEvent.Subject} location updated to {existingEvent.Location.DisplayName}");
                        }
                        else
                        {
                            _logger.LogInformation($"{existingEvent.Subject} location already correct. No changes required.");
                        }
                    }
                }
                else
                {
                    //Remove an existing holiday
                    string startTime = holiday.date.ToDateTime(new TimeOnly(0, 0)).ToString("yyyy-MM-ddTHH:mm:ss");
                    DateTimeTimeZone eventStart = new DateTimeTimeZone
                    {
                        DateTime = startTime,
                        TimeZone = userTimeZone
                    };
                    Event existingEvent = events.Where(ev => ev.Subject == holiday.name && ev.Start.DateTime == eventStart.DateTime).FirstOrDefault();
                    if (existingEvent != null)
                    {
                        await _graphClient.Users[user.Id].Calendar.Events[existingEvent.Id].DeleteAsync();
                        _logger.LogInformation($"Deleted event {existingEvent.Subject} on {holiday.date:yyyy-MM-dd}");
                    }
                }

            }
        }

        internal static Event CreateEvent(string name, DateOnly start, string[]? location, string timezone, bool oof, string category, string info)
        {

            string startTime = start.ToDateTime(new TimeOnly(0, 0)).ToString("yyyy-MM-ddTHH:mm:ss");
            string endTime = start.AddDays(1).ToDateTime(new TimeOnly(0, 0)).ToString("yyyy-MM-ddTHH:mm:ss");
            if (location != null && location.Length > 0) { Array.Sort(location); }

            var holiday = new Event
            {
                Subject = name,
                Start = new DateTimeTimeZone
                {
                    DateTime = startTime,
                    TimeZone = timezone
                },
                End = new DateTimeTimeZone
                {
                    DateTime = endTime,
                    TimeZone = timezone
                },
                Location = location == null ? null : new Location { DisplayName = string.Join(", ", location) },
                IsAllDay = true,
                ShowAs = oof ? FreeBusyStatus.Oof : FreeBusyStatus.Free,
                Categories = new List<string> { category },
                Body = new ItemBody { Content = info },
                IsReminderOn = false
            };

            return holiday;
        }

        internal async Task LoadHoldiayPacks()
        {
            _holidayPacks = await HolidayPackHelper.GetHolidayPacks();
            _categories = new List<string>();
            foreach (var holidayPack in _holidayPacks)
            {
                _logger.LogInformation($"Holiday pack {holidayPack.id} contains {holidayPack.holidays.Length} holidays");
                if (!_categories.Contains(holidayPack.category))
                {
                    _categories.Add(holidayPack.category);
                }
            }

            _holidays = HolidayPackHelper.CombineHolidayPacks(_holidayPacks);
            _logger.LogInformation($"{_holidays.Count} unique holidays identified");
        }
    }
}
