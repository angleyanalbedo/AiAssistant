using Markdig;

namespace AiAssistant.UIControls.Utils
{
    public static class MarkdownRenderHelper
    {
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public static string ConvertToHtml(string markdownText)
        {
            if (string.IsNullOrEmpty(markdownText))
            {
                return string.Empty;
            }
            return Markdown.ToHtml(markdownText, _markdownPipeline);
        }
    }
}
