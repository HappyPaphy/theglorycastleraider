using UnityEngine;

public class HintToggle : MonoBehaviour
{
    [SerializeField] private GameObject obj_XboxHint;
    [SerializeField] private GameObject obj_PcHint;

    private void Start()
    {
        obj_PcHint.SetActive(false);
        obj_XboxHint.SetActive(false);
    }

    void Update()
    {
        if(InputSchemeManager.instance != null)
        {
            switch(InputSchemeManager.instance.CurrentInputMode)
            {
                case InputMode.PC:
                    {
                        if(!obj_PcHint.activeInHierarchy)
                        {
                            obj_PcHint.SetActive(true);
                            obj_XboxHint.SetActive(false);
                        }
                    }
                    break;

                case InputMode.Xbox:
                    {
                        if (!obj_XboxHint.activeInHierarchy)
                        {
                            obj_XboxHint.SetActive(true);
                            obj_PcHint.SetActive(false);
                        }
                    }
                    break;
            }
        }
    }
}
