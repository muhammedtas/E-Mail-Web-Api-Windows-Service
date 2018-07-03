namespace EmailWebApi.Model
{
    public class EmailAttachment
    {
        public int EmailId { get; set; }
        public string FileName { get; set; }
        public byte[] File { get; set; }
        
    }
}