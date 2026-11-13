namespace StreamControlCSharp.Models
{
    public class PlayerEntry
    {
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public string Tag { get; set; } = "";

        public override string ToString() => string.IsNullOrWhiteSpace(Tag) ? Name : $"{Tag} | {Name}";
    }

    public class CommentatorEntry
    {
        public string Name { get; set; } = "";
        public string Twitter { get; set; } = "";

        public override string ToString() => Name;
    }
}
