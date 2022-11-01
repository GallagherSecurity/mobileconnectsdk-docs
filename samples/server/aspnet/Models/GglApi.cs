using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace GglApi
{
    // the result of calling GET /api
    // NOTE: only contains fields relevant to Mobile Credential issuance
    // {
    //     "version": "1.2.3.4",
    //     "features": {
    //         "items": {
    //             "items": {
    //                 "href": "https://localhost:8904/api/items"
    //             },
    //         },
    //         "cardholders": {
    //             "cardholders": {
    //                 "href": "https://localhost:8904/api/cardholders"
    //             }
    //         },
    //         "cardTypes": {
    //             "cardTypes": {
    //                 "href": "https://localhost:8904/api/card_types"
    //             }
    //         }
    //     }
    // }
    public class ApiResponse
    {
        public string? Version { get; set; }

        public ApiFeatures? Features { get; set; }
    }

    public class ApiFeatures
    {
        public ItemsFeature? Items { get; set; }
        public CardholdersFeature? Cardholders { get; set; }
        public CardTypesFeature? CardTypes { get; set; }
    }

    public class ItemsFeature
    {
        public Link? Items { get; set; }
    }

    public class CardholdersFeature
    {
        public Link? Cardholders { get; set; }
    }

    public class CardTypesFeature
    {
        public Link? CardTypes { get; set; }
    }

    public class SearchResults<T>
    {
        [JsonConstructor]
        public SearchResults(List<T> results)
        {
            Results = results;
        }

        [JsonRequired] // searchResults must always have a collection of results
        public List<T> Results { get; }
    }

    // handles the response from the /api/items endpoint in command centre
    public class Item
    {
        public string? Id { get; set; }
        public string? Href { get; set; }
        public string? Name { get; set; }
        public ItemType? Type { get; set; }
    }

    public class ItemType
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class CardType
    {
        public string? Id { get; set; }
        public string? Href { get; set; }
        public string? Name { get; set; }
        public string? CredentialClass { get; set; }
    }

    [JsonConverter(typeof(CardholderWithPersonalDataConverter))]
    public class Cardholder
    {
        public string? Id { get; set; }
        public string? Href { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Description { get; set; }
        public bool? Authorised { get; set; }
        public Item? Division { get; set; }

        public NotificationStatus? Notifications { get; set; }

        // cards looks like this:
        //"cards": [
        //  {
        //    "href": "http://localhost:8903/api/cardholders/402/cards/73cb4015369e4b87b734aa3b9419b154",
        //    "number": "1",
        //    "issueLevel": 1,
        //    "status": {
        //      "value": "Active",
        //      "type": "active"
        //    },
        //    "type": {
        //      "name": "Mobile Credential",
        //      "href": "http://localhost:8903/api/card_types/375"
        //    },
        //    "invitation": {
        //      "status": "accepted",
        //      "email": "example@example.com"
        //    },
        //    "until": "2018-12-06T21:21:00",
        //    "credentialClass": "mobile"
        //  },
        //],
        public List<Card>? Cards { get; set; }

        // this attribute is how the CardholderWithPersonalDataConverter knows where to dump the personal data to
        [PersonalDataCollection, JsonIgnore]
        public IList<PersonalDataItem>? PersonalData { get; set; }

        public class PersonalDataItem
        {
            public string Name { get; set; } = "";
            public string? Value { get; set; }
        }

        public class NotificationStatus
        {
            public bool Enabled { get; set; } = false;
        }

        public class Card
        {
            public string? Href { get; set; }
            public string? Number { get; set; }
            public int? IssueLevel { get; set; }
            public CardStatus? Status { get; set; }
            public CardType? Type { get; set; }
            public MobileCredentialInvitation? Invitation { get; set; }
            public DateTime? From { get; set; }
            public DateTime? Until { get; set; }
            public string? CredentialClass { get; set; }
        }

        public class CardStatus
        {
            public string Value { get; set; } = "";
            public string Type { get; set; } = "";
        }

        public class MobileCredentialInvitation
        {
            // all mobile credentials always have an invitation status
            public string Status { get; set; } = "";

            // once issued or expired, the HREF is gone
            public string? Href { get; set; }

            // you can only see the email that the invitation was sent to if you are privileged to do so
            public string? Email { get; set; }
        }
    }

    // An object representing a link to a resource
    public class Link
    {
        // parameterless construtor for derived classes
        public Link(Uri uri) => Href = uri.AbsoluteUri;

        [JsonConstructor]
        public Link(string href) => Href = href;

        [JsonProperty("href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Href { get; set; } // generally be immutable; needs to be settable for JSON.NET to be able to deserialize
    }

    // The REST api can return errors wrapped in a JSON object, like this:
    public class MessageObject
    {
        public string? Message { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PersonalDataCollection : Attribute { }

    // In the Command Centre REST api, personal data field values are represented 
    // in the object itself but the field names are prefixed with an @
    // If you serialize and de-serialize the Cardholder object to a dynamic thing like a Dictionary<string, object>
    // then this is all well and good, you just have some dictionary keys that start with @
    // (Also, this is trivial in something like NodeJS, Ruby or Python where effectively everything is a dynamic
    // dictionary already)
    //
    // However, we are binding our JSON to strongly typed model objects, so we need something to 
    // scoop up all the dynamically named personal data fields (e.g. "@Student ID" or "@Drivers License Number")
    // and put them somewhere. This converter does that
    public class CardholderWithPersonalDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType.GetProperties().Any(prop => Attribute.IsDefined(prop, typeof(PersonalDataCollection)));

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => throw new NotImplementedException("CardholderWithPersonalDataConverter currently doesn't support deserializing");

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            reader.DateParseHandling = DateParseHandling.None;

            var jsonObject = JObject.Load(reader);

            if (!jsonObject.HasValues)
                return null;
            
            if (objectType is null) { return null; }

            var defaultCreator = serializer.ContractResolver.ResolveContract(objectType).DefaultCreator;
            if (defaultCreator is null) { return null; }

            // Create an object of the target type using the default contract
            var serializedObj = defaultCreator();

            // Do the default serialization
            using (var subReader = jsonObject.CreateReader())
                serializer.Populate(subReader, serializedObj);

            var pdfProperties = jsonObject.Properties().Where(e => e.Name.StartsWith("@")).ToList();

            // PDF items(@pdf) in root level add here to do validation for duplicates in the personalDataDefinitions array.
            var pdfsInRootLevel = new List<Cardholder.PersonalDataItem>();

            if (pdfProperties.Count > 0)
            {
                var pdfCollection = InitialisePersonalDataCollection(serializedObj, objectType);

                using var pdfReader = jsonObject.CreateReader();
                while (pdfReader.Read())
                {
                    // PDF properties must appear in the root
                    if (pdfReader.TokenType == JsonToken.PropertyName && pdfReader.Depth == 1)
                    {
                        var propertyName = pdfReader.Value?.ToString();
                        if (propertyName != null && propertyName.StartsWith("@"))
                        {
                            object? propertyValue = default;

                            if (pdfReader.Read())
                                propertyValue = pdfReader.Value;

                            pdfCollection.Add(new Cardholder.PersonalDataItem {
                                Name = propertyName.Substring(1),
                                Value = propertyValue?.ToString()
                            });
                        }
                    }
                }
            }

            return serializedObj;
        }

        private IList<Cardholder.PersonalDataItem> InitialisePersonalDataCollection(object targetObject, Type objectType)
        {
            // Find the property of the target model type that can be used to load PDF values. Identifier is JsonPdfCollectionDataAttribute
            var pdfCollectionProperty = objectType.GetProperties().SingleOrDefault(prop => Attribute.IsDefined(prop, typeof(PersonalDataCollection)));
            if (pdfCollectionProperty == null)
                throw new JsonException("There should be a single property with JsonPdfCollectionDataAttribute for PDF values");

            if (!typeof(IList<Cardholder.PersonalDataItem>).IsAssignableFrom(pdfCollectionProperty.PropertyType))
                throw new JsonException("Invalid member type for pdf value collection property. pdf collection property must implement IList<NewCardholderPersonalDataFields>");

            // Get the property value of the target instance
            var pdfCollection = (IList<Cardholder.PersonalDataItem>?)pdfCollectionProperty.GetValue(targetObject);
            if (pdfCollection == null)
            {
                // If the property value is not already initialised, initialise here.
                pdfCollection = new List<Cardholder.PersonalDataItem>();
                pdfCollectionProperty.SetValue(targetObject, pdfCollection);
            }

            return pdfCollection;
        }
    }
}
