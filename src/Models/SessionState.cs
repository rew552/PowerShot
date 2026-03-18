namespace PowerShot.Models
{
    // ============================================================
    // Session State (persisted across UI show/hide within same PS process)
    // ============================================================
    public class SessionState
    {
        public string LastDirectory { get; set; }
        public string LastPrefix { get; set; }
        public int LastSequenceDigits { get; set; }
        public bool IsSystemInfoVisible { get; set; }
        public string LastFormat { get; set; }

        public SessionState()
        {
            LastDirectory = "";
            LastPrefix = "";
            LastSequenceDigits = 3;
            IsSystemInfoVisible = false;
            LastFormat = "jpg";
        }
    }
}
