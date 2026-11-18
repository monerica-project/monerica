using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class SubmissionFormLayoutTests
    {
        [Fact]
        public void RequiredSection_IsTheModelsRequiredFields_PlusTheSubcategory()
        {
            var modelRequired = typeof(SubmissionRequest)
                .GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() != null)
                .Select(p => p.Name)
                .Append(nameof(SubmissionRequest.SubCategoryId)) // enforced at the confirm step
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(modelRequired, SubmissionFormLayout.RequiredFields.OrderBy(n => n).ToArray());
        }

        [Fact]
        public void EveryFormField_IsInExactlyOnePlace()
        {
            var placed = SubmissionFormLayout.RequiredFields
                .Concat(SubmissionFormLayout.OptionalGroups.SelectMany(g => g.Fields))
                .Concat(SubmissionFormLayout.NonFormProperties)
                .ToList();

            Assert.Equal(placed.Count, placed.Distinct().Count());

            var all = typeof(SubmissionRequest)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(all, placed.OrderBy(n => n).ToArray());
        }

        [Fact]
        public void EmptyNewSubmission_HasEveryOptionalGroupClosed()
        {
            var model = new SubmissionRequest();
            foreach (var g in SubmissionFormLayout.OptionalGroups)
            {
                Assert.False(SubmissionFormLayout.IsOpen(g, model, new ModelStateDictionary()), g.Id);
            }
        }

        public static IEnumerable<object[]> FilledGroups()
        {
            yield return new object[] { "more-links", new SubmissionRequest { Link2 = "http://exampleonionaddress.onion" } };
            yield return new object[] { "more-links", new SubmissionRequest { RelatedLink3 = "https://forum.example.org/t/1" } };
            yield return new object[] { "contact", new SubmissionRequest { PgpKey = "-----BEGIN PGP PUBLIC KEY BLOCK-----" } };
            yield return new object[] { "location", new SubmissionRequest { CountryCode = "US" } };
            yield return new object[] { "location", new SubmissionRequest { PostalCode = "33139" } };
            yield return new object[] { "business", new SubmissionRequest { FoundedYear = "2019" } };
            yield return new object[] { "business", new SubmissionRequest { Note = "Use code XMR10" } };
            yield return new object[] { "kyc-javascript", new SubmissionRequest { KycPolicy = KycPolicy.MandatoryKyc } };
            yield return new object[] { "kyc-javascript", new SubmissionRequest { SiteJavaScript = SiteJavaScript.None } };
            yield return new object[] { "restrictions", new SubmissionRequest { TorPolicy = TorPolicy.Allowed } };
            yield return new object[] { "exchanges", new SubmissionRequest { Liquidity = Liquidity.Own } };
            yield return new object[] { "exchanges", new SubmissionRequest { RateType = RateType.Float } };
            yield return new object[] { "exchanges", new SubmissionRequest { Guarantees = new List<GuaranteeItem> { new GuaranteeItem(), new GuaranteeItem { Amount = 5 } } } };
            yield return new object[] { "categories-tags", new SubmissionRequest { SelectedTagIds = new List<int> { 3 } } };
            yield return new object[] { "categories-tags", new SubmissionRequest { SuggestedSubCategory = "Web Hosting > Domains" } };
            yield return new object[] { "note-to-reviewer", new SubmissionRequest { NoteToAdmin = "I run the site" } };
        }

        [Theory]
        [MemberData(nameof(FilledGroups))]
        public void GroupWithAValue_RendersOpen_AndOnlyThatGroup(string groupId, SubmissionRequest model)
        {
            foreach (var g in SubmissionFormLayout.OptionalGroups)
            {
                Assert.Equal(g.Id == groupId, SubmissionFormLayout.IsOpen(g, model, new ModelStateDictionary()));
            }
        }

        [Theory]
        [InlineData("Link3", "more-links")]
        [InlineData("Messenger", "contact")]
        [InlineData("StreetAddress", "location")]
        [InlineData("FoundedYear", "business")]
        [InlineData("RestrictedCountries", "restrictions")]
        [InlineData("Guarantees[2].Amount", "exchanges")]
        [InlineData("Tags", "categories-tags")]
        [InlineData("NoteToAdmin", "note-to-reviewer")]
        public void GroupWithAnError_RendersOpen_EvenWhenEmpty(string errorKey, string groupId)
        {
            var ms = new ModelStateDictionary();
            ms.AddModelError(errorKey, "bad");

            foreach (var g in SubmissionFormLayout.OptionalGroups)
            {
                Assert.Equal(g.Id == groupId, SubmissionFormLayout.IsOpen(g, new SubmissionRequest(), ms));
            }
        }

        [Fact]
        public void DefaultsAndNotApplicableValues_DoNotOpenAGroup()
        {
            var model = new SubmissionRequest
            {
                KycPolicy = KycPolicy.NotStated,
                Liquidity = Liquidity.Unknown,
                RateType = RateType.Unknown,
                Processor = string.Empty,
                CountryCode = " ",
                Guarantees = new List<GuaranteeItem> { new GuaranteeItem { Link = " ", Amount = 0 } },
            };

            Assert.False(SubmissionFormLayout.IsOpen(SubmissionFormLayout.KycAndJavaScript, model, null));
            Assert.False(SubmissionFormLayout.IsOpen(SubmissionFormLayout.Exchanges, model, null));
            Assert.False(SubmissionFormLayout.IsOpen(SubmissionFormLayout.Business, model, null));
            Assert.False(SubmissionFormLayout.IsOpen(SubmissionFormLayout.Location, model, null));
        }

        [Fact]
        public void SummaryOnlyErrors_DoNotOpenEveryGroup()
        {
            var ms = new ModelStateDictionary();
            ms.AddModelError(string.Empty, "Related link 1 is not a valid URL.");
            ms.AddModelError("Name", "required");

            Assert.All(SubmissionFormLayout.OptionalGroups, g => Assert.False(SubmissionFormLayout.IsOpen(g, new SubmissionRequest(), ms)));
        }

        [Fact]
        public void EveryOfferedOption_HasAnExplanation()
        {
            foreach (var v in Enum.GetValues<KycPolicy>())
            {
                Assert.False(string.IsNullOrWhiteSpace(SubmissionFormLayout.OptionHelp(v)), v.ToString());
            }

            foreach (var v in SiteJavaScriptExtensions.FormOrder)
            {
                Assert.False(string.IsNullOrWhiteSpace(SubmissionFormLayout.OptionHelp(v)), v.ToString());
            }

            foreach (var v in LiquidityExtensions.Selectable)
            {
                Assert.False(string.IsNullOrWhiteSpace(SubmissionFormLayout.OptionHelp(v)), v.ToString());
            }

            foreach (var v in RateTypeExtensions.PublicFormOrder)
            {
                Assert.False(string.IsNullOrWhiteSpace(SubmissionFormLayout.OptionHelp(v)), v.ToString());
            }

            foreach (var v in Enum.GetValues<VpnPolicy>())
            {
                Assert.False(string.IsNullOrWhiteSpace(SubmissionFormLayout.OptionHelp(v)), v.ToString());
            }

            foreach (var v in Enum.GetValues<TorPolicy>())
            {
                Assert.False(string.IsNullOrWhiteSpace(SubmissionFormLayout.OptionHelp(v)), v.ToString());
            }
        }

        [Fact]
        public void KycAndLiquidityHelp_UsesTheSameWordingAsTheFilterTooltips()
        {
            Assert.Equal(FilterOptionTooltips.For(KycPolicy.ShotgunKyc), SubmissionFormLayout.OptionHelp(KycPolicy.ShotgunKyc));
            Assert.Equal(FilterOptionTooltips.For(Liquidity.ThirdParty), SubmissionFormLayout.OptionHelp(Liquidity.ThirdParty));
            Assert.Equal(SiteJavaScript.Required.Explanation(), SubmissionFormLayout.OptionHelp(SiteJavaScript.Required));
        }

        [Fact]
        public void FormView_HasUniqueStaticIds_AndEveryHelpIdItPointsTo()
        {
            var view = ReadView("_SubmissionForm.cshtml");

            // Static ids written in the markup (Razor-built ones like Guarantees_@gi are unique by index).
            var ids = Regex.Matches(view, "\\sid=\"([A-Za-z][A-Za-z0-9_-]*)\"")
                .Select(m => m.Groups[1].Value)
                .ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count() + CountDuplicatedInBranches(view, ids));

            // Every aria-describedby target exists.
            var described = Regex.Matches(view, "aria-describedby=\"([^\"@]+)\"")
                .SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Distinct();
            foreach (var target in described)
            {
                Assert.Contains(target, ids);
            }

            Assert.DoesNotContain("<form", view);
        }

        [Fact]
        public void FormView_MarksEveryRequiredFieldInText()
        {
            var view = ReadView("_SubmissionForm.cshtml");
            foreach (var field in SubmissionFormLayout.RequiredFields)
            {
                Assert.Matches(new Regex($"<label asp-for=\"{field}\">[^<]*\\(required\\)</label>"), view);
            }
        }

        // The PGP key hint is written once per branch (readonly edit / editable), so its id appears twice
        // in the source but only once in any rendered page.
        private static int CountDuplicatedInBranches(string view, List<string> ids)
            => ids.Count(i => i == "PgpKeyHint") - 1;

        private static string ReadView(string name, [CallerFilePath] string thisFile = "")
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "DirectoryManager.Web")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir!.FullName, "DirectoryManager.Web", "Views", "Shared", name));
        }
    }
}
