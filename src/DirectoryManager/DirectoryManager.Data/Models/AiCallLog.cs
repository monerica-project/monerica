using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// One call to an AI model (e.g. the submission reviewer's Claude call), kept permanently so the
    /// owner can see exactly what was sent, what came back and what the job did with it.
    /// Never holds the API key or any visitor IP.
    /// </summary>
    [Index(nameof(CreatedUtc))]
    [Index(nameof(SubmissionId))]
    public class AiCallLog
    {
        public const string FeatureSubmissionReview = "submission-review";

        public int Id { get; set; }

        public DateTime CreatedUtc { get; set; }

        /// <summary>Which job/feature made the call, e.g. "submission-review".</summary>
        [MaxLength(50)]
        public string Feature { get; set; } = string.Empty;

        public int? SubmissionId { get; set; }

        /// <summary>Name of the submission at the time of the call (kept even if the submission is deleted).</summary>
        [MaxLength(255)]
        public string? SubjectName { get; set; }

        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        /// <summary>HTTP attempts made (retries on 429/5xx/network errors count).</summary>
        public int Attempts { get; set; }

        /// <summary>Last HTTP status from the API (null when the request never got a response).</summary>
        public int? HttpStatus { get; set; }

        public int DurationMs { get; set; }

        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        [MaxLength(50)]
        public string? StopReason { get; set; }

        /// <summary>Verdict the model returned (e.g. approve_candidate, flag, reject_candidate).</summary>
        [MaxLength(50)]
        public string? Verdict { get; set; }

        public double? Confidence { get; set; }

        /// <summary>The model's own summary of its decision.</summary>
        public string? Summary { get; set; }

        /// <summary>What the job did with the result, e.g. "Submission → ReadyToApprove" or "Flagged (API error)".</summary>
        [MaxLength(200)]
        public string? Outcome { get; set; }

        public string? Error { get; set; }

        /// <summary>Size of the system instructions sent (the full text is fixed in code, so only its size is kept).</summary>
        public int SystemPromptChars { get; set; }

        /// <summary>The user message sent to the model (for the reviewer: the submission dossier).</summary>
        public string? RequestText { get; set; }

        /// <summary>The model's answer (for the reviewer: the verdict JSON).</summary>
        public string? ResponseText { get; set; }
    }
}
