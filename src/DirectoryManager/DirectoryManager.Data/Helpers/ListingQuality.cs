namespace DirectoryManager.Data.Helpers
{
    /// <summary>A listing's quality points and where they came from (for the admin debug view).</summary>
    public sealed class ListingQuality
    {
        public double Status { get; init; }

        public double Rating { get; init; }

        public double ReviewVolume { get; init; }

        public double Recency { get; init; }

        public double Problems { get; init; }

        public double Trust { get; init; }

        public double Completeness { get; init; }

        public double SmoothedRating { get; init; }

        public int ReviewCount { get; init; }

        public double Total => Math.Clamp(
            this.Status + this.Rating + this.ReviewVolume + this.Recency + this.Problems + this.Trust + this.Completeness,
            SearchRankingModel.MinQuality,
            SearchRankingModel.MaxQuality);

        public override string ToString() =>
            $"quality {this.Total:0.0} = status {this.Status:0} + rating {this.Rating:0.0} (bayes {this.SmoothedRating:0.00}, n={this.ReviewCount}) " +
            $"+ volume {this.ReviewVolume:0.0} + recent {this.Recency:0} + problems {this.Problems:0} + trust {this.Trust:0} + complete {this.Completeness:0.0}";
    }
}
