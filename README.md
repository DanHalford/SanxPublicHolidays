# Sanx Public Holidays

**Sanx Public Holidays** is an open source solution that makes organisational management of calendar entries for events, public holidays and cultural observances easier. It is an Azure Function App that uses the [Microsoft Graph API](https://developer.microsoft.com/en-us/graph) to create and update calendar entries in a user's Outlook calendar. The solution is designed to be deployed in a Microsoft 365 environment and cannot be used with hybrid or on-premises Exchange servers.

Holiday event data is taken from one or more data files stored within the storage account associated with the Azure Function App. The data files are in JSON format and contain the following information for each holiday:

- Holiday date: The date of the holiday. This will be timeshifted to be an all-day event in the user's calendar, based on the user's time zone within their mailbox settings.
- Holiday name: The name of the holiday.
- [Location]: The location(s) the holiday is observed. If present, this property is matched to the user's location (as defined in Entra ID, specifically the *Office Location*, *City*, *State or Province** or *Country* fields) to determine if the calendar event should be marked as Out Of Office. The content of the Location property is also added to the Location field of the calendar event. Each holiday can have multiple locations.
- [Info]: Additional information about the holiday, which gets added to the body of the calendar event.
- [Out Of Office]: A boolean value that determines if the calendar event should be marked as Out Of Office. If not present, the default value is true.

Collections of holidays can be grouped into packs, with each pack being labelled with a unique identifier and a category; a tag added to the calendar event, such as 'Public Holiday' or 'Cultural Observance'. The solution can contain any number of packs. When adding holidays, a superset of all packs is created and used to update the user's calendar. Duplicate events with the same name and date, but different locations, are merged into a single event with the locations combined.

When processing changes for orgnisation-wide, only users with valid mailboxes and a defined *Office Location* field are considered.

## Integration

The solution has three public http trigger endpoints:
- **ClearHolidays/{userPrincipalName | userId}**: Deletes all holiday events from the user's calendar.
- **PopulateHolidaysForUser/{userPrincipalName | userId}**: Adds holiday events to the user's calendar.
- **PopulateHolidaysForAllUsers**: Adds holiday events to the calendars of all users in the organisation.

This allows the solution to easily to called by user automation scripts or scheduled tasks.

Both the **PopulateHolidaysForUser** and **PopulateHolidaysForAllUsers** endpoints will accept two query parameters to filter the holiday packs that are processed:
- **category**: The category of the holiday pack to process. If not provided, all packs are processed.
- **location**: Only holidays containing the specified location will be processed. If not provided, all holidays are processed.

### Return data and logging

The functions above return no data via HTTP other than HTTP response codes:
- **200 OK** when things have worked
- **500 Internal Server Error** when things haven't

Internal logging is used to record comprehensive data about the application's operation. You can view the logs in real time using *Log Stream* on the Azure Function App.

## Holiday Packs

For example holiday packs, see the **Sample Holiday Data** folder within the solution. This folder contains current public holiday information for Australia, New Zealand, Fiji, the Australian state of Queensland and the Auckland metropolitan area in New Zealand. There is also a holiday pack containing a small selection of cultural observances.

Each holiday item can contain the following properties:
- **date**: [Mandatory] The date of the holiday in ISO format (YYYY-MM-DD).
- **name**: [Mandatory] The name of the holiday.
- **location**: [Optional] An array of locations where the holiday applies. The location(s) are matched to the user's location (as determined by the user's Office Location, City, State or Province, and Country Entra ID fields) to determine if the calendar event should be marked as Out Of Office. Each holiday can have multiple locations.
- **info**: [Optional] Information about the holiday. This field is added to the body of the calendar event.
- **outOfOffice**: [Optional] A boolean value that determines if the calendar event should be marked as Out Of Office if the event location matches the user's location. If not present, the default value is true.

The JSON data schema for the holiday packs is shown below:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "id": {
      "type": "string",
      "format": "uuid",
      "description": "A unique identifier for the holiday pack."
    },
    "category": {
      "type": "string",
      "description": "The category of the holiday pack, e.g., 'Public Holidays'."
    },
    "holidays": {
      "type": "array",
      "description": "A list of holidays included in this pack.",
      "items": {
        "type": "object",
        "properties": {
          "date": {
            "type": "string",
            "format": "date",
            "description": "The date of the holiday in ISO format (YYYY-MM-DD)."
          },
          "name": {
            "type": "string",
            "description": "The name of the holiday."
          },
          "location": {
            "type": "array",
            "description": "An array of locations where the holiday applies.",
            "items": {
              "type": "string"
            }
          },
          "info": {
            "type": "string",
            "description": "An optional field providing more information about the holiday."
          },
          "outOfOffice": {
            "type": "boolean",
            "description": "Indicates whether the user is marked as out of office for this holiday. Defaults to true if not present."
          }
        },
        "required": ["date", "name"],
        "default": {
          "outOfOffice": true
        }
      }
    }
  },
  "required": ["id", "category", "holidays"]
}
```

## Deployment

To deploy the solution into your Azure tenancy, you must do the following:
1. Create an application registration in Entra ID with the following API permissions:
   - **Calendars.ReadWrite**: Allows the app to read and write events in user calendars.
   - **MailboxSettings.Read**: Allows the app to read all users' mailbox settings. Required to determine the user mailbox time zone.
   - **User.Read.All**: Allows the app to read directory data.
1. Create a client secret for the application registration. Make a note of the Application (client) ID, Directory (tenant) ID and the client secret; you'll need them later.
1. Create a new Azure Function App in the Azure portal. I would recommend using the Consumption plan for this solution; it's probably not something that will be running all day, every day.
1. Choose .NET as the runtime stack and the 8 (LTS) version of the runtime. The Operating System can be either Windows or Linux, but I would recommend Windows as that's what my test environment has been.
1. Create a new storage account for the function app. This is where the holiday data files will be stored.
1. I'd recommend enabling Application Insights for the function app. It's not required, but it can be useful for debugging.
1. Whatever you do, do *not* enable CD from this GitHub repository; do not trust my code to work in your environment. Instead, clone the repository to your local machine and publish the code to the function app using Visual Studio or the Azure Functions Core Tools.
1. Once the Azure Function All has been created, create the following environment variables:
   - **AZURE_TENANT_ID**: The Directory (tenant) ID of the application registration.
   - **AZURE_CLIENT_ID**: The Application (client) ID of the application registration.
   - **AZURE_CLIENT_SECRET**: The client secret of the application registration.
   - **HolidayDataConnectionString**: The connection string for the storage account. This will usually be exactly the same as the **AzureWebJobsStorage** connection string.
   - **HolidayDataContainerName**: The name of the container in the storage account where the holiday data files are stored. You haven't created this container yet, but you will, so choose a name and make a note of it.
1. Create the **HolidayDataContainerName** container in the storage account and upload the holiday data files to it. Personally, I use the Azure Storage Explorer for this, but you can use the Azure portal if you prefer.

## Security

As mentioned above, the application requires an application registration in Entra ID with three API permissions. There is no requirement for any highler level of access, and the application does not require any delegated permissions. As a general rule, applications (and users...) should not be granted more permissions than they need to require to operate.

The three API permissions are required for the following reasons:
- **Calendars.ReadWrite**: This permission is required to create, update, and delete calendar events in the user's calendar. The application also examines existing calendar events to determine if they need to be updated or deleted. The content (body) of existing calendar events is not read; only the subject, location, start date, and event category are examined.
- **MailboxSettings.Read**: This permission is required to determine the user's mailbox time zone. This is used to timeshift the holiday date to an all-day event in the user's calendar.
- **User.Read.All**: This permission is required to read the user's account information from the Entra ID profile. The application uses the **Office Location**, **City**, **State or Province** and **Country** fields to determine the user's location. This is used to determine if the calendar event should be marked as Out Of Office. In addition, the application reads the user's **User Principal Name** and **ID** fields to be able to reference the correct account.

The application does not have any requirement for transitory storage and does not store any personal or user account data. The only data stored is the holiday data files in the storage account, which are read and processed by the application. The application does not store any data in the user's mailbox or calendar.
