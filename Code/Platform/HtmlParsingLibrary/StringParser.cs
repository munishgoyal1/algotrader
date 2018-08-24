
namespace HtmlParsingLibrary
{
    public static class StringParser
    {
        /// <summary>
        /// Parses a the string passed in, and returns the required string. The string returned depends on the parameters.
        /// </summary>
        /// <param name="mainString">The string which needs to be parsed.</param>
        /// <param name="startIndex">The starting index in the Main string where searching should begin from.</param>
        /// <param name="strStart">At the end of "strStart", X is assumed to begin.</param>
        /// <param name="strEnd">X ends just before "strEnd".</param>
        /// <param name="markers">before searching for "strStart" in "mainString", we search for all the strings in the "markers" array sequentially.</param>
        /// <param name="foundAtIndex">The index at which X was found.</param>
        /// <returns>A string (lets call this string X). X is null if nothing could be found matching the criteria entered.</returns>
        public static string GetStringBetween(string mainString, int startIndex, string strStart, string strEnd, string[] markers, out int foundAtIndex)
        {
            foundAtIndex = -1;
            if (mainString == null)
                return null;
            int st = startIndex;
            if (markers != null)
            {
                for (int i = 0; i < markers.Length; i++)
                {
                    var midx = mainString.IndexOf(markers[i], st);
                    if (midx < 0)
                        //return null;
                        continue;

                    st = midx + markers[i].Length;
                }
            }
            int i1 = mainString.IndexOf(strStart, st);
            if (i1 < 0)
                return null;
            int i2 = mainString.IndexOf(strEnd, i1 + strStart.Length);
            if (i2 < 0)
                return null;
            int x = i1 + strStart.Length;
            string data = mainString.Substring(x, i2 - x);
            foundAtIndex = x;
            return data;
        }

        /// <summary>
        /// Parses a the string passed in, and returns the required string. The string returned depends on the parameters.
        /// </summary>
        /// <param name="mainString">The string which needs to be parsed.</param>
        /// <param name="startIndex">The starting index in the Main string where searching should begin from.</param>
        /// <param name="strStart">At the end of "strStart", X is assumed to begin.</param>
        /// <param name="strEnd">X ends just before "strEnd".</param>
        /// <param name="markers">before searching for "strStart" in "mainString", we search for all the strings in the "markers" array sequentially.</param>
        /// <returns>A string (lets call this string X). X is null if nothing could be found matching the criteria entered.</returns>
        public static string GetStringBetween(string mainString, int startIndex, string strStart, string strEnd, string[] markers)
        {
            int dummyIndex;
            return GetStringBetween(mainString, startIndex, strStart, strEnd, markers, out dummyIndex);
        }


        public static string GetCleanedupString(string input)
        {
            string cleanedString = input;

            cleanedString = cleanedString.Replace("&nbsp;", "");

            return cleanedString;
        }
    }
}