using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Halo : MonoBehaviour {
	private int count = 0;
    Light lt;

	// Use this for initialization
	void Start () {
        lt = GetComponent("Light") as Light;
	}
	
	// Update is called once per frame
	void Update () {
        move();
        count++;
	}

    void move()
    {
        if (count % 1 == 0)
        {
            transform.Translate(0, 0, -0.1F, Space.World);
            transform.localScale -= new Vector3(0.1F, 0.1F, 0.1F);
            lt.range -= 0.05F;
        }
    }
}
