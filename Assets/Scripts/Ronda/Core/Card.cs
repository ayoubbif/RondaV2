using System;
using Unity.Netcode;

namespace KKL.Ronda.Core
{
    [Serializable]
    public class Card : INetworkSerializable
    {
        public Suit Suit => _suit;
        private Suit _suit;

        public Value Value => _value;
        private Value _value;

        public Card() { }

        public Card(Suit suit, Value value)
        {
            _suit = suit;
            _value = value;
        }
    
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _suit);
            serializer.SerializeValue(ref _value);
        }
    }
}