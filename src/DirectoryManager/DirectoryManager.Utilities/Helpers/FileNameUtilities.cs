namespace DirectoryManager.Utilities.Helpers
{
    public class FileNameUtilities
    {
        public static string RemoveSpacesInFileName(string fileName)
        {
            return fileName.Replace(" ", string.Empty);
        }

        public static string GetFileExtensionLower(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.ToLowerInvariant();
        }
    }
}