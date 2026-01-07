using System;

namespace EasyUpdate.Models
{
    public class UpdateListItem
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Md5 { get; set; }
        public bool IsZip { get; set; }
        public string ZipPass { get; set; }
        public string ExtractName { get; set; }
        public string SavePath { get; set; }

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(Name))
                return Name;
            if (!string.IsNullOrEmpty(ExtractName))
                return ExtractName;
            try
            {
                var uri = new Uri(Url);
                return System.IO.Path.GetFileName(uri.LocalPath);
            }
            catch
            {
                return Url;
            }
        }
    }
}
