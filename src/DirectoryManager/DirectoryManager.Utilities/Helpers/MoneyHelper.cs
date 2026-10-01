using System.Globalization;

namespace DirectoryManager.Utilities.Helpers
{
    public static class MoneyHelper
    {
        /// <summary>
        /// Formats a whole-USD amount compactly: 30000 → "$30K", 1500000 → "$1.5M", 500 → "$500".
        /// </summary>
        public static string AbbreviateUsd(int amountUsd)
        {
            if (amountUsd < 0)
            {
                amountUsd = 0;
            }

            if (amountUsd >= 1_000_000)
            {
                return "$" + (amountUsd / 1_000_000m).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            }

            if (amountUsd >= 1_000)
            {
                return "$" + (amountUsd / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            }

            return "$" + amountUsd.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}
