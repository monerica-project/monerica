// ChurnWindowResult.cs
using System;

namespace DirectoryManager.Services.Models.TransferModels
{
    /// <summary>
    /// Basic churn counts for the requested window.
    /// </summary>
    public sealed class ChurnWindowResult
    {
        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public int ActiveAtStart { get; set; }

        public int Activated { get; set; }

        public int ActiveAtEnd { get; set; }

        public int UniqueActiveInWindow { get; set; }

        /// <summary>
        /// Members of the start cohort who are NOT active at EndUtc.
        /// </summary>
        public int ChurnedFromStartCohort { get; set; }

        /// <summary>
        /// Logos whose last paid day fell inside the window.
        /// </summary>
        public int GrossChurnInWindow { get; set; }
    }
}
