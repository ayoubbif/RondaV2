using System;
using System.Collections.Generic;
using System.Linq;

namespace KKL.Ronda.Core
{
    public abstract class Rules
    {
        private const int TotalDeckSize = 40;
        private const int WahadPoints = 1;
        private const int KhamsaPoints = 5;
        private const int AshraPoints = 10;
        private const int MissaPoints = 1;
        private const int FinalThrowPoints = 5;

        public static bool CanCapture(Card playedCard, List<Card> tableCards)
        {
            return tableCards.Any(card => card.Value == playedCard.Value);
        }

        public static List<Card> GetCaptureableCards(Card playedCard, List<Card> tableCards)
        {
            var captureable = new HashSet<Card>();
            var matchingCards = tableCards.Where(card => card.Value == playedCard.Value).ToList();

            foreach (var matchingCard in matchingCards)
            {
                captureable.Add(matchingCard);
                
                var forwardSequence = GetConsecutiveSequence(matchingCard, tableCards, true);
                var backwardSequence = GetConsecutiveSequence(matchingCard, tableCards, false);
                
                foreach (var card in forwardSequence.Concat(backwardSequence))
                {
                    captureable.Add(card);
                }
            }

            return captureable.ToList();
        }

        private static List<Card> GetConsecutiveSequence(Card startCard, List<Card> tableCards, bool forward)
        {
            var sequence = new List<Card>();
            var currentValue = startCard.Value;
            var remainingCards = tableCards.Where(c => c != startCard).ToList();

            while (true)
            {
                if (currentValue == Value.Seven && forward)
                    break;
                
                if (currentValue == Value.Ten && !forward)
                    break;

                // Convert to byte since Value enum is byte-based
                var nextValueByte = (byte)((byte)currentValue + (forward ? 1 : -1));
                
                // Check if the next value exists in the Value enum
                if (!Enum.IsDefined(typeof(Value), nextValueByte))
                    break;
                
                var nextValue = (Value)nextValueByte;
                var nextCard = remainingCards.FirstOrDefault(c => c.Value == nextValue);
                
                if (nextCard == null)
                    break;

                sequence.Add(nextCard);
                remainingCards.Remove(nextCard);
                currentValue = nextValue;
            }

            return sequence;
        }

        public static int CalculateCapturePoints(List<Card> capturedCards, bool isLastCapture)
        {
            var points = capturedCards.Count; // Base points
            points += CalculateSpecialMovePoints(capturedCards);

            if (isLastCapture)
            {
                points += CalculateFinalThrowPoints(capturedCards);
            }

            return points;
        }

        private static int CalculateSpecialMovePoints(List<Card> cards)
        {
            var points = 0;

            // Special card bonuses - removed Value.Five for test card
            if (cards.Any(c => c.Value == Value.One)) points += WahadPoints;
            if (cards.Any(c => c.Value == Value.Ten)) points += AshraPoints;
            
            // Only add Khamsa points if it's not part of a Missa sequence
            if (cards.Any(c => c.Value == Value.Five) && !HasValidMissaSequence(cards))
            {
                points += KhamsaPoints;
            }
            
            // Sequence bonus
            if (HasValidMissaSequence(cards)) points += MissaPoints;

            return points;
        }

        private static int CalculateFinalThrowPoints(List<Card> cards)
        {
            if (cards.Any(c => c.Value == Value.Twelve || c.Value == Value.One))
            {
                return FinalThrowPoints;
            }
            return 0;
        }

        public static bool HasValidMissaSequence(List<Card> cards)
        {
            if (cards.Count < 3) return false;

            var orderedCards = cards.OrderBy(c => (byte)c.Value).ToList();
    
            for (var i = 0; i < orderedCards.Count - 2; i++)
            {
                var current = (byte)orderedCards[i].Value;
                var next = (byte)orderedCards[i + 1].Value;
                var nextNext = (byte)orderedCards[i + 2].Value;

                // Check for consecutive sequence
                if (next == current + 1 && nextNext == next + 1)
                {
                    // Don't allow sequences to continue past 7
                    if (nextNext <= (byte)Value.Seven)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsValidDeck(Deck deck)
        {
            var cards = deck.Cards;
            
            if (cards.Count != TotalDeckSize)
                return false;

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Value value in Enum.GetValues(typeof(Value)))
                {
                    if (cards.Count(c => c.Suit == suit && c.Value == value) != 1)
                        return false;
                }
            }

            return true;
        }
    }
}