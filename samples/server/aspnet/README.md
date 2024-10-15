## Disclaimer

This project contains sample code that is provided **for educational purposes only**. The code in this repository is **not actively maintained** and should **not be used in production environments**.

- The sample code does not receive security updates or bug fixes.
- There is no guarantee of stability, performance, or correctness.
- **Do not use this code in production systems** without proper security and performance reviews.

## Pre-requisites

You should read the document **Mobile Connect SDK Integration Guide TIP** in conjunction with this sample application.

## Command Centre Pre-requisites

This sample application assumes the Command Centre REST api is running.  
NOTE: Please check "host" in appsettings.json is correct for your server.

You will need to configure the following in Command Centre:

- An operator account with the following privileges: 
- (these are the minimum required privileges for the functions used in the app)
  -> Create and Edit Cardholders
  -> Delete Cardholders
  -> View Personal Data Definitions
  -> View Site (to view Divisions and Access Groups)
  -> Modify Access Control (to assign Cardholders to Access Groups)

- A REST client set to use that operator account.  
  *NOTE:* You will need to put the sha1 thumbprint of the sample client certificate into Command Centre. See appsettings.json
  *NOTE:* You will need to put the generated api key in appsettings.json

- A personal data field called "Student ID" - Text or Numeric format.  
  *NOTE:* If you wish to use a different PDF, put it's name in appsettings.json

- An access group called "Students Access Group".  
 *NOTE:* If you wish to use a different access group, put it's name in appsettings.json

- A Mobile Credential card type called "Mobile Credential". Command Centre has this by default already so you should not need to create it.  
  *NOTE:* If you wish to use a different card type, put it's name in appsettings.json

## Software Pre-requisites:

You will need developer tools capable of compiling and running a .NET 6 Console application.
There are multiple ways of achieving this:

**Option 1.** Use Visual Studio 2022 version 17.3.3 or higher.
   Once you open the GallagherUniversityStudentPortalSampleSite.sln file in visual studio, simply build and run it.

**Option 3.** Use Visual Studio Code with the "C# for Visual Studio Code (powered by OmniSharp)" extension.
   Once installed, open the project directory in VSCode and select "Start Debugging" from the Debug menu.
   You will also need to install the .NET 6 SDK from https://dotnet.microsoft.com/en-us/download/visual-studio-sdks (this was developed against SDK 6.0.8)

**Option 2.** Command Line Only:
   Install the .NET 6 SDK from https://dotnet.microsoft.com/en-us/download/visual-studio-sdks (this was developed against SDK 6.0.8)
   Once the SDK is installed, open a command prompt window, navigate to the directory containing GallagherUniversityStudentPortalSampleSite.sln, and type 
   dotnet run

**Option 3.** Use Visual Studio 2022 for Mac version 17.3 or higher.
   Once you open the GallagherUniversityStudentPortalSampleSite.sln file in visual studio, simply build and run it.
 
Once you have compiled and run the sample site, point your browser at http://localhost:5000

Once up and running, first select "I am an Administrator" then create a new cardholder.
Second, go to the "I am a Student" area, log in as the cardholder you just created, and request a new mobile credential

## Tips:

The easiest and recommended way to connect to the Command Centre REST api is to create an instance of System.Net.Http.HttpClient and configure it to use
the correct headers and client certificate settings. See the code in GglApiClient for an example.

To create and edit cardholders and mobile credentials, you will need to download a variety of different pieces of data from CommandCentre.
See the InitialiseCommandCentreConfiguration method in Startup.cs as an example of how you might decide to do this.

If you wish to serialize the JSON produced and consumed by the Command Centre REST api into C# objects, you can refer to the class definitions in GglApi.cs.
Note however: These classes are included for sample code purposes. They do NOT represent a fully supported C# API client library and you should use them
as a quick-start/reference to get up and running rather than a full data definition.