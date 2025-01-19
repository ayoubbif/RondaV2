using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace KKL.Ronda.Networking
{
    public class RelayManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text joinCodeText;
        [SerializeField] private TMP_InputField joinInput;
        [SerializeField] private GameObject relayUI;
    
        private UnityTransport _transport;
        private const int MaxPlayers = 2;
        
        private async void Awake()
        {
            try
            {
                _transport = FindFirstObjectByType<UnityTransport>();
                if (_transport == null)
                {
                    throw new InvalidOperationException("UnityTransport component not found in scene");
                }
  
                await Authenticate();
                relayUI.SetActive(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize: {e.Message}");
                relayUI.SetActive(false);
            }
        }

        private static async Task Authenticate()
        {
            try 
            {
                await UnityServices.InitializeAsync();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (RequestFailedException e)
            {
                throw new Exception($"Authentication failed: {e.Message}", e);
            }
        }

        public async void CreateGame()
        {
            try
            {
                relayUI.SetActive(false);

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
                joinCodeText.text = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                _transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                NetworkManager.Singleton.StartHost();
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Relay service error: {e.Message}");
                relayUI.SetActive(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create game: {e.Message}");
                relayUI.SetActive(true);
            }
        }
    
        public async void JoinGame()
        {
            try
            {
                relayUI.SetActive(false);

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinInput.text);

                _transport.SetClientRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    allocation.HostConnectionData
                );

                NetworkManager.Singleton.StartClient();
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Relay service error: {e.Message}");
                relayUI.SetActive(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join game: {e.Message}");
                relayUI.SetActive(true);
            }
        }
    }
}