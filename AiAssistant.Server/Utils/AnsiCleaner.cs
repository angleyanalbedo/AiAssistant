using System.Text.RegularExpressions;

namespace AiAssistant.Server.Utils
{
    public static class AnsiCleaner
    {
        public static string Clean(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            // This regex pattern matches ANSI escape codes.
            return Regex.Replace(input, @"\x1B\[[0-?]*[ -/]*[@-~]", "");
        }
    }
}
