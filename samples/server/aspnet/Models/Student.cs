using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace GallagherUniversityStudentPortalSampleSite.Models
{
    // Represents a student in our local database
    public class Student
    {
        // MVC model binding [FromForm] requires a parameterless constructor
        // Dapper and everything else can work with that too so we just make all the properties settable
        public Student() { }

        // note the ID is our internal identifier which is an auto-incrementing number and is not shared with other systems
        // This is distinct from the "Student ID" which could be a rich value such as "S10293"
        public long Id { get; set; } = 0;

        public string Username { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string StudentId { get; set; } = "";
        public string? Password { get; set; }
        public string? CommandCentreHref { get; set; }
    }   
}
