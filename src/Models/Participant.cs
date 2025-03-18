
namespace PocketStoryPoker.Models
{
    public class Participant
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public bool IsHost { get; set; } = false;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsConnected { get; set; } = true;
        public DateTime? DisconnectedAt { get; set; } = null;
        public bool HasVoted { get; set; } = false;
        public string? SelectedValue { get; set; }
    }
}
