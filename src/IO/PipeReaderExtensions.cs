using System.IO.Pipelines;

internal static class PipeReaderExtensions
{
    public static byte[] ReadAsByteArray( this PipeReader reader )
    {
        using ( var ms = new MemoryStream() )
        {
            reader.CopyToAsync( ms );

            return ms.ToArray();
        }
    }
}
