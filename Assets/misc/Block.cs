using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour
{
	private int count = 0;

	private Vector3 movement;
	private Vector3 stop;
	int colorType = 0;

	private double[][] colors = new double[][]
	{
		///*
		// cyan
		new double[] {0.5, 1.0, 1.0},
		// yellow
		new double[] {1/6.0, 1.0, 1.0},
		// purple
		new double[] {5/6.0, 1.0, 0.502},
		// green
		new double[] {1/3.0, 1.0, 1.0},
		// red
		new double[] {0, 1.0, 1.0},
		// blue
		new double[] {2/3.0, 1.0, 1.0},
		// orange
		new double[] {33/360.0, 1.0, 1.0}
		//*/
	};

	// Use this for initialization
	void Start ()
	{
		Renderer rend = GetComponent<Renderer>();

		rend.material.shader = Shader.Find("_Color");
		//double[] color = colors[count % 6];
		double[] color = colors[3];
		rend.material.SetColor("_Color", Color.HSVToRGB((float) color[0], (float) color[1], (float) color[2]));

		rend.material.shader = Shader.Find("Specular");
		rend.material.SetColor("_SpecColor", Color.HSVToRGB((float) color[0], (float) color[1], (float) color[2]));
		//Debug.Log(colorType);
	}
	
	// Update is called once per frame
	void Update () {
        move();
        count++;
        /*
        if (transform.position.z < 0.0F)
            Destroy(transform.gameObject);
        */
	}

    void move()
    {
		if (count % 1 == 0)
            transform.Translate(0, 0, -0.1F, Space.World);
    }
}
