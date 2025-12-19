using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SpawnFoodStart;
    }

    private void SpawnFoodStart()
    {
        NetworkManager.Singleton.OnServerStarted -= SpawnFoodStart;
        //NetworkObjectPool.Singleton.InitializePool();
        for (int i = 0; i < 30; i++)
        {
            SpawnFood();
        }
        StartCoroutine(SpawnOverTime());
    }

    private void SpawnFood()
    {
        NetworkObject obj = NetworkObjectPool.Singleton.GetNetworkObject(prefab, GetRandomPositionOnMap(), Quaternion.identity);
        obj.GetComponent<Food>().prefab = prefab;
        obj.Spawn(true);
    }
    private Vector3 GetRandomPositionOnMap()
    {
        return new  Vector3 (Random.Range(-17f,17f),Random.Range(-9,9),0f);
    }

    private IEnumerator SpawnOverTime()
    {
        while (NetworkManager.Singleton.ConnectedClients.Count > 0)
        {
            yield return new WaitForSeconds(2f);
            SpawnFood();
        }
    }
}
