using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace KKL.Ronda.Networking
{
    public class Player : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString32Bytes> _playerName = new();
        private readonly NetworkVariable<uint> _score = new();
    }
}