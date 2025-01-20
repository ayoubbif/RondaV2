using UnityEngine;
using Unity.Netcode;
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
        private readonly NetworkVariable<bool> _isTurnActive = new();
        
        
        // References
        [SerializeField] private TMP_Text turnIndicatorText;
        
        public void Initialize()
        {
            if (!IsServer) return;
            
            _isTurnActive.Value = false;
        }
        
        /// <summary>
        /// Starts the first turn of the game
        /// </summary>
        public void StartFirstTurn(ulong firstPlayerId)
        {
            if (!IsServer) return;
            
            CurrentPlayerTurnId.Value = firstPlayerId;
            _isTurnActive.Value = true;
            
            UpdateTurnIndicatorClientRpc(CurrentPlayerTurnId.Value);
        }
        
        /// <summary>
        /// Advances to the next player's turn
        /// </summary>
        public void AdvanceTurn(ulong nextPlayerId)
        {
            if (!IsServer) return;
            
            CurrentPlayerTurnId.Value = nextPlayerId;
            _isTurnActive.Value = true;
        
            UpdateTurnIndicatorClientRpc(CurrentPlayerTurnId.Value);
        }
        
        [ClientRpc]
        private void UpdateTurnIndicatorClientRpc(ulong currentPlayerId)
        {
            if (turnIndicatorText)
            {
                bool isLocalPlayerTurn = currentPlayerId == NetworkManager.LocalClientId;
                turnIndicatorText.text = isLocalPlayerTurn ? "You" : "Opponent";
                turnIndicatorText.color = isLocalPlayerTurn ? Color.green : Color.red;
            }
        }
        
        public bool IsPlayerTurn(ulong playerId)
        {
            return _isTurnActive.Value && CurrentPlayerTurnId.Value == playerId;
        }
    }
}