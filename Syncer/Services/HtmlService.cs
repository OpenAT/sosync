using HtmlAgilityPack;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Services
{
    public class HtmlService
    {
        private static HashSet<string> HardLineBreakTags = new HashSet<string>
        {
            "p",
            "h1",
            "h2",
            "h3",
            "h4",
            "h5",
            "h6",
        };

        private static HashSet<string> SoftLineBreakTags = new HashSet<string>
        {
            "br",
        };

        public string GetPlainTextFromPartialHtml(string partialHtml)
        {
            return GetPlainText($"<html><body>{partialHtml}</body></html>");
        }

        public string GetPlainText(string html)
        {
            var sb = new StringBuilder();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            AppendNodeText(sb, doc.DocumentNode);

            return sb.ToString();
        }

        private void AppendNodeText(StringBuilder sb, HtmlNode node)
        {
            if (node.HasChildNodes)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    AppendNodeText(sb, childNode);

                    if (HardLineBreakTags.Contains(childNode.Name.ToLower()))
                    {
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                if (node is HtmlTextNode)
                {
                    sb.Append(node.InnerText);
                }
                else
                {
                    if (SoftLineBreakTags.Contains(node.Name.ToLower()))
                    {
                        sb.AppendLine();
                    }
                }
            }
        }
    }
}
