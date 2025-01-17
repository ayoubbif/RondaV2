using System.Linq;
using UnityEngine;
using System.Collections.Generic;


namespace KKL.Ronda.Core
{
    public class Table : MonoBehaviour
    {
        public List<Card> Cards => cards.ToList();
        [SerializeField] private List<Card> cards;

        public Table(List<Card> cards)
        {
            this.cards = cards;
        }

        public void AddCardToTable(Card card)
        {
            cards.Add(card);
        }

        public void RemoveCardFromTable(Suit suit, Value value)
        {
            var cardToRemove = cards.Find(c => c.Value == value && c.Suit == suit);

            if (cardToRemove != null)
            {
                cards.Remove(cardToRemove);
            }
        }
    }
}