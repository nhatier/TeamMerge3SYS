namespace Logic.Helpers
{
    public static class StringHelpers
    {
        public static bool IsEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }
    }
}
