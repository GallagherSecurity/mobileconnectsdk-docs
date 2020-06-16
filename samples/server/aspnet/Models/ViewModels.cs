using GallagherUniversityStudentPortalSampleSite.Models;
using GglApi;
using System;
using System.Collections.Generic;

#nullable enable

namespace GallagherUniversityStudentPortalSampleSite
{
    public class BaseViewModel
    {
        public BaseViewModel(string? error = null, string? message = null)
        {
            Error = error;
            Message = message;
        }

        public BaseViewModel() { }

        public virtual string? Error { get; set; }
        public virtual string? Message { get; set; }
    }

    public class AdminIndexViewModel : BaseViewModel
    {
        public AdminIndexViewModel(IReadOnlyList<Student> students, string? error = null, string? message = null)
            : base(error, message)
        {
            Students = students;
        }

        public IReadOnlyList<Student> Students { get; }
    }

    public class StudentLoginViewModel : BaseViewModel
    {
        public StudentLoginViewModel(string studentId, string? password, string? error = null, string? message = null) : base(error, message)
        {
            StudentId = studentId;
            Password = password;
        }

        public StudentLoginViewModel() { }

        public string StudentId { get; set; } = "";
        public string? Password { get; set; }
    }

    public class StudentDetailsViewModel : BaseViewModel
    {
        public StudentDetailsViewModel(Student student, List<Cardholder.Card>? mobileCredentials, Cardholder? cardholder, string? error = null, string? message = null) : base(error, message)
        {
            Student = student;
            MobileCredentials = mobileCredentials;
            Cardholder = cardholder;
        }

        public Student Student { get; }
        public List<Cardholder.Card>? MobileCredentials { get; } // technically mobile credentials exist under cardholder.cards, but the Controller has filtered them out for us
        public Cardholder? Cardholder { get; }
    }

    public class RequestedMobileCredentialViewModel : BaseViewModel
    {
        public RequestedMobileCredentialViewModel(Student student, Cardholder.Card mobileCredential, Cardholder cardholder, string? error = null, string? message = null) : base(error, message)
        {
            Student = student;
            Credential = mobileCredential;
            Cardholder = cardholder;
        }

        public Student Student { get; }
        public Cardholder.Card Credential { get; }
        public Cardholder Cardholder { get; }
    }
}
