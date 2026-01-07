using System;
using System.Collections.Generic;

namespace EasyUpdate.Models
{
    public class UpdatePayload
    {
        public string UpdateContent { get; set; }
        public List<UpdateListItem> List { get; set; }
        public string MainProcess { get; set; }
        public string MainExe { get; set; }

        public UpdatePayload()
        {
            List = new List<UpdateListItem>();
        }
    }
}
