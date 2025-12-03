namespace Grex.Models
{
    public class DockerContainerOption
    {
        public string Label { get; set; } = string.Empty;
        public DockerContainerInfo? Container { get; set; }
        public bool IsLocal => Container == null;

        public override string ToString() => Label;
    }
}


