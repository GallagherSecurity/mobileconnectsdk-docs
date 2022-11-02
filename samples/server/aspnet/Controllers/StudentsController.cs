using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;

namespace GallagherUniversityStudentPortalSampleSite.Controllers
{
    [Route("Students")]
    public class StudentsController : Controller
    {
        readonly Database _db;
        readonly GglApiClient _gglApi;
        readonly CommandCentreConfiguration _commandCentreConfiguration;

        public StudentsController(Database db, GglApiClient gglApi, CommandCentreConfiguration commandCentreConfiguration)
        {
            _db = db;
            _gglApi = gglApi;
            _commandCentreConfiguration = commandCentreConfiguration;
        }

        [HttpGet("")]
        public IActionResult Index() => View();

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromForm] StudentLoginViewModel studentLogin)
        {
            var student = await _db.GetAsync<Student>("SELECT id, username from students where studentId = @StudentId and password = @Password", studentLogin);
            if (student == null)
            {
                return View("Index", new StudentLoginViewModel(
                    studentLogin.StudentId, null, error: "Cannot find specified StudentId/Password combination"));
            }

            return RedirectToAction("Main", new { id = student.Id });
        }

        // this is completely insecure obviously. A real site would have cookies, etc and not just allow anyone to put /5 in the URL and see anyone's details
        [HttpGet("{id}")]
        public async Task<IActionResult> Main(int id, string? error = null)
        {
            var student = await _db.GetAsync<Student>("SELECT id, username, studentId from students where id = @id", new { id });
            if (student == null)
            {
                return RedirectToAction("Index");
            }

            // Search for the cardholder using the student ID personal data field.
            // Note: when searching for things in the command centre REST api, quote the value for exact match
            var url = $"{_commandCentreConfiguration.CardholderSearchHref}?pdf_{_commandCentreConfiguration.KeyPersonalDataFieldId}=\"{student.StudentId}\"";
            var cardholderSearchResponse = await _gglApi.GetAsync<SearchResults<Cardholder>>(url);

            // Important design consideration:
            // There will be a representation of the person (cardholder) in both your external system, and also in command centre.
            // You'll need to somehow align these two records.
            //
            // The conventional (pre-rest api) way this is done is to attach a personal data field to the cardholder in command centre, and give it a 
            // value that exists in your external system, such as a "Student ID" or "Employee Number" or some other thing like that.
            // Then you can use this value to look up the cardholder record.
            // Note however that this relies on something putting that external ID into command centre somehow (for example, using one
            // of command centre's other cardholder import mechanisms)
            //
            // An alternative way is that if you use the REST api to *create* the cardholder in the first place, then as part of that
            // creation, you can store its href or numeric ID in your external system. 
            // If you store it's href, then you can simply go and load/edit the cardholder record without first having 
            // to do a search using some other parameter which is probably more convenient.
            // Note that this relies on you creating (for example) a column in your own database to store the Command Centre Cardholder Href
            //
            // Because this is a sample app, we do both (why not?!)
            // How this works, is when you load the 'main' page, we look up the cardholder href in command centre
            // and if we find it, we store the href in our own sqlite database.
            // That way, when we want to request a mobile credential, we can skip the search-by-pdf step
            var credentials = new List<Cardholder.Card>();
            Cardholder? cardholderDetails = null;

            var cardholderSummary = cardholderSearchResponse.Results.FirstOrDefault();
            if (cardholderSummary != null && cardholderSummary.Href != null)
            {
                var cardholderHref = cardholderSummary.Href;
                await _db.RunAsync("UPDATE students SET commandCentreHref = @cardholderHref where id = @Id", new { cardholderHref, student.Id });

                // do a GET on the cardholder's HREF to pull back all the details
                cardholderDetails = await _gglApi.GetAsync<Cardholder>(cardholderHref);
                if (cardholderDetails?.Cards is List<Cardholder.Card> cards)
                {
                    // only mobile credentials have 'invitation'
                    credentials = cards.Where(card => card.Invitation != null).ToList();
                }
            }
            else
            {
                error = "Cannot find your cardholder record in command centre"; // stay on the page, just show an error
            }

            return View(new StudentDetailsViewModel(student, credentials, cardholderDetails, error: error));
        }

        public class CredentialDeleteRequest
        {
            public string UserId { get; set; } = "";
            public string CredentialHref { get; set; } = "";
        }

        [HttpPost("DeleteMobileCredential")]
        public async Task<IActionResult> DeleteMobileCredential([FromForm] CredentialDeleteRequest request)
        {
            var deleteResponse = await _gglApi.DeleteAsync(request.CredentialHref);
            if (deleteResponse.StatusCode == HttpStatusCode.NoContent) // 204
            { // success! There is no response body.
                return RedirectToAction("Main", new { id = request.UserId, message = "Deleted Credential" });
            }
            // else we failed, try read the body
            var responseBody = await deleteResponse.Content.ReadAsStringAsync();
            var msg = JsonConvert.DeserializeObject<MessageObject>(responseBody)?.Message;

            return RedirectToAction("Main", new { id = request.UserId, error = msg ?? responseBody });
        }

        public class CredentialCreateRequest
        {
            public string UserId { get; set; } = "";
            public string? Email { get; set; }
            public string? Sms { get; set; }
            public string SingleFactorOnly { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> RequestMobileCredential([FromForm] CredentialCreateRequest request)
        {
            var student = await _db.GetAsync<Student>("SELECT id, username, studentId, commandCentreHref from students where id = @UserId", request);
            if (student == null || student.CommandCentreHref == null)
            {
                // If we hadn't stored the student's commandCentreHref, we could look it up by doing a cardholder search for the student's StudentId personal data field
                return RedirectToAction("Main", new { id = request.UserId, error = "No command centre href is saved for that students!" });
            }

            // if we're registering with an email/sms or register second factor flag, pass those along
            var invitation = new Dictionary<string, object>();
            if (request.Email is string email)
                invitation["email"] = email;

            if (request.Sms is string sms)
                invitation["mobile"] = sms;

            if (request.SingleFactorOnly == "on")
                invitation["singleFactorOnly"] = true;

            var cardToAdd = new Dictionary<string, object> {
                ["type"] = new Link(_commandCentreConfiguration.MobileCredentialTypeHref)
            };

            if (invitation.Count > 0)
                cardToAdd["invitation"] = invitation;

            var patchCardholderResponse = await _gglApi.PatchAsync<HttpResponseMessage>(student.CommandCentreHref, new Dictionary<string, object> {
                ["cards"] = new Dictionary<string, object> {
                    ["add"] = new[] {
                        cardToAdd // add a single card of the configured mobile credential card type
                    }
                }
            });

            if (patchCardholderResponse.StatusCode == HttpStatusCode.NoContent)
            {
                // success. 204; There is no response body.
                // If you want to react immediately to the creation of the mobile credential,
                // you must load the cardholder details and look for a mobile credential with invitation.status of 'sent' and a non-null invitation.href 
                // We need a polling loop as the invitation.href won't be there immediately, it arrives after the Command Centre server has sent a message, and received
                // a response from the Gallagher Cloud. This is typically quite fast, however it is dependent on the latency of the internet connection between command centre
                // and the Gallagher Cloud, so won't be instant.
                // We do that here to illustrate how you might create such a polling loop.
                //
                // HOWEVER, It is highly likely you don't actually need to do this at all. Simply create a credential and forget about the response (other than checking for HTTP errors and such).
                // The next time your mobile app connects to your service (e.g. on logon or resume), AT THAT POINT you can query the Command centre REST api for any 'sent' credentials and
                // obtain the invitation href.
                // This is also better because if the credential gets delayed (e.g. command centre can't talk to the cloud) then it'll be fine.
                // - at some point in future the credential href will come back from the cloud, and your mobile app can pick it up then.
                // If you do need quick feedback, you could also implement the polling loop in the mobile app rather than in the server which is probably nicer.
                //
                // Minor Note: Astute readers might be thinking "What if I add two credentials at the same time? How will I know which one is which?"
                // The answer is threefold:
                //
                // - This is not recommended. It's not a particularly useful thing to do.
                //
                // - Provided you remember to filter for credentials of the same card type you're creating with (as we do below), then it actually doesn't
                //   matter which credential is which, as they should both be functionally identical and it's unlikely you could tell if you got the right one or not
                //
                // - If you legitimately want to do this, you can pass a "number" when you add the credential, which is arbitrary text of your chosing.
                //   You can then use that "number" to filter for credentials and ensure you find the right one.
                //   Note: The intent of the "number" field is to put a user-friendly name for the device, such as "Samsung Galaxy S6" so people with two phones
                //   can tell which credential goes where, but you can put anything in it, such as a UUID or unique integer.

                for (int i = 0; i < 10; i++)
                {
                    var cardholderResponse = await _gglApi.GetAsync<Cardholder>(student.CommandCentreHref);

                    if (cardholderResponse.Cards != null)
                    {
                        var pendingCredentials = cardholderResponse.Cards.Where(card =>
                          Equals(card.Type?.Href, _commandCentreConfiguration.MobileCredentialTypeHref) &&
                          Equals(card.Invitation?.Status, "sent") &&
                          !string.IsNullOrEmpty(card.Invitation?.Href));

                        if (pendingCredentials.FirstOrDefault() is Cardholder.Card newCredential)
                        {
                            // we found our newly-added credential
                            return View("RequestMobileCredential", new RequestedMobileCredentialViewModel(student, newCredential, cardholderResponse));
                        }
                    }
                    // you could possibly be more agressive here; a sleep 100 or sleep 200 might be fine depending on how good your internet is.
                    await Task.Delay(millisecondsDelay: 500);
                }

                // if we arrive here it means we've tried 10 times and still have no result; We have to give up at some point
                return RedirectToAction("Main", new { id = student.Id, error = "Timed out trying to create a mobile credential!Command Centre may not be able to communicate with the Gallagher Cloud!" });
            }

            // else we failed, try read the body
            var responseBody = await patchCardholderResponse.Content.ReadAsStringAsync();
            var msg = JsonConvert.DeserializeObject<MessageObject>(responseBody)?.Message;

            return RedirectToAction("Main", new { id = request.UserId, error = msg ?? responseBody });
        }
    }
}