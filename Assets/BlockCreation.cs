using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockCreation : MonoBehaviour {
    public Transform blockPrefab;
    public Object sphere;
    public int index = 0;
    private float currTime;
    private float prevTime;
    private int ind = 0;
    public TextAsset textAsset;
    public string text;
    public string[] beats;
    private List<Transform> blocks = new List<Transform>();

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
            sphere = Instantiate(blockPrefab, new Vector3(xPos, yPos, 10.0F), Quaternion.identity);
            Transform pre = (Transform)sphere;
            blocks.Add(pre);
            ind++;
        }
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].position.z < 0.0F)
            {
                Destroy(blocks[i].gameObject);
                blocks.RemoveAt(i);
            }
        }
        index++;
	}
}
