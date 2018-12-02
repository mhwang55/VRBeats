using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HaloCreation : MonoBehaviour {
    public Transform haloPrefab;
    public int index = 0;
    private float currTime;
    private float prevTime;
    private int ind = 0;
    public TextAsset textAsset;
    public string text;
    public string[] beats;

	// Use this for initialization
	void Start () {
        prevTime = Time.time;
        text = textAsset.ToString();
        beats = text.Split('\n');
	}
	
	// Update is called once per frame
	void Update () {
        currTime = Time.time;
	    if (currTime - prevTime >= float.Parse(beats[ind]) / 1000.0F - 500.0F/1000.0F)
        {
            float xPos = (float)(ind % 5) - 2.0F;
            float yPos = (float)(ind % 5) - 2.0F;
            Instantiate(haloPrefab, new Vector3(xPos, yPos, 10.0F), Quaternion.identity);
            ind++;
        }
        index++;
	}
}
