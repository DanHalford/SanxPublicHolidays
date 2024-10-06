# Sanx Public Holidays

**Sanx Public Holidays** is an open source solution that makes organisational management of calendar entries for events, public holidays and cultural observances easier. It is an Azure Function App that uses the [Microsoft Graph API](https://developer.microsoft.com/en-us/graph) to create and update calendar entries in a user's Outlook calendar. The solution is designed to be deployed in a Microsoft 365 environment and cannot be used with hybrid or on-premises Exchange servers.

Holiday event data is taken from one or more data files stored within the storage account associated with the Azure Function App. The data files are in JSON format and contain the following information for each holiday:

- Holiday date: The date of the holiday. This will be timeshifted to be an all-day event in the user's calendar, based on the user's time zone within their mailbox settings.
- Holiday name: The name of the holiday.
- [Location]: The location(s) the holiday is observed. If present, this property is matched to the user's location (as defined in Entra ID, specifically the *Office Location*, *City* or *Country* fields) to determine if the calendar event should be marked as Out Of Office. The content of the Location property is also added to the Location field of the calendar event.
- [Info]: Additional information about the holiday, which gets added to the body of the calendar event.
- [Out Of Office]: A boolean value that determines if the calendar event should be marked as Out Of Office. If not present, the default value is true.

Collections of holidays can be grouped into packs, with each pack being labelled with a unique identifier and a category; a tag added to the calendar event, such as 'Public Holiday' or 'Cultural Observance'. The solution can contain any number of packs. When adding holidays, a superset of all packs is created and used to update the user's calendar. Duplicate events with the same name and date, but different locations, are merged into a single event with the locations combined.

When processing changes for orgnisation-wide, only users with valid mailboxes and a defined *Office Location* field are considered.

## Integration

The solution has three public http trigger endpoints:
- **ClearHolidays/{userPrincipalName | userId}**: Deletes all holiday events from the user's calendar.
- **PopulateHolidaysForUser/{userPrincipalName | userId}**: Adds holiday events to the user's calendar.
- **PopulateHolidays**: Adds holiday events to the calendars of all users in the organisation.

This allows the solution to easily to called by user automation scripts or scheduled tasks.

## Holiday Packs

For example holiday packs, see the **Sample Holiday Data** folder within the solution. This folder contains current public holiday information for Australia, New Zealand, Fiji, the Australian state of Queensland and the Auckland metropolitan area in New Zealand. There is also a holiday pack containing a small selection of cultural observances.

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
            "format": "uri",
            "description": "An optional URL providing more information about the holiday."
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
   - **MailboxSettings.Read**: Allows the app to read all users' full profiles.
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