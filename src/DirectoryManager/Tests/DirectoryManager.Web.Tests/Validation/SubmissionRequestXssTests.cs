using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// "Submitted" guarantee, at the model the submit/edit POST actually binds.
    /// A SubmissionRequest carrying an XSS payload in ANY user-editable field must
    /// be flagged by the same scan the controller runs, so it never reaches storage.
    ///
    /// NOTE: the controller must scan with <see cref="ScriptValidation.ContainsSuspiciousMarkup(object?)"/>.
    /// The narrow ContainsScriptTag passes handler/URI vectors (see ScriptValidationXssTests),
    /// so these assertions are the executable spec for the controller gate.
    /// </summary>
    public class SubmissionRequestXssTests
    {
        private const string ScriptPayload = "<script>alert(1)</script>";
        private const string HandlerPayload = "<img src=x onerror=alert(1)>";
        private const string JsUriPayload = "<a href=\"javascript:alert(1)\">x</a>";

        private static SubmissionRequest CleanSubmission() => new ()
        {
            Link = "https://example.com",
            Name = "Example Service",
            Description = "Accepts Monero for hosting.",
            Note = "No KYC.",
            Location = "Online",
            Processor = "BTCPay",
            Tags = "privacy, hosting",
        };

        [Fact]
        public void CleanSubmission_IsNotFlagged()
        {
            Assert.False(ScriptValidation.ContainsSuspiciousMarkup(CleanSubmission()));
        }

        // Every field a visitor can edit, exercised with three different vectors.
        // Keys are plain strings so xUnit can pre-enumerate each row cleanly.
        public static IEnumerable<object[]> FieldVectorMatrix()
        {
            string[] fields =
            {
                "Name", "Description", "Note", "NoteToAdmin",
                "Location", "SuggestedSubCategory", "Tags",
            };
            string[] payloads = { ScriptPayload, HandlerPayload, JsUriPayload };

            foreach (var field in fields)
            {
                foreach (var payload in payloads)
                {
                    yield return new object[] { field, payload };
                }
            }
        }

        [Theory]
        [MemberData(nameof(FieldVectorMatrix))]
        public void Submission_WithPayloadInAnyField_IsFlagged(string field, string payload)
        {
            var model = CleanSubmission();
            InjectPayload(model, field, payload);

            Assert.True(
                ScriptValidation.ContainsSuspiciousMarkup(model),
                $"Payload survived the submission scan in field '{field}': {payload}");
        }

        private static void InjectPayload(SubmissionRequest model, string field, string payload)
        {
            switch (field)
            {
                case "Name": model.Name = payload; break;
                case "Description": model.Description = payload; break;
                case "Note": model.Note = payload; break;
                case "NoteToAdmin": model.NoteToAdmin = payload; break;
                case "Location": model.Location = payload; break;
                case "SuggestedSubCategory": model.SuggestedSubCategory = payload; break;
                case "Tags": model.Tags = payload; break;
                default: throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field.");
            }
        }
    }
}
