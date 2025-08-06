using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Validation
{
    public static class PgpKeyValidator
    {
        // Matches the entire ASCII-armored PGP public key block
        private static readonly Regex ArmorRegex = new Regex(
            @"-----BEGIN PGP PUBLIC KEY BLOCK-----.*?-----END PGP PUBLIC KEY BLOCK-----",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Returns true if <paramref name="armoredKey"/> is a valid ASCII-armored PGP public key,
        /// including a matching CRC24 checksum.
        /// </summary>
        public static bool IsValid(string armoredKey)
        {
            if (string.IsNullOrWhiteSpace(armoredKey))
            {
                return false;
            }

            var match = ArmorRegex.Match(armoredKey);
            if (!match.Success)
            {
                return false;
            }

            // Extract inner block (between headers)
            string content = match.Value;
            int headerEnd = content.IndexOf("-----BEGIN PGP PUBLIC KEY BLOCK-----")
                            + "-----BEGIN PGP PUBLIC KEY BLOCK-----".Length;
            int footerStart = content.LastIndexOf("-----END PGP PUBLIC KEY BLOCK-----");
            string inner = content.Substring(headerEnd, footerStart - headerEnd);

            var payloadLines = new List<string>();
            string? crcBase64 = null;

            // Process each line: drop headers, blank lines, capture CRC lines
            foreach (var rawLine in inner.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("Version:") ||
                    line.StartsWith("Comment:"))
                {
                    continue;
                }

                if (line.StartsWith("="))
                {
                    // separate CRC line
                    crcBase64 = line.Substring(1);
                    continue;
                }

                // detect merged payload+CRC: base64 payload may end with "===CRC"
                int mergeIdx = line.IndexOf("===");
                if (mergeIdx >= 0)
                {
                    // payload part before '===', then two '=' padding
                    payloadLines.Add(line.Substring(0, mergeIdx) + "==");
                    crcBase64 = line.Substring(mergeIdx + 3);
                }
                else
                {
                    payloadLines.Add(line);
                }
            }

            // Combine payload and decode
            var payloadBase64 = string.Concat(payloadLines);
            byte[] data;
            try
            {
                data = Convert.FromBase64String(payloadBase64);
            }
            catch
            {
                return false;
            }

            // If no CRC present, accept as valid
            if (string.IsNullOrEmpty(crcBase64))
            {
                return true;
            }

            // Decode CRC
            byte[] crcBytes;
            try
            {
                // pad CRC string to multiple of 4
                int mod = crcBase64.Length % 4;
                if (mod != 0)
                {
                    crcBase64 = crcBase64.PadRight(crcBase64.Length + (4 - mod), '=');
                }

                crcBytes = Convert.FromBase64String(crcBase64);
            }
            catch
            {
                return false;
            }

            if (crcBytes.Length != 3)
            {
                return false;
            }

            // Compute CRC24 of the decoded payload
            uint computed = ComputeCrc24(data);

            // Compare big-endian bytes
            if (crcBytes[0] != (byte)((computed >> 16) & 0xFF) ||
                crcBytes[1] != (byte)((computed >> 8) & 0xFF) ||
                crcBytes[2] != (byte)(computed & 0xFF))
            {
                return false;
            }

            return true;
        }

        // Standard PGP CRC-24 (poly 0x1864CFB, init 0xB704CE)
        private static uint ComputeCrc24(byte[] data)
        {
            const uint poly = 0x1864CFB;
            uint crc = 0xB704CE;

            foreach (var b in data)
            {
                crc ^= (uint)b << 16;
                for (int i = 0; i < 8; i++)
                {
                    crc <<= 1;
                    if ((crc & 0x1000000) != 0)
                    {
                        crc ^= poly;
                    }
                }
            }

            return crc & 0xFFFFFF;
        }
    }
}