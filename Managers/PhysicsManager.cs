using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    void OnEnable() => Physics.autoSyncTransforms = false;
    void LateUpdate() => Physics.SyncTransforms(); 
}