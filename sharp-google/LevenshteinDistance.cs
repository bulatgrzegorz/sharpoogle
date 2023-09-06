namespace sharp_google;

//https://www.programmingalgorithms.com/algorithm/levenshtein-distance/
internal static class LevenshteinDistance
{
    private static uint Min3(uint a, uint b, uint c)
    {
        return ((a) < (b) ? ((a) < (c) ? (a) : (c)) : ((b) < (c) ? (b) : (c)));
    }
    
    public static int GetDistance(string s1, string s2)
    {
        uint s1len, s2len, x, y, lastdiag, olddiag;
        s1len = (uint)s1.Length;
        s2len = (uint)s2.Length;
        uint[] column = new uint[s1len + 1];

        for (y = 1; y <= s1len; ++y)
            column[y] = y;

        for (x = 1; x <= s2len; ++x)
        {
            column[0] = x;

            for (y = 1, lastdiag = x - 1; y <= s1len; ++y)
            {
                olddiag = column[y];
                column[y] = Min3(column[y] + 1, column[y - 1] + 1, (uint)(lastdiag + (s1[(int)(y - 1)] == s2[(int)(x - 1)] ? 0 : 1)));
                lastdiag = olddiag;
            }
        }

        return (int)(column[s1len]);
    }
}