internal static class HeaderDictionaryExtensions
{
    public static string? GetValueOrDefault( this IHeaderDictionary headers, string key, string? valueDefault = null )
    {
        if ( headers.TryGetValue( key, out var value ) )
        {
            return value.ToString();
        }

        return ( valueDefault );
    }
}
