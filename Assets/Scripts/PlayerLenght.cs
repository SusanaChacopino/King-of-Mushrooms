using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEngine;

public class PlayerLenght : NetworkBehaviour
{
    [SerializeField] private GameObject tailprefab;
    public NetworkVariable<ushort> length = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [CanBeNull] public static event System.Action<ushort> ChangedLengthEvent;
    
    private List<GameObject> _tails;
    private Transform _lastTail;
    private Collider2D _collider2D;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _tails = new List<GameObject>();
        _lastTail = transform;
        _collider2D = GetComponent<Collider2D>();
        
        if (!IsServer) 
            length.OnValueChanged += LengthChangedEvent;
    }
    
    [ContextMenu("Add Length")]
    public void AddLength()
    {
        length.Value += 1;
        LengthChanged();
    }
    
    private void LengthChanged()
    {
        InstantiateTail();
        if (!IsOwner) return;
        ChangedLengthEvent?.Invoke(length.Value);
        ClientMusicPlayer.Instance.PlayNomAudioClip();
    }
    
    private void LengthChangedEvent(ushort previousValue, ushort newValue)
    {
        Debug.Log("LengthChanged Callback");
        LengthChanged();
    }
    
    private void InstantiateTail()
    {
        GameObject tailGameObject = Instantiate(tailprefab, transform.position, Quaternion.identity);
        tailGameObject.GetComponent<SpriteRenderer>().sortingOrder = -length.Value;
        
        if (tailGameObject.TryGetComponent(out Tail tail))
        {
            tail.networkedOwner = transform;
            tail.followTransform = _lastTail;
            _lastTail = tailGameObject.transform;
            Physics2D.IgnoreCollision(tailGameObject.GetComponent<Collider2D>(), _collider2D);
        }
        
        _tails.Add(tailGameObject);
    }
    
    // Método público para controlar la visibilidad de las colas
    public void SetTailsVisibility(bool isVisible)
    {
        // Verificación de null para evitar errores
        if (_tails == null)
        {
            Debug.LogWarning("_tails es null en SetTailsVisibility");
            return;
        }
        
        Debug.Log($"SetTailsVisibility llamado - IsVisible: {isVisible}, Cantidad de colas: {_tails.Count}");
        
        foreach (var tailObj in _tails)
        {
            if (tailObj != null)
            {
                // Ocultar/mostrar sprite de la cola
                var tailSprite = tailObj.GetComponent<SpriteRenderer>();
                if (tailSprite != null)
                {
                    tailSprite.enabled = isVisible;
                }
                
                // Deshabilitar/habilitar colisión de la cola
                var tailCollider = tailObj.GetComponent<Collider2D>();
                if (tailCollider != null)
                {
                    tailCollider.enabled = isVisible;
                }
                
                // También deshabilitar el script Tail para que no se actualice
                var tailScript = tailObj.GetComponent<Tail>();
                if (tailScript != null)
                {
                    tailScript.enabled = isVisible;
                }
            }
        }
    }
}