using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;


public class StartNetwork : MonoBehaviour
{

    public TextMeshProUGUI lblCode;
    public TMP_InputField inputCode;

    public async void Start()
    {
        // 1 - Iniciar los servicio de cloud
        await UnityServices.InitializeAsync();

        // 2 - iniciar sesión de usuario (anonimo)
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async void conectarComoHost()
    {
        // 3 - Pedir a Unity un servicio de Relay para nosotros
       Allocation servidorDeRelay = await RelayService.Instance.CreateAllocationAsync(12);

       // 4 - Configurar nuestro NetworkManager para usar el servidor de relay
       // 4.1 buscar el componente UnityTransport
        UnityTransport miTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // 4.2 generar los datosDelRelay (a partir de los datos del server)
        RelayServerData datosDelRelay = AllocationUtils.ToRelayServerData(servidorDeRelay, "dtls");     // udp dtls wss

        // 4.3 cambiar la configuración del UnityTransport por los del Relay
        miTransport.SetRelayServerData(datosDelRelay);

        // 5. iniciar el server
        NetworkManager.Singleton.StartHost();

        // 6. MOSTRAR EL CODIGO DE PARTIDA
        lblCode.text = await RelayService.Instance.GetJoinCodeAsync(servidorDeRelay.AllocationId);

    }

    public async void conectarComoCliente()
    {
        // 1 - obtener el serverRelay a partir del código de partida
        string codigoPartida = inputCode.text;

        JoinAllocation serverDeUnity = await RelayService.Instance.JoinAllocationAsync(codigoPartida);

        // 2 - configurar nuestro NetworkManager (UnityTransport)
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                AllocationUtils.ToRelayServerData(serverDeUnity, "dtls")
         );

        // 3 - Iniciar como Client
        NetworkManager.Singleton.StartClient();

    }
    
    public void desconectar()
    {
        NetworkManager.Singleton.Shutdown();
    }

}