using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;

namespace GallagherUniversityStudentPortalSampleSite.Controllers
{
    [Route("Admin")]
    public class AdminController : Controller
    {
        readonly Database _db;
        readonly GglApiClient _gglApi;
        readonly CommandCentreConfiguration _commandCentreConfiguration;

        public AdminController(Database db, GglApiClient gglApi, CommandCentreConfiguration commandCentreConfiguration)
        {
            _db = db;
            _gglApi = gglApi;
            _commandCentreConfiguration = commandCentreConfiguration;
        }

        public async Task<IActionResult> Index([FromQuery]string? error = null, [FromQuery]string? message = null)
        {
            var students = await _db.AllAsync<Student>("SELECT id, username, studentId from students");

            return View(new AdminIndexViewModel(students.ToList(), error, message));
        }

        [HttpPost("DeleteStudent/{id}")] // should be DELETE but browsers only allow forms with GET and POST
        public async Task<IActionResult> DeleteStudent(int id)
        {
            // try delete them from command centre if we have their record linked
            var student = await _db.GetAsync<Student>("SELECT commandCentreHref from students where id = @id", new { id });
            if(student == null || student.CommandCentreHref == null)
            {
                // we could do a cardholder search using their StudentID here if we wanted to
                return RedirectToAction("Index", new { error = "This student is not linked to Command Centre" });
            }

            var result = await _gglApi.DeleteAsync(student.CommandCentreHref);
            if(result.StatusCode != HttpStatusCode.NoContent) // 204
            {
                // we failed, try read the body
                var responseBody = await result.Content.ReadAsStringAsync();
                var msg = JsonConvert.DeserializeObject<MessageObject>(responseBody)?.Message;

                return RedirectToAction("Index", new { error = msg ?? responseBody });                
            }

            // delete from our local database
            await _db.RunAsync("DELETE FROM students WHERE id = @id;", new { id });

            return Redirect(Url.Action("Index", "Admin", new { message = "Deleted Student" }) ?? "");
        }

        [HttpPost("CreateStudent")]
        public async Task<IActionResult> CreateStudent([FromForm] Student newStudent)
        {
            // Note. Our "Link" class serializes the same as new Dictionary<string, object> { ["href"] = ... }
            // so we use that as a shortcut

            // create cardholder in Command Centre with the given student ID (if the PDF enforces uniqueness this can fail)
            var result = await _gglApi.PostAsync<HttpResponseMessage>(_commandCentreConfiguration.CardholderSearchHref, new Dictionary<string, object> {
                ["firstName"] = newStudent.FirstName ?? "unspecified",
                ["lastName"] = newStudent.LastName ?? "unspecified",
                ["authorised"] = true, // if they are not authorised, they can't gain access
                // put them in the root division
                ["division"] = new Link(_commandCentreConfiguration.RootDivisionHref),
                // add them to the student access group which we looked up earlier
                ["accessGroups"] = new List<object> {
                    new Dictionary<string, object> {
                        ["accessGroup"] = new Link(_commandCentreConfiguration.StudentAccessGroupHref)
                    }
                },
                // add their studentId personal data field.
                // note: Cardholder PDF values are provided in the form:  @<pdf-name>: value
                [$"@{_commandCentreConfiguration.KeyPersonalDataFieldName}"] = newStudent.StudentId,

                // Mobile Credentials can be issued at cardholder creation by providing the same details used for the requestMobileCredential call as follows:
                //["cards"] = new List<object> {
                //    new Dictionary<string, object> {
                //        ["type"] = new Link(_commandCentreConfiguration.MobileCredentialTypeHref)
                //    }
                //}
            });

            // if this was successful then update the matching student in our students table
            if (result.StatusCode == HttpStatusCode.Created) // 201
            {
                var createdCardholderHref = result.Headers.Location; // the response from the POST gives us the HREF of the new cardholder in the Location header
                                                                     // we can store it in our local system to avoid re-looking up the cardholder again later

                // try create a new student in our system's own database
                await _db.RunAsync("INSERT INTO students SELECT null, @username, @studentId, @password, @commandCentreHref;", new
                {
                    username = newStudent.Username,
                    studentId = newStudent.StudentId,
                    password = newStudent.Password ?? "password",
                    commandCentreHref = createdCardholderHref?.ToString(),
                });

                return Redirect(Url.Action("Index", "Admin", new { message = "Created Cardholder" }) ?? "");
            }
            else
            {
                var responseMessage = await result.Content.ReadAsStringAsync();
                var wrapper = JsonConvert.DeserializeObject<MessageObject>(responseMessage);
                if(wrapper?.Message != null)
                    responseMessage = wrapper.Message;

                return Redirect(Url.Action("Index", "Admin", new { error = $"Failed to create cardholder! {responseMessage}" }) ?? "");
            }
        }
    }
}