using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public string JoinCode { get; private set; }

    bool isInitialized;

    public async Task InitializeAsync()
    {
        if (isInitialized) { return; }

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        isInitialized = true;
    }

    public async Task<string> CreateRelayAsync(int maxConnections)
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        SetTransport(allocation);

        string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        JoinCode = code;

        return code;
    }

    public async Task JoinRelayAsync(string code)
    {
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
        SetTransport(joinAllocation);

        JoinCode = code;
    }

    void SetTransport(Allocation allocation)
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData serverData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        transport.SetRelayServerData(serverData);
    }

    void SetTransport(JoinAllocation joinAllocation)
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData serverData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
        transport.SetRelayServerData(serverData);
    }
}
