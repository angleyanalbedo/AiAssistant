using System.Text.RegularExpressions;

namespace AiAssistant.Server.Utils
{
    public static class AnsiStripper
    {
        private static readonly Regex AnsiRegex = new Regex(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

        public static string Clean(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            return AnsiRegex.Replace(input, string.Empty);
        }
    }
}
