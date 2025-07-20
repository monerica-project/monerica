// AITagService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities;
using OpenAI;
using OpenAI.Chat;

namespace DirectoryManager.Console.Services
{
    public class AITagService : IAITagService
    {
        private readonly OpenAIClient openAi;
        private readonly IDirectoryEntryRepository entries;
        private readonly ITagRepository tags;
        private readonly IDirectoryEntryTagRepository entryTags;

        public AITagService(
            OpenAIClient openAiClient,
            IDirectoryEntryRepository entries,
            ITagRepository tags,
            IDirectoryEntryTagRepository entryTags)
        {
            this.openAi = openAiClient ?? throw new ArgumentNullException(nameof(openAiClient));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
            this.tags = tags ?? throw new ArgumentNullException(nameof(tags));
            this.entryTags = entryTags ?? throw new ArgumentNullException(nameof(entryTags));
        }

        public async Task GenerateTagsForAllEntriesAsync()
        {
            // 1) bind a ChatClient to gpt-4.1
            var chat = this.openAi.GetChatClient("gpt-4.1");

            // 2) fetch every entry (including its SubCategory→Category)
            var allEntries = (await this.entries.GetAllAsync())
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Where(e => string.Compare(e.Name, "DFX AG", StringComparison.OrdinalIgnoreCase) > 0)
                .ToList();

            foreach (var entry in allEntries)
            {
                // 3) build a prompt using Category/Subcategory/Name/Description/Link/Note
                string category = entry.SubCategory?.Category?.Name ?? "UnknownCategory";
                string subcategory = entry.SubCategory?.Name ?? "UnknownSubcategory";
                string desc = string.IsNullOrWhiteSpace(entry.Description)
                                     ? "(no description)"
                                     : entry.Description.Trim();
                string note = string.IsNullOrWhiteSpace(entry.Note)
                                     ? ""
                                     : $" Note: {entry.Note.Trim()}";
                string prompt = $@"
You are a tagging assistant. Given the following metadata, produce **up to 7** concise, comma-separated tags.

Category:    {category}
Subcategory: {subcategory}
Name:        {entry.Name}
Description: {desc}
Link:        {entry.Link}{note}

Output ONLY the comma-separated tags.";

                // 4) assemble your messages
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a helpful tagging assistant."),
                    new UserChatMessage(prompt)
                };

                // 5) call GPT
                var response = await chat
                    .CompleteChatAsync(messages)
                    .ConfigureAwait(false);

                string rawCsv = response.Value.Content[0].Text;
                var suggestions = rawCsv
              .Split(',', StringSplitOptions.RemoveEmptyEntries)
              .Select(t => t.Trim().ToLowerInvariant())
              .Where(t => t.Length > 0)
              .Distinct()
              .Take(7)
              .ToList();

                // 7) clear out old EntryTags
                var existingTags = await this.entryTags.GetTagsForEntryAsync(entry.DirectoryEntryId);
                foreach (var et in existingTags)
                {
                    await this.entryTags.RemoveTagAsync(entry.DirectoryEntryId, et.TagId);
                }

                // 8) upsert each suggested Tag + link it
                foreach (var tagName in suggestions)
                {
                    // find or create the tag
                    var tag = await this.tags.GetByKeyAsync(tagName.UrlKey())
                              ?? await this.tags.CreateAsync(tagName);

                    // link it to this entry
                    await this.entryTags.AssignTagAsync(entry.DirectoryEntryId, tag.TagId);
                }

                System.Console.WriteLine(
                    $"[{entry.DirectoryEntryId}] “{entry.Name}” → {string.Join(", ", suggestions)}");
            }
        }
    }
}
