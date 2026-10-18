using UnityEngine;

public class setactiveobject : MonoBehaviour
{
    public GameObject ui;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ui.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
