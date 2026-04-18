using UnityEngine;

namespace SUI.Runtime
{
public class SuiCounterState : MonoBehaviour
{
    [SerializeField] private SuiRuntimeHost host;
    [SerializeField] private string title = "Sandbox UI";
    [SerializeField] private int count;

    private void Reset()
    {
        if (host == null)
        {
            host = GetComponent<SuiRuntimeHost>();
        }
    }

    private void OnEnable()
    {
        if (host == null)
        {
            return;
        }

        host.SetToken("title", title);
        host.SetToken("count", count.ToString());
    }

    public void Increment()
    {
        count++;
        if (host != null)
        {
            host.SetToken("count", count.ToString());
        }
    }
}
}

