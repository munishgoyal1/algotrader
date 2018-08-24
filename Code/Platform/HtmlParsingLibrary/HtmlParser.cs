using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace HtmlParsingLibrary
{
    public class ParsedTable
    {
        List<List<Object>> content = new List<List<object>>();

        public int RowCount
        {
            get { return content.Count; }
        }

        public int ColumnCount(int rowIndex)
        {
            return content[rowIndex].Count;
        }

        public Object this[int i, int j]
        {
            get
            {
                if (content.Count <= i)
                    return null;
                List<object> subContent = content[i];
                if (subContent.Count <= j)
                    return null;
                return subContent[j];
            }
        }

        internal int addRow()
        {
            content.Add(new List<object>());
            return content.Count - 1;
        }

        internal void addColumn(int rowIndex, object data)
        {
            List<object> subContent = content[rowIndex];
            subContent.Add(data);
        }

        public void PrintTable(string prefix, TextWriter sw)
        {
            sw.WriteLine("[" + prefix + "] START TABLE");
            for (int i = 0; i < content.Count; i++)
            {
                List<object> subContent = content[i];
                for (int j = 0; j < subContent.Count; j++)
                {
                    string subprefix = null;
                    if (string.IsNullOrEmpty(prefix))
                        subprefix = ("" + i + "," + j);
                    else
                        subprefix += (prefix + "," + i + "," + j);
                    object o = subContent[j];
                    if (o is ParsedTable)
                    {
                        ParsedTable pt = o as ParsedTable;
                        pt.PrintTable(subprefix, sw);
                    }
                    else
                    {
                        sw.WriteLine("[" + subprefix + "] START CONTENT");
                        if (o == null)
                            sw.WriteLine("NULL");
                        else
                            sw.WriteLine(o.ToString());
                        sw.WriteLine("[" + subprefix + "] END CONTENT");
                    }
                }
            }
            sw.WriteLine("[" + prefix + "] END TABLE");
        }

        public static string GetValue(ParsedTable parsedTable, int[] indices)
        {
            if (indices.Length % 2 != 0) throw new InvalidOperationException();
            int i = 0;
            object input = parsedTable;
            while (i < indices.Length)
            {
                ParsedTable table = (ParsedTable)input;
                input = table[indices[i], indices[i + 1]];
                i += 2;
            }
            string inputVal = input.ToString();
            inputVal = inputVal.Replace("&nbsp;", " ");
            inputVal = inputVal.Trim();
            return inputVal;
        }
    }

    public static class HtmlUtilities
    {
        public static string CleanUpHtml(string mainString)
        {
            mainString = EnsureOpenTagsClosed(mainString);
            mainString = EnsureTagClosedBeforeNextTagOpenedOrClosed(mainString, "td", new string[] { "td", "tr", "table" });
            return mainString;
        }

        public static string EnsureHtmlParsable(string mainString)
        {
            // remove all html comments
            mainString = CleanupHtmlComments(mainString);

            // insert closing table tags
            //mainString = EnsureTagsClosed(mainString, "table"); // because there can be nested tables, so do not do this for table tag
            mainString = EnsureTagsClosed(mainString, "tr");
            mainString = EnsureTagsClosed(mainString, "td");

            return mainString;

        }

        public static string EnsureTagsClosed(string mainString, string tag)
        {
            string openingTag = "<" + tag;
            string closingTag = "</" + tag + ">";
            bool bTagsRemaining = true;

            int nextSearchIndex = 0;
            
            while (bTagsRemaining)
            {
                // first opening tag
                int i1 = mainString.IndexOf(openingTag, nextSearchIndex, StringComparison.OrdinalIgnoreCase);
                // no match for comments tag opening found, break out
                if (i1 == -1)
                {
                    break;
                }

                // find 2nd consecutive opening tag
                int i2 = mainString.IndexOf(openingTag, i1 + openingTag.Length, StringComparison.OrdinalIgnoreCase);

                // if 2nd opening tag not found, assign index as end point
                if(i2 == -1)
                {
                    bTagsRemaining = false;
                    i2 = mainString.Length - 1;
                }

                // between the 2 opening tags there should be a closing tag
                // search for it, if not found insert it just before the 2nd opening tag

                string subString = mainString.Substring(i1, i2 - i1);

                // search for closing tag
                int i3 = subString.IndexOf(closingTag, 0, StringComparison.OrdinalIgnoreCase);

                // not found so insert the closing tag
                if (i3 == -1)
                {
                    mainString = mainString.Insert(i2, closingTag);
                }

                nextSearchIndex = i1 + openingTag.Length;
            }
            return mainString;
        }

        public static string CleanupHtmlComments(string mainString)
        {
            while (true)
            {
                int i1 = mainString.IndexOf("<!--", 0, StringComparison.OrdinalIgnoreCase);
                // no match for comments tag opening found, break out
                if (i1 == -1)
                {
                    break;
                }
                int i2 = mainString.IndexOf("-->", i1, StringComparison.OrdinalIgnoreCase);
                i2 += "-->".Length;

                // No closing comment tag found. improper string 
                if (i2 == -1)
                {
                    Debug.Assert(i1 == -1, "Html Comment tags not closed");
                    break;
                }
                // remove all the chars within & including the comments tag
                mainString = mainString.Remove(i1, i2 - i1);
            }
            return mainString;
        }


        static string ExtractBody(string mainString)
        {
            string startTag1 = "<body>";
            string startTag2 = "<body ";
            string endTag = "</body>";

            int si = -1;
            int i1 = mainString.IndexOf(startTag1, 0, StringComparison.OrdinalIgnoreCase);
            int i2 = mainString.IndexOf(startTag2, 0, StringComparison.OrdinalIgnoreCase);
            if (i1 == -1 && i2 == -1)
                return null;
            else if (i1 == -1)
                si = i2;
            else if (i2 == -1)
                si = i1;
            else
                si = Math.Min(i1, i2);
            int openingTagEnd = mainString.IndexOf(">", si);
            int ei = mainString.IndexOf(endTag, 0, StringComparison.OrdinalIgnoreCase);
            if (ei == -1)
                return mainString.Substring(openingTagEnd + 1);
            string bodyString = mainString.Substring(openingTagEnd + 1, ei - openingTagEnd - 1);
            return bodyString;
        }

        //given a string "<abcdd fsgsgfg<hfrefr>" returns
        //"<abcdd fsgsgfg><hfrefr>"
        static string EnsureOpenTagsClosed(string mainString)
        {
            int openBrackIndex = mainString.IndexOf("<", 0);
            while (openBrackIndex != -1)
            {
                int nextOpenBrackIndex = mainString.IndexOf("<", openBrackIndex + 1);
                int closeBrackIndex = mainString.IndexOf(">", openBrackIndex);
                if (nextOpenBrackIndex == -1)
                {
                    if (closeBrackIndex == -1)
                    {
                        mainString = mainString + ">";
                    }
                    break;
                }
                if (closeBrackIndex == -1 || (closeBrackIndex > nextOpenBrackIndex))
                {
                    mainString = mainString.Insert(nextOpenBrackIndex, ">");
                    continue;
                }
                openBrackIndex = nextOpenBrackIndex;
            }
            return mainString;
        }

        static string EnsureTagClosedBeforeNextTagOpenedOrClosed(string mainString,
            string tagToBeClosed,
            string[] nextTagOpened)
        {
            string startTag1 = "<" + tagToBeClosed + ">";
            string startTag2 = "<" + tagToBeClosed + " ";
            string endTag = "</" + tagToBeClosed + ">";

            string[] nextTagStart1 = new string[nextTagOpened.Length];
            string[] nextTagStart2 = new string[nextTagOpened.Length];
            string[] nextTagEnd = new string[nextTagOpened.Length];
            for (int i = 0; i < nextTagOpened.Length; i++)
            {
                string nextTag = nextTagOpened[i];
                string s1 = "<" + nextTag + ">";
                string s2 = "<" + nextTag + " ";
                nextTagStart1[i] = s1;
                nextTagStart2[i] = s2;
                nextTagEnd[i] = "</" + nextTag + ">";
            }

            int startIndex = 0;
            int endIndex = 0;
            while (true)
            {
                int i1 = mainString.IndexOf(startTag1, startIndex, StringComparison.OrdinalIgnoreCase);
                int i2 = mainString.IndexOf(startTag2, startIndex, StringComparison.OrdinalIgnoreCase);
                if (i1 == -1 && i2 == -1)
                {
                    break;
                }
                if (i1 == -1)
                    startIndex = i2;
                else if (i2 == -1)
                    startIndex = i1;
                else
                    startIndex = Math.Min(i1, i2);
                endIndex = mainString.IndexOf(endTag, startIndex, StringComparison.OrdinalIgnoreCase);

                int minNextTagStartIndex = int.MaxValue;

                for (int i = 0; i < nextTagOpened.Length; i++)
                {
                    i1 = mainString.IndexOf(nextTagStart1[i], startIndex + 1, StringComparison.OrdinalIgnoreCase);
                    if (i1 == -1)
                        continue;
                    if (i1 < minNextTagStartIndex)
                        minNextTagStartIndex = i1;
                }
                for (int i = 0; i < nextTagOpened.Length; i++)
                {
                    i2 = mainString.IndexOf(nextTagStart2[i], startIndex + 1, StringComparison.OrdinalIgnoreCase);
                    if (i2 == -1)
                        continue;
                    if (i2 < minNextTagStartIndex)
                        minNextTagStartIndex = i2;
                }
                for (int i = 0; i < nextTagOpened.Length; i++)
                {
                    int i3 = mainString.IndexOf(nextTagEnd[i], startIndex + 1, StringComparison.OrdinalIgnoreCase);
                    if (i3 == -1)
                        continue;
                    if (i3 < minNextTagStartIndex)
                        minNextTagStartIndex = i3;
                }

                if (endIndex == -1)
                {
                    mainString += endTag;
                    break;
                }

                if (minNextTagStartIndex == -1)
                    break;

                if (minNextTagStartIndex < endIndex)
                {
                    mainString = mainString.Insert(minNextTagStartIndex, endTag);
                    continue;
                }

                startIndex++;
            }

            return mainString;
        }

        public static string StackBasedCleanUpTableRelatedTagsInHtml(string originalMainString)
        {
            string mainString = originalMainString.ToLowerInvariant();
            List<string> tags = new List<string>();
            tags.Add("tr");
            tags.Add("td");
            tags.Add("table");

            Stack<string> openTags = new Stack<string>();

            int startIndex = 0;
            while (true)
            {
                int foundIndexForOpen = int.MaxValue;
                string foundOpenTag = null;
                string openTextFound = null;
                foreach (string tag in tags)
                {
                    string searchFor = "<" + tag + " ";
                    int index = mainString.IndexOf(searchFor, startIndex);
                    if ((index != -1) && (index < foundIndexForOpen))
                    {
                        foundIndexForOpen = index;
                        foundOpenTag = tag;
                        openTextFound = searchFor;
                    }

                    searchFor = "<" + tag + ">";
                    index = mainString.IndexOf(searchFor, startIndex);
                    if (index == -1) continue;
                    if (index < foundIndexForOpen)
                    {
                        foundIndexForOpen = index;
                        foundOpenTag = tag;
                        openTextFound = searchFor;
                    }
                }

                int foundIndexForClose = int.MaxValue;
                string foundCloseTag = null;
                string closeTextFound = null;
                foreach (string tag in tags)
                {
                    string searchFor = "</" + tag + " >";
                    int index = mainString.IndexOf(searchFor, startIndex);
                    if ((index != -1) && (index < foundIndexForClose))
                    {
                        foundIndexForClose = index;
                        foundCloseTag = tag;
                        closeTextFound = searchFor;
                    }

                    searchFor = "</" + tag + ">";
                    index = mainString.IndexOf(searchFor, startIndex);
                    if (index == -1) continue;
                    if (index < foundIndexForClose)
                    {
                        foundIndexForClose = index;
                        foundCloseTag = tag;
                        closeTextFound = searchFor;
                    }
                }

                if (foundIndexForOpen == int.MaxValue && foundIndexForClose == int.MaxValue)
                {
                    break;
                }

                if (foundIndexForOpen < foundIndexForClose)
                {
                    openTags.Push(foundOpenTag);
                    startIndex = foundIndexForOpen + openTextFound.Length;
                    continue;
                }

                if (foundIndexForClose < foundIndexForOpen)
                {
                    if (openTags.Count == 0)
                    {
                        //there are some extra close tags at the end
                        //remove those.
                        mainString = mainString.Remove(foundIndexForClose, closeTextFound.Length);
                        continue;
                    }
                    else
                    {
                        string currentOpenTag = openTags.Peek();
                        if (currentOpenTag.Equals(foundCloseTag, StringComparison.OrdinalIgnoreCase))
                        {
                            openTags.Pop();
                            startIndex = foundIndexForClose + closeTextFound.Length;
                            continue;
                        }
                        else
                        {
                            string insertString = "</" + currentOpenTag + ">";
                            mainString = mainString.Insert(foundIndexForClose, insertString);
                            continue;
                        }
                    }
                }
            }

            return mainString;
        }
    }

    public static class HtmlTableParser
    {
        private static int findMatchingEndTag(string mainString, string tagName, int startTagPosition)
        {
            string startTag1 = "<" + tagName + ">";
            string startTag2 = "<" + tagName + " ";
            string endTag = "</" + tagName + ">";
            int numTagsInBetween = 1;
            int searchStartIndex = startTagPosition + 1;
            int endIndex = -1;
            while (numTagsInBetween > 0)
            {
                int si = -1;
                int i1 = mainString.IndexOf(startTag1, searchStartIndex, StringComparison.OrdinalIgnoreCase);
                int i2 = mainString.IndexOf(startTag2, searchStartIndex, StringComparison.OrdinalIgnoreCase);
                if (i1 == -1 && i2 == -1)
                    si = int.MaxValue;
                else if (i1 == -1)
                    si = i2;
                else if (i2 == -1)
                    si = i1;
                else
                    si = Math.Min(i1, i2);
                int ei = mainString.IndexOf(endTag, searchStartIndex, StringComparison.OrdinalIgnoreCase);
                Debug.Assert(ei >= searchStartIndex);
                if (ei < si)
                {
                    endIndex = ei;
                    searchStartIndex = endIndex + 1;
                    numTagsInBetween--;
                    continue;
                }
                else
                {
                    searchStartIndex = si + 1;
                    numTagsInBetween++;
                    continue;
                }
            }
            Debug.Assert(endIndex > startTagPosition);
            return endIndex;
        }

        private static List<object> parseHtmlAsTableRow(string mainString)
        {
            //At this point, we know that the entire "mainString" occurred immediately within
            //"<tr> and "</tr>"
            //therefore, we have to parse it into columns.

            List<object> objectsInRow = new List<object>();
            int searchStartIndex = 0;
            while (true)
            {
                int tcsi = -1;
                int tcei = -1;
                int i1 = mainString.IndexOf("<td>", searchStartIndex, StringComparison.OrdinalIgnoreCase);
                int i2 = mainString.IndexOf("<td ", searchStartIndex, StringComparison.OrdinalIgnoreCase);
                if (i1 == -1 && i2 == -1)
                {
                    break;
                }
                if (i1 == -1)
                    tcsi = i2;
                else if (i2 == -1)
                    tcsi = i1;
                else
                    tcsi = Math.Min(i1, i2);
                int openingTableTagEnd = mainString.IndexOf(">", tcsi);
                tcei = findMatchingEndTag(mainString, "td", tcsi);

                object cellContent = ParseHtmlIntoTables(mainString.Substring(openingTableTagEnd + 1, tcei - openingTableTagEnd - 1), false);
                objectsInRow.Add(cellContent);

                searchStartIndex = tcei + "</td>".Length;
            }

            return objectsInRow;
        }

        private static ParsedTable parseHtmlAsTable(string mainString)
        {
            //At this point, we know that the entire "mainString" occurred immediately within
            //"<table> and "</endtable>"
            //therefore, we have to parse it into rows and columns.
            //this function will parse it into rows

            ParsedTable table = new ParsedTable();
            int searchStartIndex = 0;
            while (true)
            {
                int trsi = -1;
                int trei = -1;
                int i1 = mainString.IndexOf("<tr>", searchStartIndex, StringComparison.OrdinalIgnoreCase);
                int i2 = mainString.IndexOf("<tr ", searchStartIndex, StringComparison.OrdinalIgnoreCase);
                if (i1 == -1 && i2 == -1)
                {
                    break;
                }
                if (i1 == -1)
                    trsi = i2;
                else if (i2 == -1)
                    trsi = i1;
                else
                    trsi = Math.Min(i1, i2);
                int openingTableTagEnd = mainString.IndexOf(">", trsi);
                trei = findMatchingEndTag(mainString, "tr", trsi);

                int rowIndex = table.addRow();
                string subString = mainString.Substring(openingTableTagEnd + 1, trei - openingTableTagEnd - 1).Trim(' ', '\t', '\r', '\n');
                List<object> objectsInRow = parseHtmlAsTableRow(subString);
                foreach (object o in objectsInRow)
                {
                    table.addColumn(rowIndex, o);
                }

                searchStartIndex = trei + "</tr>".Length;
            }

            return table;
        }

        public static Object ParseHtmlIntoTables(string mainString, bool encapsulateInTable)
        {
            List<object> objectsOnPage = new List<object>();

            if (string.IsNullOrEmpty(mainString))
                return null;
            mainString = mainString.Trim(' ', '\t', '\r', '\n');

            int searchStartIndex = 0;
            ParsedTable table = null;
            while (true)
            {
                int tsi = -1, tei = -1;
                if (string.IsNullOrEmpty(mainString))
                    break;
                int i1 = mainString.IndexOf("<table>", searchStartIndex, StringComparison.OrdinalIgnoreCase);
                int i2 = mainString.IndexOf("<table ", searchStartIndex, StringComparison.OrdinalIgnoreCase);
                if (i1 == -1 && i2 == -1)
                {
                    break;
                }
                if (i1 == -1)
                    tsi = i2;
                else if (i2 == -1)
                    tsi = i1;
                else
                    tsi = Math.Min(i1, i2);
                int openingTableTagEnd = mainString.IndexOf(">", tsi);
                tei = findMatchingEndTag(mainString, "table", tsi);

                if (tsi > searchStartIndex)
                {
                    string subTextString = mainString.Substring(searchStartIndex, tsi - searchStartIndex).Trim(' ', '\t', '\r', '\n');
                    objectsOnPage.Add(subTextString);
                    Debug.Assert(subTextString.IndexOf("</td>", StringComparison.OrdinalIgnoreCase) == -1);
                    Debug.Assert(subTextString.IndexOf("</tr>", StringComparison.OrdinalIgnoreCase) == -1);
                    Debug.Assert(subTextString.IndexOf("</table>", StringComparison.OrdinalIgnoreCase) == -1);
                }

                string subString = mainString.Substring(openingTableTagEnd + 1, tei - openingTableTagEnd - 1).Trim(' ', '\t', '\r', '\n');
                table = parseHtmlAsTable(subString);
                objectsOnPage.Add(table);
                table = null;
                searchStartIndex = tei + "</table>".Length;
            }

            string endString = mainString.Substring(searchStartIndex).Trim(' ', '\t', '\r', '\n');
            if (!string.IsNullOrEmpty(endString))
            {
                objectsOnPage.Add(endString);
                Debug.Assert(endString.IndexOf("</td>", StringComparison.OrdinalIgnoreCase) == -1);
                Debug.Assert(endString.IndexOf("</tr>", StringComparison.OrdinalIgnoreCase) == -1);
                Debug.Assert(endString.IndexOf("</table>", StringComparison.OrdinalIgnoreCase) == -1);
            }

            if (objectsOnPage.Count == 0)
                return null;
            if (objectsOnPage.Count == 1)
            {
                object o = objectsOnPage[0];
                if (o is ParsedTable)
                    return o;
                if (!encapsulateInTable)
                    return o;
                table = new ParsedTable();
                table.addRow();
                table.addColumn(0, o);
                return table;
            }
            else
            {
                table = new ParsedTable();
                table.addRow();
                foreach (object o in objectsOnPage)
                {
                    table.addColumn(0, o);
                }
                return table;
            }
        }
    }
}
