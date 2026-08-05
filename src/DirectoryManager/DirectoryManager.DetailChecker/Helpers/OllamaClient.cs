using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirectoryManager.DetailChecker.Helpers
{
    /// <summary>
    /// Thin client for a locally-hosted model via Ollama's HTTP API (default
    /// http://127.0.0.1:11434). Runs a small model (e.g. llama3.2:1b) — free, no API,
    /// no data leaves the box. We only ask it the two *semantic* questions (country + KYC)
    /// on a short pre-extracted snippet, and force JSON output at temperature 0 so results
    /// are as deterministic as a tiny model allows. It is deliberately conservative: when
    /// the text is ambiguous, multi-jurisdiction, or silent, it must return "unclear" — the
    /// caller then flags for a human instead of changing anything.
    /// </summary>
    public sealed class OllamaClient
    {
        private const string SystemPrompt =
            "You reconfirm two facts about a crypto/finance website for a directory, by READING and " +
            "UNDERSTANDING the text below — not by matching keywords. Sites almost never use the term " +
            "\"KYC\" and describe things inconsistently, so infer from HOW they describe signing up, " +
            "creating an account, identity/age verification, deposit limits, or restrictions. Judge ONLY " +
            "from this text; if it doesn't address a fact, or is ambiguous/contradictory, say so rather " +
            "than guessing.\n\n" +
            "Return ONLY a JSON object with exactly these keys:\n" +
            "  country_code: ISO 3166-1 alpha-2 code of the ONE country the service is registered in or " +
            "operated from (e.g. \"US\",\"DE\"). Use \"\" if the text doesn't say or implies several.\n" +
            "  country_multiple: true if it implies multiple/unclear jurisdictions or names none.\n" +
            "  kyc: infer the identity-verification stance, one of:\n" +
            "    \"guaranteed_no\" – clearly usable with no identity verification at all (e.g. no account, " +
            "just an email, explicitly anonymous/non-custodial with no ID);\n" +
            "    \"rare\" – normally none, but may ask in edge cases;\n" +
            "    \"shotgun\" – may demand ID unpredictably/after the fact;\n" +
            "    \"mandatory\" – ID/passport/selfie/address always required to use it;\n" +
            "    \"varies\" – depends on the provider, amount, or region;\n" +
            "    \"not_stated\" – the text simply never addresses identity verification;\n" +
            "    \"unclear\" – it addresses it but contradictorily/ambiguously.\n" +
            "  confidence: 0.0-1.0 — your honest certainty given only this text (be low if unsure).\n" +
            "  evidence: a short direct quote (<160 chars) from the text you based it on, or \"\".";

        private readonly HttpClient http;
        private readonly string endpoint;
        private readonly string model;

        public OllamaClient(string endpoint, string model, TimeSpan? timeout = null)
        {
            this.endpoint = endpoint.TrimEnd('/');
            this.model = model;
            this.http = new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(3) };
        }

        /// <summary>True when the Ollama server answers (so we can fall back to rules-only if not).</summary>
        public async Task<bool> IsReachableAsync()
        {
            try
            {
                using var resp = await this.http.GetAsync($"{this.endpoint}/api/tags");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Classify country + KYC by reading the site's relevant text. Null on failure.</summary>
        public async Task<LlmResult?> ClassifyAsync(string? text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null; // nothing to classify
            }

            var body = new
            {
                model = this.model,
                prompt = SystemPrompt + "\n\n=== WEBSITE TEXT ===\n" + text + "\n=== END ===",
                stream = false,
                format = "json",
                options = new { temperature = 0.0, num_ctx = 4096 },
            };

            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                using var resp = await this.http.PostAsync($"{this.endpoint}/api/generate", content, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return null;
                }

                var payload = await resp.Content.ReadAsStringAsync(ct);
                var outer = JsonSerializer.Deserialize<OllamaGenerateResponse>(payload);
                if (string.IsNullOrWhiteSpace(outer?.Response))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<LlmResult>(outer.Response);
            }
            catch
            {
                return null;
            }
        }

        private sealed class OllamaGenerateResponse
        {
            [JsonPropertyName("response")]
            public string? Response { get; set; }
        }
    }

    /// <summary>Raw JSON the model returns (validated/normalised by the caller).</summary>
    public sealed class LlmResult
    {
        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("country_multiple")]
        public bool CountryMultiple { get; set; }

        [JsonPropertyName("kyc")]
        public string? Kyc { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("evidence")]
        public string? Evidence { get; set; }
    }
}
