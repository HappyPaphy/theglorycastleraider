using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LocalizedFontAddToList : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private bool hasAddToListOnce = false;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if(textMesh != null && !hasAddToListOnce)
        {
            hasAddToListOnce = true;
            LocalizedFontChangerManager.instance.AddTextMeshToList(textMesh);
        }
    }
}
