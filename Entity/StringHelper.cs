namespace Entity
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.CodeAnalysis.Text;

    public static class StringHelpers
    {
        public static string InsertTextAt(this string text, string newText, int line, int column)
        {
            var lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            lines[line] = lines[line].Substring(0, column) + newText + lines[line].Substring(column);
            return string.Join(Environment.NewLine, lines);
        }

        public static string SkipLines(this string text, int linesToSkip)
        {
            var lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            return string.Join(Environment.NewLine, lines.Skip(linesToSkip));
        }

        static Regex nlRegex = new Regex(@"(\n|\r\n)", RegexOptions.Multiline);

        public static string NormalizeNewlines(this string text)
        {
            return nlRegex.Replace(text, Environment.NewLine);
        }

        public static string ReplaceToken(this string text, string token, string replacement, out LinePosition position)
        {
            var colOffset = -1;
            var lineOffset = -1;
            var reader = new StringReader(text);
            var builder = new StringBuilder();
            var lineCount = 0;
            while (true) {
                var line = reader.ReadLine();
                if (line == null) break;
                if (line.Contains(token)) {
                    lineOffset = lineCount;
                    colOffset = line.IndexOf(token);
                    builder.AppendLine(line.Replace(token, replacement));
                } else {
                    builder.AppendLine(line);
                }
                lineCount++;
            }
            position = new LinePosition(lineOffset, colOffset);
            return builder.ToString();
        }
    }
}