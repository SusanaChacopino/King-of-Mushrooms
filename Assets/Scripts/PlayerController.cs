using System;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 3f;
    [CanBeNull] public static event System.Action GameOverEvent;
    
    private Camera _mainCamera;
    private Vector3 _mouseInput = Vector3.zero;
    private PlayerLenght _playerLength;
    private readonly ulong[] _targetClientsArray = new ulong[1];
    
    // Variables para interpolación de movimiento
    private Vector3 _targetPosition;
    private NetworkVariable<Vector3> _networkPosition = new NetworkVariable<Vector3>(
        writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<Quaternion> _networkRotation = new NetworkVariable<Quaternion>(
        writePerm: NetworkVariableWritePermission.Owner);
    
    // Estado del jugador
    private NetworkVariable<bool> _isAlive = new NetworkVariable<bool>(
        true, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private SpriteRenderer[] _spriteRenderers;
    private Collider2D[] _colliders;
    
    private void Initialize()
    {
        _mainCamera = Camera.main;
        _playerLength = GetComponent<PlayerLenght>();
        _targetPosition = transform.position;
        
        // Obtener solo los componentes visuales y de colisión de la cabeza
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _colliders = GetComponentsInChildren<Collider2D>();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
        
        if (IsOwner)
        {
            _networkPosition.Value = transform.position;
            _networkRotation.Value = transform.rotation;
        }
        
        // Suscribirse a cambios en el estado de vida
        _isAlive.OnValueChanged += OnAliveStateChanged;
        
        // NO llamar a UpdateVisibility aquí, esperar un frame
        // para que PlayerLenght se inicialice
    }
    
    public override void OnNetworkDespawn()
    {
        _isAlive.OnValueChanged -= OnAliveStateChanged;
        base.OnNetworkDespawn();
    }
    
    private void OnAliveStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"OnAliveStateChanged - Cliente: {OwnerClientId}, IsOwner: {IsOwner}, Alive: {newValue}");
        UpdateVisibility(newValue);
        
        // Si el jugador muere y es el owner, mostrar game over
        if (!newValue && IsOwner)
        {
            Debug.Log("Invocando GameOverEvent para el owner");
            GameOverEvent?.Invoke();
        }
    }
    
    private void UpdateVisibility(bool isVisible)
    {
        Debug.Log($"UpdateVisibility llamado - IsVisible: {isVisible}, Cliente: {OwnerClientId}");
        
        // Mostrar/ocultar el jugador (cabeza)
        foreach (var spriteRenderer in _spriteRenderers)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = isVisible;
        }
        
        // Habilitar/deshabilitar colisiones
        foreach (var col in _colliders)
        {
            if (col != null)
                col.enabled = isVisible;
        }
        
        // Ocultar/mostrar todas las colas del jugador (con verificación de null)
        if (_playerLength != null)
        {
            _playerLength.SetTailsVisibility(isVisible);
        }
    }
    
    private void Update()
    {
        if (!_isAlive.Value) return;
        
        if (IsOwner && Application.isFocused)
        {
            HandleMovement();
        }
        else if (!IsOwner)
        {
            // Interpolación suave para clientes remotos
            transform.position = Vector3.Lerp(transform.position, _networkPosition.Value, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation.Value, Time.deltaTime * 15f);
        }
    }
    
    private void HandleMovement()
    {
        // Movimiento
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
        
        _targetPosition = Vector3.MoveTowards(transform.position, mouseWorldCoordinates, Time.deltaTime * speed);
        transform.position = _targetPosition;
        
        // Rotación
        if (Vector3.Distance(mouseWorldCoordinates, transform.position) > 0.1f)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;
        }
        
        // Actualizar posición en red
        _networkPosition.Value = transform.position;
        _networkRotation.Value = transform.rotation;
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void DetermineCollisionWinnerServerRpc(PlayerData player1, PlayerData player2)
    {
        Debug.Log($"DetermineCollisionWinnerServerRpc - Player1 (ID: {player1.Id}, Length: {player1.Length}) vs Player2 (ID: {player2.Id}, Length: {player2.Length})");
        
        if (player1.Length > player2.Length)
        {
            WinInformationServerRpc(player1.Id, player2.Id);
        }
        else if (player2.Length > player1.Length)
        {
            WinInformationServerRpc(player2.Id, player1.Id);
        }
        else
        {
            // En caso de empate, gana el que inició la colisión (player1)
            WinInformationServerRpc(player1.Id, player2.Id);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void WinInformationServerRpc(ulong winner, ulong loser)
    {
        Debug.Log($"WinInformationServerRpc - Winner: {winner}, Loser: {loser}");
        
        // Encontrar al perdedor y marcarlo como muerto
        foreach (var kvp in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            if (kvp.Value.TryGetComponent(out PlayerController controller))
            {
                if (controller.OwnerClientId == loser)
                {
                    Debug.Log($"Marcando jugador {loser} como muerto");
                    controller._isAlive.Value = false;
                    break;
                }
            }
        }
        
        // Notificar al ganador
        _targetClientsArray[0] = winner;
        ClientRpcParams winnerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientsArray
            }
        };
        AtePlayerClientRpc(winnerParams);
        
        // Notificar al perdedor
        _targetClientsArray[0] = loser;
        ClientRpcParams loserParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientsArray
            }
        };
        GameOverClientRpc(loserParams);
    }
    
    [ClientRpc]
    private void AtePlayerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log("¡Te has comido a un jugador!");
    }
    
    [ClientRpc]
    private void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log("GameOverClientRpc - Invocando evento");
        GameOverEvent?.Invoke();
    }
    
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!_isAlive.Value) return;
        if (!col.gameObject.CompareTag("Player")) return;
        if (!IsOwner) return;
        
        Debug.Log("Player Collision detectada");
        
        // Colisión con cabeza de otro jugador
        if (col.gameObject.TryGetComponent(out PlayerLenght otherPlayerLength))
        {
            if (col.gameObject.TryGetComponent(out PlayerController otherController))
            {
                if (!otherController._isAlive.Value) return;
            }
            
            Debug.Log("Colisión con cabeza de otro jugador");
            
            var player1 = new PlayerData()
            {
                Id = OwnerClientId,
                Length = _playerLength.length.Value
            };
            
            var player2 = new PlayerData()
            {
                Id = otherPlayerLength.OwnerClientId,
                Length = otherPlayerLength.length.Value
            };
            
            DetermineCollisionWinnerServerRpc(player1, player2);
        }
        // Colisión con cola de otro jugador
        else if (col.gameObject.TryGetComponent(out Tail tail))
        {
            Debug.Log("Colisión con cola de otro jugador");
            
            if (tail.networkedOwner != null)
            {
                var tailOwnerController = tail.networkedOwner.GetComponent<PlayerController>();
                if (tailOwnerController != null && tailOwnerController._isAlive.Value)
                {
                    WinInformationServerRpc(tailOwnerController.OwnerClientId, OwnerClientId);
                }
            }
        }
    }
    
    struct PlayerData : INetworkSerializable
    {
        public ulong Id;
        public ushort Length;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Length);
        }
    }
}