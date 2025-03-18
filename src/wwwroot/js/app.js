// Main application JavaScript file
console.log('Initializing Pocket Story Poker app...');

// Handle mobile menu clicks
document.addEventListener('DOMContentLoaded', function () {
    // Close mobile menu when clicking outside
    document.addEventListener('click', function (event) {
        const menu = document.querySelector('.nav-menu');
        const menuButton = document.querySelector('.mobile-menu-toggle');
        
        if (menu && menu.classList.contains('active')) {
            // Only close if click is outside the menu and not on the toggle button
            if (!menu.contains(event.target) && !menuButton.contains(event.target)) {
                menu.classList.remove('active');
            }
        }
    });

    // Stop propagation for clicks inside the menu to prevent closing
    const menuItems = document.querySelectorAll('.nav-menu a, .user-profile');
    menuItems.forEach(item => {
        item.addEventListener('click', function(e) {
            e.stopPropagation();
        });
    });

    // Add touch enhancement for buttons
    const buttons = document.querySelectorAll('.btn');
    buttons.forEach(button => {
        button.addEventListener('touchstart', function() {
            this.classList.add('btn-touch');
        });
        
        button.addEventListener('touchend', function() {
            this.classList.remove('btn-touch');
        });
    });

    // Check for session reconnection on page load/refresh
    console.log('Page loaded. Checking for active session...');
    const currentSessionId = localStorage.getItem('currentSessionId');
    if (currentSessionId) {
        console.log(`Found active session: ${currentSessionId}`);
    }
});

// Adjust viewport height for mobile browsers
function setMobileHeight() {
    const vh = window.innerHeight * 0.01;
    document.documentElement.style.setProperty('--vh', `${vh}px`);
}

window.addEventListener('resize', setMobileHeight);
setMobileHeight();

// Toast notification function
window.showToast = function(message, duration = 3000) {
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.textContent = message;

    // Append to body
    document.body.appendChild(toast);

    // Show toast
    setTimeout(() => {
        toast.classList.add('toast-visible');
    }, 10);

    // Remove toast after duration
    setTimeout(() => {
        toast.classList.remove('toast-visible');
        setTimeout(() => {
            document.body.removeChild(toast);
        }, 300);
    }, duration);
};

// Function to clear previous session data
window.clearPreviousSessionData = function() {
    try {
        console.log('Clearing previous session data');
        localStorage.removeItem('currentSessionId');
        // Keep userName for convenience
    } catch (error) {
        console.error('Error clearing session data:', error);
    }
};

// Function to reset app state (keep only essential data)
window.resetAppState = function() {
    try {
        console.log('Resetting app state');
        localStorage.removeItem('currentSessionId');
        // Keep userName for convenience
    } catch (error) {
        console.error('Error resetting app state:', error);
    }
};

// Log navigation to help debug SPA routing
let lastUrl = window.location.href;
window.addEventListener('popstate', () => {
    if (lastUrl !== window.location.href) {
        console.log(`Navigation: ${lastUrl} -> ${window.location.href}`);
        lastUrl = window.location.href;
    }
});

// Use unload event to detect page refreshes/navigations
window.addEventListener('beforeunload', function() {
    console.log('Page unloading. Current session ID:', localStorage.getItem('currentSessionId'));
});

// Report any SignalR connection errors
window.reportSignalRError = function(error) {
    console.error('SignalR connection error:', error);
};

// Function to focus an HTML element
window.focusElement = function(element) {
    if (element) {
        setTimeout(() => {
            element.focus();
        }, 100);
    }
};

// Session tracking to prevent multiple tabs - improved version
window.sessionManager = {
    // Key used for tracking active sessions
    activeSessionKey: "activeSession",
    
    // Session heartbeat interval (ms)
    heartbeatInterval: 1000, // Faster heartbeat
    
    // Get random ID for this tab instance
    tabId: Math.random().toString(36).substring(2, 12),
    
    // Timer reference
    heartbeatTimer: null,
    
    // Mark this session as active in this tab
    markSessionActive: function(sessionId) {
        const now = new Date().getTime();
        const sessionData = {
            sessionId: sessionId,
            tabId: this.tabId,
            lastHeartbeat: now,
            timestamp: now // When this session was first activated
        };
        
        try {
            // Check if another session is already active
            if (this.isSessionActive(sessionId)) {
                console.log("Session already active in another tab");
                return false;
            }
            
            localStorage.setItem(this.activeSessionKey, JSON.stringify(sessionData));
            console.log(`Session ${sessionId} marked active in tab ${this.tabId}`);
            
            // Start sending heartbeats
            if (this.heartbeatTimer) {
                clearInterval(this.heartbeatTimer);
            }
            
            this.heartbeatTimer = setInterval(() => {
                this.sendHeartbeat(sessionId);
            }, this.heartbeatInterval);
            
            // Add storage event listener to detect changes from other tabs
            window.removeEventListener('storage', this.storageListener);
            window.addEventListener('storage', this.storageListener);
            
            return true;
        } catch (error) {
            console.error("Error marking session active:", error);
            return false;
        }
    },
    
    // Send regular heartbeats to keep the session marked as active
    sendHeartbeat: function(sessionId) {
        try {
            const activeSessionJson = localStorage.getItem(this.activeSessionKey);
            if (!activeSessionJson) return;
            
            const activeSession = JSON.parse(activeSessionJson);
            
            // Only update if this is our tab and session
            if (activeSession.tabId === this.tabId && activeSession.sessionId === sessionId) {
                activeSession.lastHeartbeat = new Date().getTime();
                localStorage.setItem(this.activeSessionKey, JSON.stringify(activeSession));
            }
        } catch (error) {
            console.error("Error sending heartbeat:", error);
        }
    },
    
    // Check if the session is already active in another tab
    isSessionActive: function(sessionId) {
        try {
            const activeSessionJson = localStorage.getItem(this.activeSessionKey);
            if (!activeSessionJson) return false;
            
            const activeSession = JSON.parse(activeSessionJson);
            const now = new Date().getTime();
            
            // If the heartbeat is stale (>5 seconds), consider the session inactive
            const staleThreshold = 5000; // 5 seconds
            if (now - activeSession.lastHeartbeat > staleThreshold) {
                console.log("Found stale session, considering inactive");
                localStorage.removeItem(this.activeSessionKey);
                return false;
            }
            
            // If it's our tab, it's fine
            if (activeSession.tabId === this.tabId) {
                return false;
            }
            
            // Check if it's the same session
            const isSameSession = activeSession.sessionId === sessionId;
            
            // If same session in another tab, it's active elsewhere
            if (isSameSession) {
                console.log(`Session ${sessionId} already active in tab ${activeSession.tabId}`);
                return true;
            }
            
            return false;
        } catch (error) {
            console.error("Error checking active session:", error);
            return false;
        }
    },
    
    // Listen for storage changes from other tabs
    storageListener: function(event) {
        if (event.key === window.sessionManager.activeSessionKey) {
            console.log("Session storage changed in another tab");
        }
    },
    
    // Clear active session tracking
    clearActiveSession: function() {
        try {
            // Get the current active session info
            const activeSessionJson = localStorage.getItem(this.activeSessionKey);
            if (activeSessionJson) {
                const activeSession = JSON.parse(activeSessionJson);
                
                // Only remove if it's our tab
                if (activeSession.tabId === this.tabId) {
                    localStorage.removeItem(this.activeSessionKey);
                    console.log("Active session cleared");
                }
            }
            
            if (this.heartbeatTimer) {
                clearInterval(this.heartbeatTimer);
                this.heartbeatTimer = null;
            }
            
            // Remove storage event listener
            window.removeEventListener('storage', this.storageListener);
        } catch (error) {
            console.error("Error clearing active session:", error);
        }
    },

    saveSessionCreationTime: function(sessionId, timestamp) {
        localStorage.setItem(`session_${sessionId}_created`, timestamp);
    },

    getSessionCreationTime: function(sessionId) {
        return localStorage.getItem(`session_${sessionId}_created`) || '';
    },

    // Add reconnection tracking to sessionManager
    trackLastActivity: function(sessionId) {
        localStorage.setItem(`session_${sessionId}_lastActivity`, new Date().toISOString());
    },

    getLastActivity: function(sessionId) {
        return localStorage.getItem(`session_${sessionId}_lastActivity`);
    },

    // Check if this appears to be a page refresh rather than a new visit
    isReconnecting: function(sessionId) {
        const lastActivity = this.getLastActivity(sessionId);
        if (!lastActivity) return false;
        
        const lastActivityTime = new Date(lastActivity);
        const now = new Date();
        
        // If last activity was within 30 seconds, consider it a refresh/reconnection
        return (now - lastActivityTime) < 30000;
    }
};

// Extend sessionManager with persistence functions
window.sessionManager.saveSessionData = function(sessionId, data) {
    localStorage.setItem(`session_${sessionId}_data`, JSON.stringify(data));
};

window.sessionManager.getSessionData = function(sessionId) {
    const data = localStorage.getItem(`session_${sessionId}_data`);
    if (data) {
        try {
            return JSON.parse(data);
        } catch (e) {
            console.error("Error parsing session data", e);
            return null;
        }
    }
    return null;
};

// Make sure we clean up on page navigation, not just on unload
const originalPushState = history.pushState;
history.pushState = function() {
    window.sessionManager.clearActiveSession();
    return originalPushState.apply(this, arguments);
};

// Clean up on page unload
window.addEventListener('beforeunload', function() {
    window.sessionManager.clearActiveSession();
});

// Clean up on browser navigation
window.addEventListener('popstate', function() {
    window.sessionManager.clearActiveSession();
});

// User name management functions
window.userManager = {
    // Save username to localStorage
    saveUserName: function(name) {
        if (!name) return false;
        
        try {
            localStorage.setItem('userName', name);
            console.log(`Username saved: ${name}`);
            return true;
        } catch (error) {
            console.error('Error saving username:', error);
            return false;
        }
    },
    
    // Get username from localStorage
    getUserName: function() {
        try {
            return localStorage.getItem('userName') || '';
        } catch (error) {
            console.error('Error retrieving username:', error);
            return '';
        }
    },
    
    // Clear username
    clearUserName: function() {
        try {
            localStorage.removeItem('userName');
            console.log('Username cleared');
            return true;
        } catch (error) {
            console.error('Error clearing username:', error);
            return false;
        }
    },
    
    // Check if username exists
    hasUserName: function() {
        try {
            const name = localStorage.getItem('userName');
            return name !== null && name.trim() !== '';
        } catch (error) {
            console.error('Error checking username:', error);
            return false;
        }
    }
};

// Game state management
window.gameStateManager = {
    saveState: function(sessionId, key, value) {
        localStorage.setItem(`game_${sessionId}_${key}`, JSON.stringify(value));
    },
    
    getState: function(sessionId, key) {
        var value = localStorage.getItem(`game_${sessionId}_${key}`);
        if (value === null) return null;
        try {
            return JSON.parse(value);
        } catch(e) {
            return value;
        }
    },
    
    clearState: function(sessionId) {
        // Get all keys in localStorage
        const keys = [];
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key.startsWith(`game_${sessionId}_`)) {
                keys.push(key);
            }
        }
        
        // Remove all matching keys
        keys.forEach(key => localStorage.removeItem(key));
        console.log(`Cleared ${keys.length} game state items for session ${sessionId}`);
    }
};

// Update regularly while the session is active
setInterval(function() {
    const currentSessionId = localStorage.getItem('currentSessionId');
    if (currentSessionId) {
        window.sessionManager.trackLastActivity(currentSessionId);
    }
}, 5000);
