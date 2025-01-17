using UnityEngine;
using Unity.Netcode;
using System;
using TMPro;

namespace KKL.Ronda.Networking
{
    /// <summary>
    /// Handles turn management and synchronization across the network
    /// </summary>
    public class TurnManager : NetworkBehaviour
    {
        // Network Variables
        internal readonly NetworkVariable<ulong> CurrentPlayerTurnId = new();

        private readonly NetworkVariable<float> _turnTimeRemaining = new();
        private readonly NetworkVariable<bool> _isTurnActive = new();
        
        // Constants
        private const float TurnTimeLimit = 30f; // 30 seconds per turn
        private const float TurnWarningTime = 10f; // Warning when 10 seconds remain
        
        // Events
        public event Action<ulong> OnTurnChanged;
        public event Action OnTurnWarning;
        public event Action OnTurnTimeout;
        
        // References
        [SerializeField] private TMP_Text turnIndicatorText;
        [SerializeField] private TMP_Text timerText;
        
        public ulong CurrentPlayerId => CurrentPlayerTurnId.Value;
        

        // Add verification method
        public bool VerifyTurnChange(ulong oldPlayerId, ulong newPlayerId)
        {
            if (!_isTurnActive.Value) return false;
            if (CurrentPlayerTurnId.Value != newPlayerId) return false;
        
            Debug.Log($"Turn change verified: {oldPlayerId} -> {newPlayerId}");
            return true;
        }
        
        private void Update()
        {
            if (IsServer && _isTurnActive.Value)
            {
                UpdateTurnTimer();
            }
            
            if (IsClient)
            {
                UpdateTimerDisplay();
            }
        }
        
        public void Initialize()
        {
            if (!IsServer) return;
            
            _isTurnActive.Value = false;
            _turnTimeRemaining.Value = TurnTimeLimit;
        }
        
        /// <summary>
        /// Starts the first turn of the game
        /// </summary>
        public void StartFirstTurn(ulong firstPlayerId)
        {
            if (!IsServer) return;
            
            CurrentPlayerTurnId.Value = firstPlayerId;
            _isTurnActive.Value = true;
            _turnTimeRemaining.Value = TurnTimeLimit;
            
            UpdateTurnIndicatorClientRpc(CurrentPlayerTurnId.Value);
            OnTurnChanged?.Invoke(CurrentPlayerTurnId.Value);
        }
        
        /// <summary>
        /// Advances to the next player's turn
        /// </summary>
        public void AdvanceTurn(ulong nextPlayerId)
        {
            if (!IsServer) return;
        
            Debug.Log($"TurnManager: Advancing turn to player {nextPlayerId}");
            CurrentPlayerTurnId.Value = nextPlayerId;
            _turnTimeRemaining.Value = TurnTimeLimit;
            _isTurnActive.Value = true;
        
            UpdateTurnIndicatorClientRpc(CurrentPlayerTurnId.Value);
            OnTurnChanged?.Invoke(CurrentPlayerTurnId.Value);
        }

        
        private void UpdateTurnTimer()
        {
            if (_turnTimeRemaining.Value <= 0)
            {
                HandleTurnTimeout();
                return;
            }
            
            _turnTimeRemaining.Value -= Time.deltaTime;
            
            if (_turnTimeRemaining.Value <= TurnWarningTime)
            {
                TurnWarningClientRpc();
            }
        }
        
        private void HandleTurnTimeout()
        {
            _isTurnActive.Value = false;
            TurnTimeoutClientRpc();
            // Auto-advance turn or handle timeout logic
        }
        
        private void UpdateTimerDisplay()
        {
            if (timerText != null)
            {
                timerText.text = $"Time: {Mathf.CeilToInt(_turnTimeRemaining.Value)}s";
                timerText.color = _turnTimeRemaining.Value <= TurnWarningTime ? Color.red : Color.white;
            }
        }
        
        [ClientRpc]
        private void UpdateTurnIndicatorClientRpc(ulong currentPlayerId)
        {
            if (turnIndicatorText != null)
            {
                bool isLocalPlayerTurn = currentPlayerId == NetworkManager.LocalClientId;
                turnIndicatorText.text = isLocalPlayerTurn ? "Your Turn" : "Opponent's Turn";
                turnIndicatorText.color = isLocalPlayerTurn ? Color.green : Color.red;
            }
        }
        
        [ClientRpc]
        private void TurnWarningClientRpc()
        {
            OnTurnWarning?.Invoke();
        }
        
        [ClientRpc]
        private void TurnTimeoutClientRpc()
        {
            OnTurnTimeout?.Invoke();
        }
        
        public bool IsPlayerTurn(ulong playerId)
        {
            return _isTurnActive.Value && CurrentPlayerTurnId.Value == playerId;
        }
    }
}

