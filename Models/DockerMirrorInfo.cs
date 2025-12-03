using System;

namespace Grex.Models
{
    public class DockerMirrorInfo
    {
        public string ContainerId { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public string ContainerPath { get; set; } = string.Empty;
        public string LocalMirrorPath { get; set; } = string.Empty;
        public string LocalSearchPath { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}


