using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine.UI;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class MultiplayerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


private async void Awake()
{
    await UnityServices.InitializeAsync();
    await AuthenticationService.Instance.SignInAnonymouslyAsync();
}

void Start()
    {
        hostButton.onClick.AddListener(HostButtonOnClick);
        clientButton.onClick.AddListener(ClientButtonOnClick);
        
        
    }

    [SerializeField] private TextMeshProUGUI JoinCodeText;
    private async void HostButtonOnClick()
    {
        try
        {
            
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            JoinCodeText.text = JoinCode;
            
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            
            NetworkManager.Singleton.StartHost();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
        
    }

    [SerializeField] private TMP_InputField JoinCodeInput;
    private async void ClientButtonOnClick()
    {
        try
        {
           JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(JoinCodeInput.text);
            
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
        
    }
}