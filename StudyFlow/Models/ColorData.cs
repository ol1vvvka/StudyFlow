namespace StudyFlow.Models
{
    public class ColorData
    {
        public ColorData(string hex, string description)
        {
            Hex = hex;
            Description = description;
        }

        public string Hex { get; }
        public string Description { get; }
        public Color Color => Microsoft.Maui.Graphics.Color.FromArgb(Hex);
    }
}
