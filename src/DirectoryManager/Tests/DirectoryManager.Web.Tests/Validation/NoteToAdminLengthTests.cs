using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>An over-long note to admin must show the submitter an error, never fail silently or at the database.</summary>
    public class NoteToAdminLengthTests
    {
        private static List<ValidationResult> Validate(object model, string member)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.Where(r => r.MemberNames.Contains(member)).ToList();
        }

        [Fact]
        public void PublicForm_NoteAtTheLimit_IsAccepted() =>
            Assert.Empty(Validate(new SubmissionRequest { NoteToAdmin = new string('a', SubmissionRequest.NoteToAdminMaxLength) }, nameof(SubmissionRequest.NoteToAdmin)));

        [Fact]
        public void PublicForm_NoteOverTheLimit_ShowsAClearError()
        {
            var errors = Validate(new SubmissionRequest { NoteToAdmin = new string('a', SubmissionRequest.NoteToAdminMaxLength + 1) }, nameof(SubmissionRequest.NoteToAdmin));
            Assert.Single(errors);
            Assert.Contains("note to the admin", errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PublicFormLimit_FitsInTheDatabaseColumn() =>
            Assert.True(SubmissionRequest.NoteToAdminMaxLength <= Submission.NoteToAdminDbMaxLength);

        [Fact]
        public void AdminSave_NoteOverTheDatabaseLimit_ShowsAClearError()
        {
            var errors = Validate(new Submission { NoteToAdmin = new string('a', Submission.NoteToAdminDbMaxLength + 1) }, nameof(Submission.NoteToAdmin));
            Assert.Single(errors);
            Assert.Contains("note to admin", errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }
}
