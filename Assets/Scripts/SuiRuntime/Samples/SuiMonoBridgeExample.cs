using UnityEngine;

namespace SUI.Runtime.Samples
{
public sealed class SuiMonoBridgeExample : MonoBehaviour
{
    [SerializeField] private SuiRuntimeHost host;

    private void Awake()
    {
        if (host == null)
        {
            host = GetComponent<SuiRuntimeHost>();
        }
    }

    public void Ping()
    {
        UnityEngine.Debug.Log("SUI Mono bridge Ping()");
        if (host != null)
        {
            host.SetToken("title", "Ping @ " + System.DateTime.Now.ToLongTimeString());
        }
    }

    public void Submit()
    {
        UnityEngine.Debug.Log("Submit() from MonoBehaviour bridge");
    }
}
}
