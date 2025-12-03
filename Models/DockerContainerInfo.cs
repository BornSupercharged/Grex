using System;

namespace Grex.Models
{
    public class DockerContainerInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        public string ShortId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Id))
                    return string.Empty;

                return Id.Length <= 12 ? Id : Id[..12];
            }
        }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name))
                    return ShortId;

                return $"{Name} ({ShortId})";
            }
        }

        public override string ToString() => DisplayName;
    }
}


