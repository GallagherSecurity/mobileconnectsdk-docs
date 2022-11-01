Database.Setup(resetDatabase: false).Wait();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddNewtonsoftJson();

builder.Services.AddTransient<Database>();

var apiClientFactory = new GglApiClientFactory(
    builder.Configuration["host"],
    builder.Configuration["apiKey"],
    builder.Configuration["clientCertificatePfx"],
    builder.Configuration["clientCertificatePfxPassword"]);

builder.Services.AddSingleton(apiClientFactory);

Configuration = builder.Configuration;

var commandCentreConfig = InitialiseCommandCentreConfiguration(apiClientFactory.CreateClient(), Configuration).Result;

builder.Services.AddSingleton(commandCentreConfig);

builder.Services.AddTransient(_ => apiClientFactory.CreateClient());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program
{
    internal static IConfiguration Configuration { get; private set; }

    public static async Task<CommandCentreConfiguration> InitialiseCommandCentreConfiguration(GglApiClient gglApi, IConfiguration configuration)
    {
        var keyPdfName = configuration["pdfName"] ?? throw new Exception("pdfName not defined in configuration file");
        var keyAccessGroupName = configuration["accessGroupName"] ?? throw new Exception("accessGroupName not defined in configuration file");
        var keyCardTypeName = configuration["cardTypeName"] ?? throw new Exception("cardTypeName not defined in configuration file");

        string keyPersonalDataFieldId;
        string studentAccessGroupHref;
        string rootDivisionHref;
        string cardholderSearchHref;
        string mobileCredentialTypeHref;

        Console.WriteLine("Startup: Connecting to the Command Centre REST api");

        // Always start with the entrypoint to the Command Centre REST api, which is /api
        var apiResponse = await gglApi.GetAsync<ApiResponse>("/api");

        // get the base url for the cardholders feature
        cardholderSearchHref = apiResponse.Features?.Cardholders?.Cardholders?.Href ??
            throw new Exception("ERROR: No cardholders feature in the REST api; Are you licensed for REST cardholders?");
        Console.WriteLine("Success: Found cardholderSearchHref; {0}", cardholderSearchHref);

        // look up the link for the root division to create new cardholders into
        var items = apiResponse.Features.Items ?? throw new Exception("ERROR: No items feature in the REST api; Are you licensed?");

        var itemsSearchHref = items.Items?.Href;
        var divisionResponse = await gglApi.GetAsync<SearchResults<Item>>($"{itemsSearchHref}?type=15&name=\"Root Division\"");

        rootDivisionHref = divisionResponse.Results.FirstOrDefault()?.Href
            ?? throw new Exception("ERROR: Can't find the root division");

        Console.WriteLine("Success: Found the Root Division; it's link is {0}", rootDivisionHref);

        // go look up the search key for the student ID pdf - type 33 is 'personal data field'
        // we expect a JSON response like this:
        // {
        //     "results": [
        //         {
        //             "id": "500",
        //             "name": "Student ID",
        //             "type": {
        //                 "id": "33",
        //                 "name": "Personal Data Field"
        //             }
        //         }
        //     ]
        // }
        var configPdfResponse = await gglApi.GetAsync<SearchResults<Item>>($"{itemsSearchHref}?type=33&name=\"{Uri.EscapeDataString(keyPdfName)}\""); // quoted name for exact string match
        keyPersonalDataFieldId = configPdfResponse.Results.FirstOrDefault()?.Id
            ?? throw new Exception($"ERROR: Can't find the personal data field with name of {keyPdfName}");

        Console.WriteLine("Success: Found PDF called {0}; it's lookup key ID is {1}", keyPdfName, keyPersonalDataFieldId);

        // find the Students access group (type == 2) which contains the Student ID pdf
        var accessGroupResponse = await gglApi.GetAsync<SearchResults<Item>>($"{itemsSearchHref}?type=2&name=\"{Uri.EscapeDataString(keyAccessGroupName)}\""); // quoted name for exact string match
        studentAccessGroupHref = accessGroupResponse.Results.FirstOrDefault()?.Href
            ?? throw new Exception($"ERROR: Can't find the access group with name of {keyAccessGroupName}");

        Console.WriteLine("Success: Found Access Group called {0}; it's link is {1}", keyAccessGroupName, studentAccessGroupHref);

        // go look up the search key for the mobile credential card type
        var cardTypesSearchHref = apiResponse.Features?.CardTypes?.CardTypes?.Href ?? throw new Exception("ERROR: No card types feature in the REST api; Are you licensed?");

        var cardTypesResponse = await gglApi.GetAsync<SearchResults<CardType>>($"{cardTypesSearchHref}?name=\"{Uri.EscapeDataString(keyCardTypeName)}\"");
        // we expect a JSON response like this:
        // {
        //     "results": [
        //         {
        //             "href": "https://localhost:8904/api/card_types/471",
        //             "id": "471",
        //             "name": "Mobile Credential",
        //             "credentialClass": "mobile"
        //         },
        //     ]
        // }
        mobileCredentialTypeHref = cardTypesResponse.Results.FirstOrDefault().Href
            ?? throw new Exception($"ERROR: Can't find the card type with name of {keyCardTypeName}");

        Console.WriteLine("Success: Found Card Type called {0}; it's link is {1}", keyAccessGroupName, mobileCredentialTypeHref);

        return new CommandCentreConfiguration(
            keyPersonalDataFieldId,
            keyPdfName,
            studentAccessGroupHref,
            rootDivisionHref, cardholderSearchHref,
            mobileCredentialTypeHref);
    }
}