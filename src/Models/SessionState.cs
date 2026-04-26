namespace PowerShot.Models
{
    public class SessionState
    {
        public string LastDirectory { get; set; }
        public string LastPrefix { get; set; }
        public int LastSequenceDigits { get; set; }
        public string LastFormat { get; set; }

        public SessionState()
        {
            LastDirectory = "";
            LastPrefix = "";
            LastSequenceDigits = 3;
            LastFormat = "jpg";
        }
    }
}
