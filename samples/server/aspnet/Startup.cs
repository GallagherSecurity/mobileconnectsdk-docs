using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GallagherUniversityStudentPortalSampleSite.Services;
using GglApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#nullable enable

namespace GallagherUniversityStudentPortalSampleSite
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            Database.Setup(resetDatabase: false).Wait();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews()
                .AddNewtonsoftJson();

            services.AddTransient<Services.Database>();

            var apiClientFactory = new GglApiClientFactory(
                Configuration["host"],
                Configuration["apiKey"],
                Configuration["clientCertificatePfx"],
                Configuration["clientCertificatePfxPassword"]);

            services.AddSingleton(apiClientFactory);

            var commandCentreConfig  = InitialiseCommandCentreConfiguration(apiClientFactory.CreateClient(), Configuration).Result;

            services.AddSingleton(commandCentreConfig);

            services.AddTransient(_ => apiClientFactory.CreateClient());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        public async Task<CommandCentreConfiguration> InitialiseCommandCentreConfiguration(GglApiClient gglApi, IConfiguration configuration)
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
            var configPdfResponse = await gglApi.GetAsync<SearchResults<Item>>($"{itemsSearchHref}?type=33&name=\"{Uri.EscapeUriString(keyPdfName)}\""); // quoted name for exact string match
            keyPersonalDataFieldId = configPdfResponse.Results.FirstOrDefault()?.Id
                ?? throw new Exception($"ERROR: Can't find the personal data field with name of {keyPdfName}");

            Console.WriteLine("Success: Found PDF called {0}; it's lookup key ID is {1}", keyPdfName, keyPersonalDataFieldId);

            // find the Students access group (type == 2) which contains the Student ID pdf
            var accessGroupResponse = await gglApi.GetAsync<SearchResults<Item>>($"{itemsSearchHref}?type=2&name=\"{Uri.EscapeUriString(keyAccessGroupName)}\""); // quoted name for exact string match
            studentAccessGroupHref = accessGroupResponse.Results.FirstOrDefault()?.Href
                ?? throw new Exception($"ERROR: Can't find the access group with name of {keyAccessGroupName}");

            Console.WriteLine("Success: Found Access Group called {0}; it's link is {1}", keyAccessGroupName, studentAccessGroupHref);

            // go look up the search key for the mobile credential card type
            var cardTypesSearchHref = apiResponse.Features?.CardTypes?.CardTypes?.Href ?? throw new Exception("ERROR: No card types feature in the REST api; Are you licensed?");

            var cardTypesResponse = await gglApi.GetAsync<SearchResults<CardType>>($"{cardTypesSearchHref}?name=\"{Uri.EscapeUriString(keyCardTypeName)}\"");
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

    public class CommandCentreConfiguration
    {
        public CommandCentreConfiguration(
            string keyPersonalDataFieldId,
            string keyPersonalDataFieldName,
            string studentAccessGroupHref,
            string rootDivisionHref,
            string cardholderSearchHref,
            string mobileCredentialTypeHref)
        {
            KeyPersonalDataFieldId = keyPersonalDataFieldId;
            KeyPersonalDataFieldName = keyPersonalDataFieldName;
            StudentAccessGroupHref = studentAccessGroupHref;
            RootDivisionHref = rootDivisionHref;
            CardholderSearchHref = cardholderSearchHref;
            MobileCredentialTypeHref = mobileCredentialTypeHref;
        }

        // When searching for cardholders in command centre using a personal data field, we need the numeric
        // identifier of the personal data field itself. For example, the "Student ID" pdf may have an id of 500
        // So we need to use the "items" feature of the rest api to find that numeric id and store it for later use
        public string KeyPersonalDataFieldId { get; }
        public string KeyPersonalDataFieldName { get; }

        // When creating new cardholders we need to specify a Division and Access Group to associate them with
        // These are provided to the create cardholder call as href links to the item
        public string StudentAccessGroupHref { get; }
        public string RootDivisionHref { get; }

        // When searching for cardholders in command centre, we do a GET to a given URL.
        // In the current release of Command Centre, it's always '/api/cardholders'
        // However following the principles of REST and HATEOAS we get this URL by following links from the entrypoint.
        // This is also useful as if we're not privileged to load cardholders, the entrypoint won't have the
        // feature present, so we'll know that we're not allowed to search for cardholders before we try and start
        public string CardholderSearchHref { get; }

        // When adding mobile credentials to cardholders, we need to supply the "card type" href
        // Command Centre needs this to associate the credential with a "type" - the "type" has metadata such as
        // default enable/disable dates, card states and other things like that. Therefore we need to look it up 
        // by name and store the href for later use
        public string MobileCredentialTypeHref { get; }
    }
}
