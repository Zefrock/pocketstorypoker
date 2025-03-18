using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PocketStoryPoker.Models;
using PocketStoryPoker.Services;

namespace PocketStoryPoker.Hubs
{
    public class PokerHub : Hub
    {
        private readonly ILoggingService _logger;

        public PokerHub(ILoggingService logger)
        {
            _logger = logger;
        }

        // Track active sessions and participants - use thread-safe collection
        private static readonly ConcurrentDictionary<string, List<Participant>> _sessions =
            new ConcurrentDictionary<string, List<Participant>>();
            
        // Track game state per session
        private static readonly ConcurrentDictionary<string, string> _gameStates =
            new ConcurrentDictionary<string, string>();
        
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _gameVotes =
            new ConcurrentDictionary<string, Dictionary<string, string>>();

        private static readonly ConcurrentDictionary<string, List<string>> _disconnectedUsers =
            new ConcurrentDictionary<string, List<string>>();

        private async Task ResetParticipantVotes(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            foreach (var participant in participants)
            {
                participant.HasVoted = false;
                await Clients.Group(sessionId).SendAsync("ParticipantUpdated", participant);
            }
        }

        public async Task JoinSession(string userName, string sessionId)
        {
            try
            {
                // Store the username in the connection context for later use
                if (Context.Items != null)
                {
                    Context.Items["UserName"] = userName;
                }

                // Check if this user already exists in the session
                Participant? existingParticipant = null;
                bool wasDisconnected = false;

                if (_sessions.TryGetValue(sessionId, out var existingParticipants) && existingParticipants != null)
                {
                    // Look for a participant by connection ID first (for name changes)
                    existingParticipant = existingParticipants
                        .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

                    if (existingParticipant == null)
                    {
                        // If not found by connection ID, look by name
                        existingParticipant = existingParticipants
                            .FirstOrDefault(p => p.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (existingParticipant != null)
                    {
                        // This is a reconnection
                        wasDisconnected = !existingParticipant.IsConnected;

                        // Update connection details and name
                        existingParticipant.ConnectionId = Context.ConnectionId;
                        existingParticipant.Name = userName; // Update name in case it changed
                        existingParticipant.IsConnected = true;
                        existingParticipant.DisconnectedAt = null;

                        _logger.LogInfo($"User {userName} is reconnecting to session {sessionId}");
                    }
                }

                // Add user to session group
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

                // If user doesn't exist yet, create a new participant
                if (existingParticipant == null)
                {
                    // Create new participant record
                    var participant = new Participant
                    {
                        ConnectionId = Context.ConnectionId,
                        Name = userName,
                        SessionId = sessionId,
                        JoinedAt = DateTime.UtcNow,
                        IsConnected = true
                    };

                    // Add to session or create new session
                    if (!_sessions.TryGetValue(sessionId, out _))
                    {
                        // New session
                        _sessions[sessionId] = new List<Participant> { participant };
                    }
                    else
                    {
                        existingParticipants?.Add(participant);
                    }

                    // Notify others about new participant
                    await Clients.OthersInGroup(sessionId).SendAsync("ParticipantJoined", participant);
                }
                else
                {
                    // Notify others about reconnected participant
                    await Clients.OthersInGroup(sessionId).SendAsync(
                        wasDisconnected ? "ParticipantReconnected" : "ParticipantUpdated",
                        existingParticipant);
                }

                // Send full participants list to the joining/reconnecting user
                if (_sessions.TryGetValue(sessionId, out var participants) && participants != null)
                {
                    await Clients.Caller.SendAsync("SessionParticipants", participants);
                }

                // Send current game state to joining user
                var gameState = _gameStates.GetValueOrDefault(sessionId);
            if (!string.IsNullOrEmpty(gameState))
            {
                // If game is in selecting phase, tell client to show cards
                if (gameState?.Equals("SELECTING", StringComparison.Ordinal) == true)
                    {
                        await Clients.Caller.SendAsync("GameStarted");
                    }
                    // If game is showing results, get votes and tell client to show them
                    else if (gameState?.Equals("SHOWING_RESULTS", StringComparison.Ordinal) == true && _gameVotes.TryGetValue(sessionId, out var currentVotes))
                    {
                        foreach (var vote in currentVotes)
                        {
                            await Clients.Caller.SendAsync("VoteSubmitted", vote.Key, vote.Value);
                        }
                        await Clients.Caller.SendAsync("ShowResults");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in JoinSession: {ex.Message}");
            }
        }

        public async Task LeaveSession(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            // Find the participant in any case (connected or not)
            string? userName = Context.Items?["UserName"]?.ToString();
            var participant = participants.FirstOrDefault(p =>
                p.ConnectionId == Context.ConnectionId ||
                (userName != null && p.Name == userName && !p.IsConnected));

            if (participant != null)
            {
                // This is an explicit leave, completely remove the participant from the list
                participants.Remove(participant);
                _logger.LogInfo($"Participant {participant.Name} explicitly left session {sessionId}");

                // Notify others that participant has left
                await Clients.OthersInGroup(sessionId).SendAsync("ParticipantLeft", participant);
            }

            // Clean up empty sessions
            if (participants.Count == 0)
            {
                _ = Task.Delay(60000).ContinueWith(t =>
                {
                    if (_sessions.TryGetValue(sessionId, out var checkParticipants) &&
                        checkParticipants?.Count == 0)
                    {
                        _sessions.TryRemove(sessionId, out _);
                        _logger.LogInfo($"Empty session {sessionId} removed after timeout");
                    }
                });
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        }

        public async Task EndSessionForAll(string sessionId, string userName = "")
        {
            if (!_sessions.TryGetValue(sessionId, out var sessionParticipants) || sessionParticipants == null)
                return;

            var caller = sessionParticipants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (caller == null) return;

            try
            {
                string displayName = string.IsNullOrEmpty(userName) ? caller.Name : userName;
                _logger.LogInfo($"SESSION END: Session {sessionId} being ended by {displayName}");

                var participants = new List<Participant>(sessionParticipants);
                _logger.LogInfo($"Removing {participants.Count} participants from ended session {sessionId}");

                // Notify all participants that the session has ended
                await Clients.Group(sessionId).SendAsync("SessionEnded", $"Session ended by {displayName}");

                // Remove all participants from the group
                foreach (var participant in participants)
                {
                    await Groups.RemoveFromGroupAsync(participant.ConnectionId, sessionId);
                }

                // Clean up the session
                _sessions.TryRemove(sessionId, out _);
                _gameStates.TryRemove(sessionId, out _);
                _gameVotes.TryRemove(sessionId, out _);

                _logger.LogInfo($"Session {sessionId} successfully ended by {displayName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ending session: {ex.Message}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                foreach (var sessionEntry in _sessions)
                {
                    string sessionId = sessionEntry.Key;
                    var participant = sessionEntry.Value?.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

                    if (participant != null)
                    {
                        // Mark as disconnected instead of removing
                        participant.IsConnected = false;
                        participant.DisconnectedAt = DateTime.UtcNow;

                        _logger.LogInfo($"DISCONNECT: {participant.Name} disconnected from session {sessionId}");

                        // Notify others that participant disconnected
                        await Clients.Group(sessionId).SendAsync("ParticipantDisconnected", participant);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnDisconnectedAsync: {ex.Message}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task FinalizeLeaveSession(string sessionId, Participant participant)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            // Remove from participants list
            participants.Remove(participant);

            // If session is now empty, don't remove it immediately
            // This allows for page refreshes without losing the session
            if (participants.Count == 0)
            {
                // Instead of immediately removing, start a delayed removal
                // This gives time for the user to reconnect on refresh
                _ = Task.Delay(10000).ContinueWith(t =>
                {
                    if (_sessions.TryGetValue(sessionId, out var checkParticipants) &&
                        checkParticipants?.Count == 0)
                    {
                        _sessions.TryRemove(sessionId, out _);
                        _gameStates.TryRemove(sessionId, out _);
                        _gameVotes.TryRemove(sessionId, out _);
                        _logger.LogInfo($"Session {sessionId} removed after timeout (no participants)");
                    }
                });
            }

            // Notify others
            await Clients.Group(sessionId).SendAsync("ParticipantLeft", participant);
        }

        public bool SessionExists(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            return _sessions.ContainsKey(sessionId);
        }

        public async Task UpdateParticipantName(string sessionId, string oldName, string newName)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            // Find participant by old name
            var participant = participants.FirstOrDefault(p => 
                p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));

            if (participant != null)
            {
                // Update name
                participant.Name = newName;
                _logger.LogInfo($"User name changed from {oldName} to {newName} in session {sessionId}");

                // Update any existing votes
                if (_gameVotes.TryGetValue(sessionId, out var votes) && votes != null)
                {
                    if (votes.TryGetValue(oldName, out var vote))
                    {
                        votes.Remove(oldName);
                        votes[newName] = vote;
                    }
                }

                // Notify ALL participants about the name change, including the sender
                await Clients.Group(sessionId).SendAsync("ParticipantUpdated", participant);

                // Force a refresh of the participant list to ensure synchronization
                await Clients.Group(sessionId).SendAsync("SessionParticipants", participants);
            }
        }

        public bool CreateSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogError("Cannot create session: Empty session ID");
                return false;
            }

            try
            {
                // If session exists, just return true
                if (_sessions.ContainsKey(sessionId))
                {
                    _logger.LogInfo($"Session already exists with ID: {sessionId}");
                    return true;
                }

                // Initialize a new empty session with a placeholder list
                var newSessionAdded = _sessions.TryAdd(sessionId, new List<Participant>());

                if (newSessionAdded)
                {
                    _logger.LogInfo($"Created new session successfully with ID: {sessionId}");
                    return true;
                }
                else
                {
                    var exists = _sessions.ContainsKey(sessionId);
                    _logger.LogInfo($"Failed to add session {sessionId}, exists now: {exists}");
                    return exists;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating session {sessionId}: {ex.Message}");
                return false;
            }
        }

        public async Task StartEstimationGame(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            try
            {
                // Reset voting status and notify all participants
                await ResetParticipantVotes(sessionId);

                // Mark game as in selecting phase
                _gameStates[sessionId] = "SELECTING";
                
                // Initialize or clear votes for this session
                _gameVotes[sessionId] = new Dictionary<string, string>();

                // Notify all participants about the estimation game
                await Clients.Group(sessionId).SendAsync("GameStarted");

                _logger.LogInfo($"Estimation game started in session {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting estimation game: {ex.Message}");
            }
        }

        public async Task SubmitVote(string sessionId, string userName, string value)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            try
            {
                // Update participant's voting status and store vote
                var participant = participants.FirstOrDefault(p => p.Name == userName);
                if (participant != null)
                {
                    participant.HasVoted = true;
                    
                    // Store the vote
                    if (!_gameVotes.ContainsKey(sessionId))
                    {
                        _gameVotes[sessionId] = new Dictionary<string, string>();
                    }
                    _gameVotes[sessionId][userName] = value;
                }

                // Broadcast the vote and participant status to all participants
                await Clients.Group(sessionId).SendAsync("VoteSubmitted", userName, value);
                await Clients.Group(sessionId).SendAsync("ParticipantUpdated", participant);

                _logger.LogInfo($"User {userName} voted {value} in session {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error submitting vote: {ex.Message}");
            }
        }

        public async Task ShowResults(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            try
            {
                // Mark game as showing results
                _gameStates[sessionId] = "SHOWING_RESULTS";

                // Get current votes for this session
                var votes = _gameVotes.GetValueOrDefault(sessionId, new Dictionary<string, string>());
                _logger.LogInfo($"Showing results for session {sessionId}, votes count: {votes.Count}");

                // Re-send all votes to ensure clients have the latest state
                foreach (var vote in votes)
                {
                    await Clients.Group(sessionId).SendAsync("VoteSubmitted", vote.Key, vote.Value);
                }

                // Tell everyone to show results
                await Clients.Group(sessionId).SendAsync("ShowResults");

                _logger.LogInfo($"Results shown in session {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing results: {ex.Message}");
            }
        }

        public async Task StartNewEstimation(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var participants) || participants == null)
                return;

            try
            {
                // Reset all votes first
                await ResetParticipantVotes(sessionId);

                // Mark game as in selecting phase
                _gameStates[sessionId] = "SELECTING";
                
                // Clear votes for new game
                if (_gameVotes.TryGetValue(sessionId, out var votes) && votes != null)
                {
                    votes.Clear();
                }
                else
                {
                    _gameVotes[sessionId] = new Dictionary<string, string>();
                }

                // Tell everyone to reset and start new estimation
                await Clients.Group(sessionId).SendAsync("StartNewEstimation");

                _logger.LogInfo($"New estimation started in session {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting new estimation: {ex.Message}");
            }
        }

        public Task<string> GetCurrentGameState(string sessionId)
        {
            if (!_sessions.ContainsKey(sessionId))
                return Task.FromResult(string.Empty);

            if (_gameStates.TryGetValue(sessionId, out string? state))
            {
                return Task.FromResult(state ?? string.Empty);
            }

            return Task.FromResult(string.Empty);
        }

        public Task<Dictionary<string, string>> GetCurrentVotes(string sessionId)
        {
            if (_gameVotes.TryGetValue(sessionId, out var votes) && votes != null)
            {
                return Task.FromResult(votes);
            }

            return Task.FromResult(new Dictionary<string, string>());
        }
    }
}
