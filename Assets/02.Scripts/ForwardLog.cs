using UnityEngine;

public class BarrelForward : MonoBehaviour
{
    void Update()
    {
        Vector3 objectForward = transform.forward;

        Debug.Log($"{name}ÀÇ Forward : {objectForward}");
    }
}
