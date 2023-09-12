using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Serialization;

namespace DirectoryManager.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IDirectoryEntryRepository _directoryEntryRepository;
        public string createdByUserId = "a65cd37f-9931-47ef-be49-e6d8df407010";
        public HomeController(
            ISubmissionRepository submissionRepository,
            ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            _submissionRepository = submissionRepository;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _directoryEntryRepository = directoryEntryRepository;
        }


        public async Task<IActionResult> IndexAsync()
        {
            var allEntries = await _directoryEntryRepository.GetAllAsync();
           
            return View(allEntries);
        }

        private async Task ImportAsync()
        {

            string html = await WebpageFetcher.GetHtmlContentAsync("https://monerica.com");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Create a list to store all directory entries
            List<DirectoryEntry> entries = new List<DirectoryEntry>();

            // Capture all main category nodes (the h3 tags)
            foreach (var mainCategoryNode in doc.DocumentNode.SelectNodes("//h3"))
            {
                var mainCategoryName = mainCategoryNode.InnerText.Trim();

                if (mainCategoryName == "Donate")
                {
                    continue;
                }

                // Get all sub-categories that come before the next h3 (or until the end of the document)
                var subCategories = mainCategoryNode.SelectNodes("following-sibling::p/b[@id]");
                if (subCategories != null)
                {
                    foreach (var subCategoryNode in subCategories)
                    {
                        var subCategoryName = subCategoryNode.InnerText.Trim();

                        // Get all the list items for this sub-category
                        var listItems = subCategoryNode.ParentNode.SelectNodes("following-sibling::ul[1]/li");
                        if (listItems != null)
                        {
                            foreach (var listItem in listItems)
                            {
                                DirectoryEntry entry = CreateDirectoryEntryFromListItem(listItem);
                                var category = await GetOrCreateCategory(mainCategoryName);
                                var subCategory = await GetOrCreateSubCategory(subCategoryName, category);
                                entry.SubCategory = subCategory;
                                entry.CreatedByUserId = createdByUserId;

                                if (entry.Link == null)
                                {
                                    continue;
                                }

                                if (await _directoryEntryRepository.GetByLinkAsync(entry.Link) != null)
                                {
                                    continue;
                                }
                                await _directoryEntryRepository.CreateAsync(entry);


                            }
                        }
                    }
                }
            }
        }


        private DirectoryEntry CreateDirectoryEntryFromListItem(HtmlNode li)
        {
            var entry = new DirectoryEntry();

            var a = li.SelectSingleNode("./a");
            if (a != null)
            {
                entry.Name = HttpUtility.HtmlDecode(a.InnerText.Trim());
                entry.Link = a.GetAttributeValue("href", "");

                // Decode the inner text first
                var decodedText = HttpUtility.HtmlDecode(li.InnerText.Trim('-').Trim());

                // Description is the part before the first '.'
                var descriptionEndIdx = decodedText.IndexOf('.');
                if (descriptionEndIdx != -1)
                {
                    entry.Description = decodedText.Substring(0, descriptionEndIdx + 1).Replace(entry.Name, "").Trim();

                    if (entry.Description.StartsWith("-"))
                    {
                        entry.Description = entry.Description.Remove(0, 1);
                        entry.Description = ReduceSpaces(entry.Description);
                    }
                }

                // Extract location
                var locationMatch = Regex.Match(decodedText, @"Location: ([a-zA-Z\s]+,?\s?[a-zA-Z\s]*)(\.|,)");
                if (locationMatch.Success)
                {
                    entry.Location = locationMatch.Groups[1].Value.Trim();
                }


                // Extract processor
                var processorMatch = Regex.Match(decodedText, @"Processor: (.*?)(\.|,)");
                if (processorMatch.Success)
                {
                    entry.Processor = processorMatch.Groups[1].Value.Trim();
                }

                // Extract note from <i> tag
                var noteNode = li.SelectSingleNode("./i");
                if (noteNode != null)
                {
                    entry.Note = HttpUtility.HtmlDecode(noteNode.InnerText.Trim());
                    entry.Note = ReduceSpaces(entry.Note);
                }
            }
            else
            {
                entry.Description = HttpUtility.HtmlDecode(li.InnerText.Trim('-').Trim());

                if (entry.Description.StartsWith("-"))
                {
                    entry.Description = entry.Description.Remove(0, 1);
                    entry.Description = ReduceSpaces(entry.Description);
                }
            }

            return entry;
        }

        public static string ReduceSpaces(string extractedText)
        {
            extractedText = extractedText.Replace(Environment.NewLine, " ");
            extractedText = extractedText.Replace("\n", " ");
            extractedText = extractedText.Replace("\t", " ");

            RegexOptions options = RegexOptions.None;
            Regex regex = new("[ ]{2,}", options);
            extractedText = regex.Replace(extractedText, " ");

            return extractedText;
        }

        private async Task<SubCategory> GetOrCreateSubCategory(string subCategoryName, Category category)
        {
            var existingCategory = await _subCategoryRepository.GetByNameAsync(subCategoryName);
            if (existingCategory != null)
            {
                return existingCategory;
            }

            var newCategory = new SubCategory
            {
                Name = subCategoryName,
                SubCategoryKey = TextHelpers.UrlKey(subCategoryName),
                Category = category,
                CreatedByUserId = createdByUserId
            };
            await _subCategoryRepository.CreateAsync(newCategory);

            return newCategory;
        }

        private async Task<Category> GetOrCreateCategory(string categoryName)
        {
            var existingCategory = await _categoryRepository.GetByNameAsync(categoryName);
            if (existingCategory != null)
            {
                return existingCategory;
            }

            var newCategory = new Category
            {
                Name = categoryName,
                CategoryKey = TextHelpers.UrlKey(categoryName),
                CreatedByUserId = createdByUserId
            };
            await _categoryRepository.CreateAsync(newCategory);

            return newCategory;
        }

        [Route("Submission")]
        [HttpGet]
        public IActionResult Submission()
        {
            return View(new SubmissionRequest());  // Creates a new instance just to ensure that there's a default model
        }

        [Route("Submission")]
        [HttpPost]
        public async Task<IActionResult> Submission(SubmissionRequest model)
        {
            if (ModelState.IsValid)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Convert the ContributionRequest model to Submission
                Submission submission = new()
                {
                    // Assuming the properties in Submission and ContributionRequest are the same, else adjust accordingly
                    Name = model.Name,
                    Link = model.Link,
                    Description = model.Description,
                    Location = model.Location,
                    Processor = model.Processor,
                    Note = model.Note,
                    IpAddress = ipAddress
                };

                await _submissionRepository.AddAsync(submission);

                // Redirect to some "Success" page or any other.
                return RedirectToAction("Success");
            }

            // If the model is not valid, just re-display the form with the validation errors
            return View(model);
        }


        [Route("Submission/success")]
        public IActionResult Success()
        {
            return View();  // Assuming you have a Success.cshtml view
        }
    }
}


public class WebpageFetcher
{
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<string> GetHtmlContentAsync(string url)
    {
        try
        {
            return await httpClient.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return null;
        }
    }
}