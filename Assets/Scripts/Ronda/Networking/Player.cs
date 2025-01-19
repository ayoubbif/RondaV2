using System.Collections.Generic;
using System.Linq;
using KKL.Ronda.Core;
using Unity.Netcode;
using UnityEngine;

namespace KKL.Ronda.Networking
{
    public class Player : NetworkBehaviour
    {

        private readonly NetworkVariable<uint> _score = new();

        private static GameManager GameManager => GameManager.Instance; 


        [SerializeField] private List<Card> cardsInHand;

        public Card[] Cards { get; set; }
        public uint Score
        {
            get => _score.Value;
            set => _score.Value = value;
        }

        public List<Card> CardsInHand => cardsInHand.ToList();
    
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        
            if (IsOwner)
            {
                AddPlayerServerRpc();
            }
        }
    
        private void Start()
        {
            _score.OnValueChanged += ScoreChanged;
        }

        public override void OnDestroy()
        {
            _score.OnValueChanged -= ScoreChanged;
        }

        private static void ScoreChanged(uint oldValue, uint newValue)
        {
            GameManager.Instance.UpdateScoreUI();
        }
    
        public void InitializeCards(int size)
        {
            Cards = new Card[size];
        }

        public void SetCards(Card[] cards)
        {
            Cards = cards;
        }

        public void AddCardsToHand(List<Card> cards)
        {
            cardsInHand = cards;
        }
    
        public void AddScore(uint score)
        {
            _score.Value += score;
        }
    
        public void RemoveCardFromHand(Value value, Suit suit)
        {
            Card cardToRemove = cardsInHand.Find(c => c.Value == value && c.Suit == suit);

            if (cardToRemove != null)
            {
                cardsInHand.Remove(cardToRemove);
            }
        }
    
        [ClientRpc]
        private void AddPlayerClientRpc()
        {
            GameManager.AddPlayer(this);
        }

        [ServerRpc]
        private void AddPlayerServerRpc()
        {
            AddPlayerClientRpc();
        }
    }
}