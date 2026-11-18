using System.Security.Cryptography;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Suggestions for the sign-up form: a random friendly username (e.g. <c>quiet_otter_4821</c>)
    /// and a strong random password (e.g. <c>k7wq-3mfz-p9xd-t4hn-c2vr</c>, about 100 bits). Both are
    /// only written into the form page; nothing is stored on the server until the visitor submits
    /// the form and solves the captcha.
    /// </summary>
    public static class MemberCredentialGenerator
    {
        // No 0/o, 1/l/i: easy to copy by hand.
        private const string PasswordAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";
        private const int PasswordGroups = 5;
        private const int PasswordGroupLength = 4;

        private static readonly string[] Adjectives =
        {
            "quiet", "brave", "calm", "swift", "lucky", "sunny", "clever", "gentle", "happy", "bold",
            "bright", "merry", "noble", "proud", "witty", "keen", "kind", "jolly", "mellow", "rapid",
            "silver", "golden", "amber", "misty", "frosty", "cosmic", "humble", "steady", "nimble", "cozy",
        };

        private static readonly string[] Nouns =
        {
            "heron", "otter", "falcon", "maple", "river", "comet", "badger", "willow", "lynx", "panda",
            "robin", "cedar", "harbor", "meadow", "fox", "owl", "bison", "koala", "puffin", "walrus",
            "pine", "breeze", "pebble", "summit", "canyon", "lagoon", "tiger", "raven", "beacon", "acorn",
        };

        /// <summary>A random username that passes <see cref="MemberAccountRules.ValidateUsername"/>.</summary>
        public static string Username()
        {
            while (true)
            {
                var name = $"{Pick(Adjectives)}_{Pick(Nouns)}_{RandomNumberGenerator.GetInt32(1000, 10000)}";
                if (MemberAccountRules.ValidateUsername(name) is null)
                {
                    return name;
                }
            }
        }

        /// <summary>A random password that passes <see cref="MemberAccountRules.ValidatePassword"/>.</summary>
        public static string Password()
        {
            while (true)
            {
                var groups = new string[PasswordGroups];
                for (var g = 0; g < PasswordGroups; g++)
                {
                    var chars = new char[PasswordGroupLength];
                    for (var i = 0; i < PasswordGroupLength; i++)
                    {
                        chars[i] = PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)];
                    }

                    groups[g] = new string(chars);
                }

                var password = string.Join('-', groups);
                if (MemberAccountRules.ValidatePassword(password) is null)
                {
                    return password;
                }
            }
        }

        private static string Pick(string[] words) => words[RandomNumberGenerator.GetInt32(words.Length)];
    }
}
