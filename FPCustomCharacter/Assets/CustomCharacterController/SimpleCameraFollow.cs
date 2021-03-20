using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{

    [SerializeField] private Transform m_target;
    private Vector3 offset = new Vector3(0,0,0);

    void Start() => offset = transform.position - m_target.transform.position;    

    private void LateUpdate()
    {
        // Follow the target and look at it
        transform.position = offset + m_target.transform.position;
        transform.LookAt(m_target, Vector3.up);
    }
}
